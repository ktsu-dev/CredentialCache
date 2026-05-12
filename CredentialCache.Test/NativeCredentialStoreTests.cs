// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache.Test;

using System.Runtime.InteropServices;
using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;

/// <summary>
/// Exercises the platform-native credential store returned by
/// <see cref="CredentialStoreFactory.CreateDefault(string)"/> on whatever OS
/// the test happens to be running on. The store is scoped to a per-run service
/// name so the tests don't collide with other applications' real credentials.
///
/// On Linux these require a running Secret Service implementation (e.g.
/// <c>gnome-keyring-daemon</c> launched under <c>dbus-run-session</c>). The
/// cross-platform CI workflow provides one; locally they will fail-fast with
/// a clear <see cref="CredentialStoreException"/> if no daemon is available.
/// </summary>
[TestClass]
public class NativeCredentialStoreTests
{
	private static string UniqueServiceName() =>
		$"ktsu.CredentialCache.IntegrationTest.{Guid.NewGuid():N}";

	private static ICredentialStore CreateNativeStore() =>
		CredentialStoreFactory.CreateDefault(UniqueServiceName());

	/// <summary>
	/// Performs a tiny no-op call against the native store to verify the platform
	/// dependencies are actually present (e.g. libsecret loaded and a Secret
	/// Service daemon is reachable on Linux). If not, the test is reported as
	/// <see cref="Assert.Inconclusive(string)"/> rather than failed, so a
	/// developer without a keyring set up doesn't see scary red.
	/// </summary>
	private static void AssertNativeStoreAvailableOrInconclusive(ICredentialStore store)
	{
		PersonaGUID probe = CredentialCache.CreatePersonaGUID();
		try
		{
			// Removing a key that doesn't exist must not throw on a working backend.
			_ = store.Remove(probe);
		}
		catch (Exception ex) when (IsMissingPlatformDependency(ex))
		{
			Assert.Inconclusive($"Native credential store is not available in this environment: {ex.GetType().Name}: {ex.Message}");
		}
	}

	private static bool IsMissingPlatformDependency(Exception ex)
	{
		// Walk the inner-exception chain - libsecret/Security.framework load
		// failures surface inside a TypeInitializationException when triggered
		// during a static cctor (e.g. the libsecret schema handle).
		for (Exception? current = ex; current is not null; current = current.InnerException)
		{
			if (current is DllNotFoundException or CredentialStoreException)
			{
				return true;
			}
		}
		return false;
	}

	[TestMethod]
	public void NativeStoreRoundTripsCredentialWithToken()
	{
		ICredentialStore store = CreateNativeStore();
		AssertNativeStoreAvailableOrInconclusive(store);
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();
		Credential original = new CredentialWithToken
		{
			Token = SemanticString<CredentialToken>.Create("native-test-token"),
		};

		try
		{
			store.Save(persona, original);

			Assert.IsTrue(store.TryLoad(persona, out Credential? loaded));
			CredentialWithToken? typed = loaded as CredentialWithToken;
			Assert.IsNotNull(typed);
			Assert.AreEqual("native-test-token", typed!.Token.ToString());
		}
		finally
		{
			store.Remove(persona);
		}
	}

	[TestMethod]
	public void NativeStoreSaveOverwritesExistingEntry()
	{
		ICredentialStore store = CreateNativeStore();
		AssertNativeStoreAvailableOrInconclusive(store);
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();

		try
		{
			store.Save(persona, new CredentialWithToken
			{
				Token = SemanticString<CredentialToken>.Create("first"),
			});
			store.Save(persona, new CredentialWithToken
			{
				Token = SemanticString<CredentialToken>.Create("second"),
			});

			Assert.IsTrue(store.TryLoad(persona, out Credential? loaded));
			Assert.AreEqual("second", ((CredentialWithToken)loaded!).Token.ToString());
		}
		finally
		{
			store.Remove(persona);
		}
	}

	[TestMethod]
	public void NativeStoreRemoveReturnsFalseForUnknownPersona()
	{
		ICredentialStore store = CreateNativeStore();
		AssertNativeStoreAvailableOrInconclusive(store);
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();

		Assert.IsFalse(store.Remove(persona));
		Assert.IsFalse(store.TryLoad(persona, out _));
	}

	[TestMethod]
	public void NativeStoreSurvivesAcrossStoreInstances()
	{
		string service = UniqueServiceName();
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();
		Credential original = new CredentialWithUsernamePassword
		{
			Username = SemanticString<CredentialUsername>.Create("bob"),
			Password = SemanticString<CredentialPassword>.Create("sekrit"),
		};

		ICredentialStore writer = CredentialStoreFactory.CreateDefault(service);
		AssertNativeStoreAvailableOrInconclusive(writer);
		try
		{
			writer.Save(persona, original);

			ICredentialStore reader = CredentialStoreFactory.CreateDefault(service);
			Assert.IsTrue(reader.TryGet(persona, out Credential? loaded));
			CredentialWithUsernamePassword? typed = loaded as CredentialWithUsernamePassword;
			Assert.IsNotNull(typed);
			Assert.AreEqual("bob", typed!.Username.ToString());
			Assert.AreEqual("sekrit", typed.Password.ToString());
		}
		finally
		{
			writer.Remove(persona);
		}
	}

	[TestMethod]
	public void WindowsStoreEnumerateKeysReturnsWrittenPersonas()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			Assert.Inconclusive("EnumerateKeys is only implemented on Windows Credential Manager.");
			return;
		}

		ICredentialStore store = CreateNativeStore();
		AssertNativeStoreAvailableOrInconclusive(store);
		ISearchableCredentialStore? searchable = store as ISearchableCredentialStore;
		Assert.IsNotNull(searchable, "Windows store should implement ISearchableCredentialStore.");

		PersonaGUID persona = CredentialCache.CreatePersonaGUID();
		try
		{
			searchable!.Save(persona, new CredentialWithNothing());
			IEnumerable<PersonaGUID> keys = searchable.EnumerateKeys();
			Assert.IsTrue(keys.Any(k => string.Equals(k.ToString(), persona.ToString(), StringComparison.Ordinal)));
		}
		finally
		{
			searchable!.Remove(persona);
		}
	}
}

/// <summary>
/// Small helper that wires TryLoad through as TryGet for symmetry with
/// CredentialCache's API in test assertions. Keeps the assertion sites readable.
/// </summary>
internal static class CredentialStoreTestExtensions
{
	public static bool TryGet(this ICredentialStore store, PersonaGUID persona, out Credential? credential)
		=> store.TryLoad(persona, out credential);
}
