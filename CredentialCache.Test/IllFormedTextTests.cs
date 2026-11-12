// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;

/// <summary>
/// Tests that a credential holding ill-formed UTF-16 (a lone surrogate) is refused when saved,
/// rather than persisted with U+FFFD in its place and read back after a restart as a different
/// secret.
/// </summary>
[TestClass]
public class IllFormedTextTests
{
	public sealed class CredentialWithPlainStrings : Credential
	{
		public string Value { get; init; } = "";

		public IReadOnlyList<string> Values { get; init; } = [];
	}

	[ClassInitialize]
	public static void RegisterType(TestContext context)
	{
		_ = context;
		CredentialSerialization.RegisterDerivedType<CredentialWithPlainStrings>("Test.CredentialWithPlainStrings");
	}

	private static CredentialWithToken TokenOf(string value) =>
		new() { Token = SemanticString<CredentialToken>.Create(value) };

	/// <summary>
	/// Built in code: an attribute stores its string arguments as UTF-8, so a lone surrogate in a
	/// <c>[DataRow]</c> would already be U+FFFD by the time the test ran.
	/// </summary>
	private static readonly string[] IllFormedTokens = ["abc\uD800def", "abc\uDC00def", "\uD83D", "x\uDE00\uD83D"];

	[TestMethod]
	public void SerializeRefusesALoneSurrogateInASemanticString()
	{
		foreach (string token in IllFormedTokens)
		{
			ArgumentException fromBytes = Assert.ThrowsExactly<ArgumentException>(() => CredentialSerialization.Serialize(TokenOf(token)));
			Assert.ThrowsExactly<ArgumentException>(() => CredentialSerialization.SerializeToString(TokenOf(token)));
			Assert.DoesNotContain(token, fromBytes.Message);
		}
	}

	[TestMethod]
	public void SerializeRefusesALoneSurrogateInAPlainStringOrAListOfThem()
	{
		Assert.ThrowsExactly<ArgumentException>(
			() => CredentialSerialization.Serialize(new CredentialWithPlainStrings { Value = "a\uD800" }));
		Assert.ThrowsExactly<ArgumentException>(
			() => CredentialSerialization.Serialize(new CredentialWithPlainStrings { Values = ["ok", "\uDFFFb"] }));
	}

	[TestMethod]
	public void AddOrReplaceRefusesALoneSurrogateAndStoresNothingInASerializingStore()
	{
		RawBlobCredentialStore store = new();
		using CredentialCache cache = new(store);
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();

		Assert.ThrowsExactly<ArgumentException>(() => cache.AddOrReplace(persona, TokenOf("abc\uD800def")));

		Assert.IsEmpty(store.Blobs);
		Assert.IsFalse(cache.TryGet(persona, out _));
	}

	[TestMethod]
	public void AddOrReplaceAndSaveRefuseALoneSurrogateInTheInMemoryStore()
	{
		InMemoryCredentialStore store = new();
		using CredentialCache cache = new(store);
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();

		Assert.ThrowsExactly<ArgumentException>(() => cache.AddOrReplace(persona, TokenOf("abc\uD800def")));
		Assert.ThrowsExactly<ArgumentException>(() => store.Save(persona, TokenOf("abc\uD800def")));

		Assert.IsEmpty(store.EnumerateKeys());
		Assert.IsFalse(cache.TryGet(persona, out _));
	}

	[TestMethod]
	public void SupplementaryCharactersStillSaveAndLoadUnchanged()
	{
		const string token = "key-\U0001F600-\U00020000-end";
		RawBlobCredentialStore store = new();
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();
		using (CredentialCache writer = new(store))
		{
			writer.AddOrReplace(persona, TokenOf(token));
		}

		using CredentialCache reader = new(store);
		Assert.IsTrue(reader.TryGet(persona, out Credential? loaded));
		Assert.AreEqual(token, Assert.IsInstanceOfType<CredentialWithToken>(loaded).Token.WeakString);

		CredentialWithPlainStrings plain = Assert.IsInstanceOfType<CredentialWithPlainStrings>(
			CredentialSerialization.Deserialize(CredentialSerialization.Serialize(new CredentialWithPlainStrings { Value = token, Values = [token] })));
		Assert.AreEqual(token, plain.Value);
		Assert.AreEqual(token, plain.Values.Single());
	}
}
