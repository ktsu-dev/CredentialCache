// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Storage;

using System.Security.Cryptography;
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
	/// Deserializes a credential from a UTF-8 JSON byte array and then overwrites
	/// <paramref name="utf8Json"/> with zeros, whether or not deserialization succeeds.
	/// </summary>
	/// <remarks>
	/// Platform stores copy the stored secret into a managed array in order to
	/// deserialize it. That array holds plaintext, and left alone it lingers on the
	/// managed heap - subject to GC promotion and compaction - until it is eventually
	/// collected, where it can still be read out of a crash dump. Store implementations
	/// should read through this helper rather than calling <see cref="Deserialize(byte[])"/>
	/// directly, so no plaintext copy outlives the call.
	/// </remarks>
	internal static Credential? DeserializeAndScrub(byte[] utf8Json)
	{
		if (utf8Json is null)
		{
			return null;
		}

		try
		{
			return Deserialize(utf8Json);
		}
		finally
		{
			Zero(utf8Json);
		}
	}

	/// <summary>
	/// Overwrites <paramref name="buffer"/> with zeros in a way the runtime cannot
	/// discard as a dead store.
	/// </summary>
	internal static void Zero(byte[] buffer)
	{
		if (buffer is null || buffer.Length == 0)
		{
			return;
		}

		CryptographicOperations.ZeroMemory(buffer);
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
