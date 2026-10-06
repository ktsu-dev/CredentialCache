// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ktsu.CredentialCache.Storage;

/// <summary>
/// Covers the Linux store on a host without libsecret (a headless server, SSH session or
/// container). Callers are told to catch <see cref="CredentialStoreException"/> and fall back,
/// so that is what every operation must throw, never a <see cref="TypeInitializationException"/>.
/// </summary>
[TestClass]
public class LibsecretMissingTests
{
	[TestMethod]
	public void TranslateMissingLibraryWrapsDllNotFoundException()
	{
		if (!OperatingSystem.IsLinux())
		{
			Assert.Inconclusive("The libsecret store is only built on Linux.");
			return;
		}

		AssertTranslated(new DllNotFoundException("Unable to load shared library 'libsecret-1.so.0'"));
	}

	[TestMethod]
	public void TranslateMissingLibraryWrapsEntryPointNotFoundException()
	{
		if (!OperatingSystem.IsLinux())
		{
			Assert.Inconclusive("The libsecret store is only built on Linux.");
			return;
		}

		AssertTranslated(new EntryPointNotFoundException("secret_schema_new"));
	}

	[TestMethod]
	public void TranslateMissingLibraryLeavesOtherFailuresAlone()
	{
		if (!OperatingSystem.IsLinux())
		{
			Assert.Inconclusive("The libsecret store is only built on Linux.");
			return;
		}

		AssertNotTranslated();
	}

	[TestMethod]
	public void EveryOperationThrowsCredentialStoreExceptionWhenLibsecretIsMissing()
	{
		if (!OperatingSystem.IsLinux())
		{
			Assert.Inconclusive("The libsecret store is only built on Linux.");
			return;
		}

		if (NativeLibrary.TryLoad("libsecret-1.so.0", out IntPtr handle))
		{
			NativeLibrary.Free(handle);
			Assert.Inconclusive("libsecret is installed here, so the missing-library path cannot be reached.");
			return;
		}

		AssertEveryOperationThrows();
	}

	[SupportedOSPlatform("linux")]
	private static void AssertTranslated(Exception nativeFailure)
	{
		CredentialStoreException exception = Assert.ThrowsExactly<CredentialStoreException>(
			() => LinuxSecretServiceCredentialStore.TranslateMissingLibrary<IntPtr>(() => throw nativeFailure));

		Assert.AreSame(nativeFailure, exception.InnerException);
		StringAssert.Contains(exception.Message, "libsecret");
	}

	[SupportedOSPlatform("linux")]
	private static void AssertNotTranslated()
	{
		Assert.AreEqual(42, LinuxSecretServiceCredentialStore.TranslateMissingLibrary(() => 42));
		Assert.ThrowsExactly<InvalidOperationException>(
			() => LinuxSecretServiceCredentialStore.TranslateMissingLibrary<int>(() => throw new InvalidOperationException()));
	}

	[SupportedOSPlatform("linux")]
	private static void AssertEveryOperationThrows()
	{
		LinuxSecretServiceCredentialStore store = new($"ktsu.CredentialCache.MissingLibsecretTest.{Guid.NewGuid():N}");
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();

		// Twice each: a type initializer would fail once and then rethrow its cached
		// TypeInitializationException on every later call.
		for (int attempt = 0; attempt < 2; attempt++)
		{
			Assert.ThrowsExactly<CredentialStoreException>(() => store.TryLoad(persona, out _));
			Assert.ThrowsExactly<CredentialStoreException>(() => store.Remove(persona));
			Assert.ThrowsExactly<CredentialStoreException>(() => store.Save(persona, new CredentialWithNothing()));
		}
	}
}
