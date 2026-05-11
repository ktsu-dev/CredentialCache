// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache.Storage;

using System.Text.Json;
using ktsu.RoundTripStringJsonConverter;

/// <summary>
/// Serializes <see cref="Credential"/> instances to and from UTF-8 JSON. Custom
/// <see cref="ICredentialStore"/> implementations should round-trip credentials
/// through these helpers so polymorphic <see cref="Credential"/> subclasses are
/// preserved.
/// </summary>
public static class CredentialSerialization
{
	private static readonly JsonSerializerOptions Options = BuildOptions();

	private static JsonSerializerOptions BuildOptions()
	{
		JsonSerializerOptions options = new()
		{
			WriteIndented = false,
		};
		// Persuade System.Text.Json to use the SemanticString factory methods (Create / FromString)
		// rather than treating SemanticString<T> as an IEnumerable<char> collection.
		options.Converters.Add(new RoundTripStringJsonConverterFactory());
		return options;
	}

	/// <summary>
	/// Serializes the credential to a UTF-8 JSON byte array.
	/// </summary>
	public static byte[] Serialize(Credential credential) =>
		JsonSerializer.SerializeToUtf8Bytes(credential, Options);

	/// <summary>
	/// Serializes the credential to a UTF-8 JSON string.
	/// </summary>
	public static string SerializeToString(Credential credential) =>
		JsonSerializer.Serialize(credential, Options);

	/// <summary>
	/// Deserializes a credential from a UTF-8 JSON byte array. Returns <c>null</c> if the bytes
	/// do not represent a known credential.
	/// </summary>
	public static Credential? Deserialize(byte[] utf8Json)
	{
		if (utf8Json is null || utf8Json.Length == 0)
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<Credential>(utf8Json, Options);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>
	/// Deserializes a credential from a UTF-8 JSON string. Returns <c>null</c> if the value
	/// does not represent a known credential.
	/// </summary>
	public static Credential? DeserializeFromString(string utf8Json)
	{
		if (string.IsNullOrEmpty(utf8Json))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<Credential>(utf8Json, Options);
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
