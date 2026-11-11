// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using System.Text;
using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;

/// <summary>
/// Tests that a credential whose non-nullable property is null is neither read back as found
/// nor written to a store, so no caller is handed a credential that throws on first use.
/// </summary>
[TestClass]
public class NullPropertyTests
{
	private const string NullToken = "{\"$type\":\"CredentialWithToken\",\"Token\":null}";
	private const string NullUsername = "{\"$type\":\"CredentialWithUsernamePassword\",\"Username\":null,\"Password\":\"hunter2\"}";
	private const string NullPassword = "{\"$type\":\"CredentialWithUsernamePassword\",\"Username\":\"alice\",\"Password\":null}";

	[TestMethod]
	[DataRow(NullToken)]
	[DataRow(NullUsername)]
	[DataRow(NullPassword)]
	public void DeserializeReturnsNullForANullNonNullableProperty(string json) =>
		Assert.IsNull(CredentialSerialization.Deserialize(Encoding.UTF8.GetBytes(json)));

	[TestMethod]
	[DataRow(NullToken)]
	[DataRow(NullUsername)]
	[DataRow(NullPassword)]
	public void DeserializeFromStringReturnsNullForANullNonNullableProperty(string json) =>
		Assert.IsNull(CredentialSerialization.DeserializeFromString(json));

	[TestMethod]
	[DataRow("{\"$type\":\"CredentialWithToken\",\"Token\":\"abc\"}")]
	[DataRow("{\"$type\":\"CredentialWithUsernamePassword\",\"Username\":\"alice\",\"Password\":\"hunter2\"}")]
	[DataRow("{\"$type\":\"CredentialWithNothing\"}")]
	public void DeserializeStillReadsCompleteCredentials(string json) =>
		Assert.IsNotNull(CredentialSerialization.Deserialize(Encoding.UTF8.GetBytes(json)));

	[TestMethod]
	[DataRow(NullToken)]
	[DataRow(NullUsername)]
	[DataRow(NullPassword)]
	public void TryGetReturnsFalseForAStoredEntryWithANullNonNullableProperty(string json)
	{
		RawBlobCredentialStore store = new();
		using CredentialCache cache = new(store);
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();
		store.Blobs[persona] = Encoding.UTF8.GetBytes(json);

		bool found = cache.TryGet(persona, out Credential? credential);

		Assert.IsFalse(found);
		Assert.IsNull(credential);
	}

	[TestMethod]
	public void SerializeRejectsANullNonNullableProperty()
	{
		CredentialWithToken credential = new() { Token = null! };

		Assert.ThrowsExactly<ArgumentException>(() => CredentialSerialization.Serialize(credential));
		Assert.ThrowsExactly<ArgumentException>(() => CredentialSerialization.SerializeToString(credential));
	}

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public void AddOrReplaceRejectsANullNonNullablePropertyAndLeavesTheStoreAndCacheUnchanged(bool inMemory)
	{
		RawBlobCredentialStore rawStore = new();
		InMemoryCredentialStore memoryStore = new();
		using CredentialCache cache = inMemory ? new(memoryStore) : new(rawStore);
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();
		CredentialWithUsernamePassword credential = new()
		{
			Username = SemanticString<CredentialUsername>.Create("alice"),
			Password = null!,
		};

		Assert.ThrowsExactly<ArgumentException>(() => cache.AddOrReplace(persona, credential));

		Assert.IsFalse(rawStore.Blobs.ContainsKey(persona));
		Assert.IsFalse(memoryStore.TryLoad(persona, out _));
		Assert.IsFalse(cache.TryGet(persona, out _));
	}

	[TestMethod]
	public void InMemoryStoreSaveRejectsANullNonNullablePropertyAsAPlatformStoreWould()
	{
		InMemoryCredentialStore store = new();
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();

		Assert.ThrowsExactly<ArgumentException>(() => store.Save(persona, new CredentialWithToken { Token = null! }));

		Assert.IsFalse(store.TryLoad(persona, out _));
	}

	[TestMethod]
	public void InMemoryStoreStillSavesACompleteCredential()
	{
		using CredentialCache cache = new(new InMemoryCredentialStore());
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();

		cache.AddOrReplace(persona, new CredentialWithToken { Token = SemanticString<CredentialToken>.Create("abc") });

		Assert.IsTrue(cache.TryGet(persona, out Credential? credential));
		Assert.AreEqual("abc", ((CredentialWithToken)credential!).Token.ToString());
	}
}
