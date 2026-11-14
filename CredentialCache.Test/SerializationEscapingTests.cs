// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using System.Text;
using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;

/// <summary>
/// Tests that serialized credentials keep their characters as raw UTF-8 rather than
/// <c>\uXXXX</c> escapes, which would grow each one to six bytes and push secrets over
/// size-capped stores such as Windows Credential Manager's 2560-byte blob.
/// </summary>
[TestClass]
public class SerializationEscapingTests
{
	private const string Username = "пользователь";
	private const string Password = "a+b<c>d&e'f密码";

	[TestMethod]
	public void SerializeWritesPlusAndNonAsciiCharactersAsRawUtf8()
	{
		CredentialWithUsernamePassword credential = new()
		{
			Username = SemanticString<CredentialUsername>.Create(Username),
			Password = SemanticString<CredentialPassword>.Create(Password),
		};

		string json = Encoding.UTF8.GetString(CredentialSerialization.Serialize(credential));

		Assert.DoesNotContain("\\u", json);
		Assert.Contains(Username, json);
		Assert.Contains(Password, json);
	}

	[TestMethod]
	public void SerializeToStringWritesPlusAndNonAsciiCharactersAsRawUtf8()
	{
		CredentialWithToken credential = new()
		{
			Token = SemanticString<CredentialToken>.Create(Password),
		};

		string json = CredentialSerialization.SerializeToString(credential);

		Assert.DoesNotContain("\\u", json);
		Assert.Contains(Password, json);
	}

	[TestMethod]
	public void SerializedBase64TokenIsNoLargerThanItsCharactersPlusTheEnvelope()
	{
		string token = Convert.ToBase64String([.. Enumerable.Range(0, 1800).Select(i => (byte)(i * 251))]);
		Assert.Contains("+", token);
		CredentialWithToken credential = new()
		{
			Token = SemanticString<CredentialToken>.Create(token),
		};

		byte[] blob = CredentialSerialization.Serialize(credential);

		Assert.IsLessThan(token.Length + 64, blob.Length);
	}

	[TestMethod]
	public void PreviouslyEscapedBlobStillDeserializes()
	{
		const string escaped =
			"{\"$type\":\"CredentialWithUsernamePassword\","
			+ "\"Username\":\"\\u043F\\u043E\\u043B\\u044C\\u0437\\u043E\\u0432\\u0430\\u0442\\u0435\\u043B\\u044C\","
			+ "\"Password\":\"a\\u002Bb\\u003Cc\\u003Ed\\u0026e\\u0027f\\u5BC6\\u7801\"}";

		Credential? fromBytes = CredentialSerialization.Deserialize(Encoding.UTF8.GetBytes(escaped));
		Credential? fromString = CredentialSerialization.DeserializeFromString(escaped);

		foreach (Credential? credential in new[] { fromBytes, fromString })
		{
			CredentialWithUsernamePassword loaded = Assert.IsInstanceOfType<CredentialWithUsernamePassword>(credential);
			Assert.AreEqual(Username, loaded.Username.WeakString);
			Assert.AreEqual(Password, loaded.Password.WeakString);
		}
	}

	[TestMethod]
	public void SupplementaryCharactersAreWrittenAsRawUtf8AndFitWindowsBlobCap()
	{
		// 210 emoji are 840 UTF-8 bytes. Escaped as surrogate pairs they were 2520 bytes, which
		// with the envelope pushed the blob over Windows Credential Manager's 2560-byte cap.
		string token = string.Concat(Enumerable.Repeat("\U0001F600", 210));
		CredentialWithToken credential = new()
		{
			Token = SemanticString<CredentialToken>.Create(token),
		};

		byte[] blob = CredentialSerialization.Serialize(credential);
		string json = Encoding.UTF8.GetString(blob);

		Assert.DoesNotContain("\\u", json);
		Assert.IsLessThan(Encoding.UTF8.GetByteCount(token) + 64, blob.Length);
		using NativeSecretBuffer buffer = NativeSecretBuffer.CopyWithinLimit(blob, 5 * 512, "test");
		CredentialWithToken loaded = Assert.IsInstanceOfType<CredentialWithToken>(CredentialSerialization.DeserializeFromString(json));
		Assert.AreEqual(token, loaded.Token.WeakString);
	}

	[TestMethod]
	public void SerializeToStringWritesSupplementaryCharactersRaw()
	{
		const string token = "a\U00020000b\U0001F680c";
		CredentialWithToken credential = new()
		{
			Token = SemanticString<CredentialToken>.Create(token),
		};

		string json = CredentialSerialization.SerializeToString(credential);

		Assert.DoesNotContain("\\u", json);
		Assert.Contains(token, json);
		CredentialWithToken loaded = Assert.IsInstanceOfType<CredentialWithToken>(CredentialSerialization.DeserializeFromString(json));
		Assert.AreEqual(token, loaded.Token.WeakString);
	}

	[TestMethod]
	public void QuotesBackslashesAndControlCharactersAreStillEscaped()
	{
		const string token = "q\"b\\n\nt\tz\u0001\u001F\U0001F600";
		CredentialWithToken credential = new()
		{
			Token = SemanticString<CredentialToken>.Create(token),
		};

		string json = Encoding.UTF8.GetString(CredentialSerialization.Serialize(credential));

		Assert.Contains("q\\\"b\\\\n\\nt\\tz\\u0001\\u001F\U0001F600", json);
		CredentialWithToken loaded = Assert.IsInstanceOfType<CredentialWithToken>(CredentialSerialization.Deserialize(Encoding.UTF8.GetBytes(json)));
		Assert.AreEqual(token, loaded.Token.WeakString);
	}

	[TestMethod]
	public void PreviouslyEscapedSurrogatePairStillDeserializes()
	{
		const string escaped = "{\"$type\":\"CredentialWithToken\",\"Token\":\"\\uD83D\\uDE00\"}";

		CredentialWithToken loaded = Assert.IsInstanceOfType<CredentialWithToken>(CredentialSerialization.DeserializeFromString(escaped));

		Assert.AreEqual("\U0001F600", loaded.Token.WeakString);
	}
}
