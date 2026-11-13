// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using System.Text;
using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;

/// <summary>
/// Tests that a stored entry whose value no longer passes its <see cref="SemanticString{T}"/>
/// validation reads back as "not found" rather than throwing, the way any other unreadable entry
/// does.
/// </summary>
[TestClass]
public class InvalidStoredValueTests
{
	[IsBase64]
	public sealed record class Base64Token : SemanticString<Base64Token> { }

	public sealed class CredentialWithBase64Token : Credential
	{
		public Base64Token Token { get; init; } = null!;
	}

	private const string InvalidJson = "{\"$type\":\"Test.CredentialWithBase64Token\",\"Token\":\"not base64!\"}";
	private const string ValidJson = "{\"$type\":\"Test.CredentialWithBase64Token\",\"Token\":\"aGk=\"}";

	[ClassInitialize]
	public static void RegisterType(TestContext context)
	{
		_ = context;
		CredentialSerialization.RegisterDerivedType<CredentialWithBase64Token>("Test.CredentialWithBase64Token");
	}

	[TestMethod]
	public void DeserializeReturnsNullForAValueThatFailsValidation() =>
		Assert.IsNull(CredentialSerialization.Deserialize(Encoding.UTF8.GetBytes(InvalidJson)));

	[TestMethod]
	public void DeserializeFromStringReturnsNullForAValueThatFailsValidation() =>
		Assert.IsNull(CredentialSerialization.DeserializeFromString(InvalidJson));

	[TestMethod]
	public void TryGetReturnsFalseForAStoredValueThatFailsValidation()
	{
		RawBlobCredentialStore store = new();
		using CredentialCache cache = new(store);
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();
		store.Blobs[persona] = Encoding.UTF8.GetBytes(InvalidJson);

		bool found = cache.TryGet(persona, out Credential? credential);

		Assert.IsFalse(found);
		Assert.IsNull(credential);
	}

	[TestMethod]
	public void AStoredValueThatPassesValidationStillReadsBack()
	{
		Credential? read = CredentialSerialization.Deserialize(Encoding.UTF8.GetBytes(ValidJson));

		CredentialWithBase64Token token = Assert.IsInstanceOfType<CredentialWithBase64Token>(read);
		Assert.AreEqual("aGk=", token.Token.ToString());
	}
}
