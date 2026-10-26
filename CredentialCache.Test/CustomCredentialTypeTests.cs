// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using ktsu.CredentialCache.Storage;

/// <summary>
/// Tests that a <see cref="Credential"/> subclass defined outside the library can be registered and
/// stored, and that one which was never registered is refused the same way by every store.
/// </summary>
/// <remarks>
/// Registrations are process-wide and cannot be undone, so each test uses its own subclass and its
/// own discriminator.
/// </remarks>
[TestClass]
public class CustomCredentialTypeTests
{
	public sealed class CredentialWithCertificate : Credential
	{
		public string Thumbprint { get; init; } = "";
	}

	public sealed class CredentialWithSshKey : Credential
	{
		public string KeyId { get; init; } = "";
	}

	public sealed class NeverRegisteredCredential : Credential
	{
		public string Value { get; init; } = "";
	}

	public sealed class TwiceRegisteredCredential : Credential { }

	public sealed class ConflictingCredential : Credential { }

	public abstract class AbstractCredential : Credential { }

	private static PersonaGUID NewPersona() => PersonaGUID.Create(Guid.NewGuid().ToString());

	[TestMethod]
	public void ARegisteredSubclassRoundTripsThroughSerialization()
	{
		CredentialSerialization.RegisterDerivedType<CredentialWithCertificate>("Test.CredentialWithCertificate");

		byte[] json = CredentialSerialization.Serialize(new CredentialWithCertificate { Thumbprint = "ab" });
		Credential? read = CredentialSerialization.Deserialize(json);

		CredentialWithCertificate certificate = Assert.IsInstanceOfType<CredentialWithCertificate>(read);
		Assert.AreEqual("ab", certificate.Thumbprint);
	}

	[TestMethod]
	public void ARegisteredSubclassRoundTripsThroughTheNativeBuffer()
	{
		CredentialSerialization.RegisterDerivedType<CredentialWithSshKey>("Test.CredentialWithSshKey");

		using NativeSecretBuffer buffer = NativeSecretBuffer.OfCredential(new CredentialWithSshKey { KeyId = "key-1" });
		Credential? read = NativeSecretBuffer.ReadCredential(buffer.Pointer);

		CredentialWithSshKey key = Assert.IsInstanceOfType<CredentialWithSshKey>(read);
		Assert.AreEqual("key-1", key.KeyId);
	}

	[TestMethod]
	public void AddOrReplaceRefusesAnUnregisteredSubclassAndLeavesTheCacheUnchanged()
	{
		InMemoryCredentialStore store = new();
		using CredentialCache cache = new(store);
		PersonaGUID persona = NewPersona();
		CredentialWithToken original = new() { Token = CredentialToken.Create("original") };
		cache.AddOrReplace(persona, original);

		CredentialStoreException exception = Assert.ThrowsExactly<CredentialStoreException>(
			() => cache.AddOrReplace(persona, new NeverRegisteredCredential { Value = "x" }));

		Assert.Contains(nameof(NeverRegisteredCredential), exception.Message);
		Assert.IsTrue(cache.TryGet(persona, out Credential? cached));
		Assert.AreSame(original, cached);
		Assert.IsTrue(store.TryLoad(persona, out Credential? stored));
		Assert.AreSame(original, stored);
	}

	[TestMethod]
	public void RegisteringTheSameTypeAndDiscriminatorAgainDoesNothing()
	{
		CredentialSerialization.RegisterDerivedType<TwiceRegisteredCredential>("Test.TwiceRegisteredCredential");
		CredentialSerialization.RegisterDerivedType<TwiceRegisteredCredential>("Test.TwiceRegisteredCredential");

		_ = Assert.ThrowsExactly<ArgumentException>(
			() => CredentialSerialization.RegisterDerivedType<TwiceRegisteredCredential>("Test.SomethingElse"));
	}

	[TestMethod]
	public void ADiscriminatorThatAlreadyNamesAnotherTypeIsRefused()
	{
		_ = Assert.ThrowsExactly<ArgumentException>(
			() => CredentialSerialization.RegisterDerivedType<ConflictingCredential>(nameof(CredentialWithToken)));

		// The refusal left nothing behind.
		using CredentialCache cache = new(new InMemoryCredentialStore());
		_ = Assert.ThrowsExactly<CredentialStoreException>(
			() => cache.AddOrReplace(NewPersona(), new ConflictingCredential()));
	}

	[TestMethod]
	public void TheLibrarysOwnAndAbstractTypesCannotBeRegistered()
	{
		_ = Assert.ThrowsExactly<ArgumentException>(
			() => CredentialSerialization.RegisterDerivedType<CredentialWithToken>("Test.Token"));
		_ = Assert.ThrowsExactly<ArgumentException>(
			() => CredentialSerialization.RegisterDerivedType<AbstractCredential>("Test.Abstract"));
		_ = Assert.ThrowsExactly<ArgumentException>(
			() => CredentialSerialization.RegisterDerivedType<CredentialWithCertificate>(" "));
	}
}
