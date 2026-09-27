// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using System.Collections.Concurrent;
using System.Text;
using ktsu.CredentialCache.Storage;

/// <summary>
/// Tests that a stored payload which is not a known credential reads back as "not found"
/// rather than throwing, whatever shape the JSON takes.
/// </summary>
[TestClass]
public class UnknownPayloadTests
{
	[TestMethod]
	[DataRow("{}")]
	[DataRow("{\"Token\":\"x\"}")]
	[DataRow("{\"$type\":\"Bogus\"}")]
	[DataRow("[]")]
	[DataRow("123")]
	public void DeserializeReturnsNullForJsonThatIsNotAKnownCredential(string json) =>
		Assert.IsNull(CredentialSerialization.Deserialize(Encoding.UTF8.GetBytes(json)));

	[TestMethod]
	[DataRow("{}")]
	[DataRow("{\"Token\":\"x\"}")]
	[DataRow("{\"$type\":\"Bogus\"}")]
	[DataRow("[]")]
	[DataRow("123")]
	public void DeserializeFromStringReturnsNullForJsonThatIsNotAKnownCredential(string json) =>
		Assert.IsNull(CredentialSerialization.DeserializeFromString(json));

	[TestMethod]
	[DataRow("{}")]
	[DataRow("{\"Token\":\"x\"}")]
	public void TryGetReturnsFalseForAStoredEntryWithoutATypeDiscriminator(string json)
	{
		RawBlobCredentialStore store = new();
		using CredentialCache cache = new(store);
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();
		store.Blobs[persona] = Encoding.UTF8.GetBytes(json);

		bool found = cache.TryGet(persona, out Credential? credential);

		Assert.IsFalse(found);
		Assert.IsNull(credential);
	}
}

/// <summary>
/// A store that holds raw bytes and reads them back the way the native stores do, so an entry
/// written by another tool, or damaged, can be planted directly.
/// </summary>
public sealed class RawBlobCredentialStore : ICredentialStore
{
	public ConcurrentDictionary<PersonaGUID, byte[]> Blobs { get; } = new();

	public string Name => "RawBlob";

	public bool TryLoad(PersonaGUID persona, out Credential? credential)
	{
		credential = Blobs.TryGetValue(persona, out byte[]? blob)
			? CredentialSerialization.DeserializeAndScrub([.. blob])
			: null;
		return credential is not null;
	}

	public void Save(PersonaGUID persona, Credential credential) =>
		Blobs[persona] = CredentialSerialization.Serialize(credential);

	public bool Remove(PersonaGUID persona) => Blobs.TryRemove(persona, out _);
}
