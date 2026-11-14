// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Storage;

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ktsu.RoundTripStringJsonConverter;

/// <summary>
/// Serializes <see cref="Credential"/> instances to and from UTF-8 JSON. Custom
/// <see cref="ICredentialStore"/> implementations should round-trip credentials
/// through these helpers so polymorphic <see cref="Credential"/> subclasses are
/// preserved.
/// </summary>
/// <remarks>
/// The library's own subclasses are known up front. A subclass defined anywhere else has to be
/// registered with <see cref="RegisterDerivedType{T}(string)"/> before it is saved or loaded.
/// </remarks>
public static class CredentialSerialization
{
	/// <summary>
	/// The subclasses <see cref="Credential"/> itself declares, keyed by type.
	/// </summary>
	private static readonly Dictionary<Type, string> BuiltInTypes = typeof(Credential)
		.GetCustomAttributes<JsonDerivedTypeAttribute>()
		.ToDictionary(attribute => attribute.DerivedType, attribute => (string)attribute.TypeDiscriminator!);

	private static readonly Lock RegistrationLock = new();

	/// <summary>
	/// The subclasses registered through <see cref="RegisterDerivedType{T}(string)"/>. Replaced
	/// rather than mutated, under <see cref="RegistrationLock"/>, so a reader always sees a
	/// complete set.
	/// </summary>
	private static volatile Dictionary<Type, string> registeredTypes = [];

	/// <summary>
	/// The options every helper serializes with. Rebuilt on each registration rather than
	/// extended, because System.Text.Json fixes an options instance's polymorphic type set the
	/// first time it is used, so a type added afterwards would otherwise never be seen.
	/// </summary>
	private static volatile JsonSerializerOptions options = BuildOptions(registeredTypes);

	private static JsonSerializerOptions BuildOptions(Dictionary<Type, string> derivedTypes)
	{
		DefaultJsonTypeInfoResolver resolver = new();
		resolver.Modifiers.Add(typeInfo =>
		{
			if (typeInfo.Type == typeof(Credential) && typeInfo.PolymorphismOptions is not null)
			{
				foreach ((Type type, string discriminator) in derivedTypes)
				{
					typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, discriminator));
				}
			}
		});

		JsonSerializerOptions built = new()
		{
			WriteIndented = false,
			TypeInfoResolver = resolver,
			// The default encoder escapes '+', '<', '>', '&', '\'' and every non-ASCII character
			// as a six-byte \uXXXX, which pushes secrets over size-capped stores such as Windows
			// Credential Manager's 2560-byte blob. The payload is never embedded in HTML or
			// script, so HTML-safe escaping buys nothing, and the reader accepts both forms.
			// UnsafeRelaxedJsonEscaping alone still writes an emoji as a twelve-byte surrogate
			// pair escape, so this encoder writes supplementary characters raw as well.
			Encoder = CredentialJsonEncoder.Instance,
		};
		// Persuade System.Text.Json to use the SemanticString factory methods (Create / FromString)
		// rather than treating SemanticString<T> as an IEnumerable<char> collection.
		// Wrapped so a string or SemanticString holding a lone surrogate is refused rather than
		// silently stored with U+FFFD in its place.
		built.Converters.Add(new WellFormedTextConverterFactory(new RoundTripStringJsonConverterFactory()));
		// Refuse a JSON null for a non-nullable property, in both directions. Otherwise a stored
		// "Token":null reads back as a found credential whose Token throws on first use, and a
		// credential with a null! property is persisted and round-trips.
		built.RespectNullableAnnotations = true;
		return built;
	}

	/// <summary>
	/// Registers a <see cref="Credential"/> subclass defined outside this library, so the
	/// platform stores can save and load it.
	/// </summary>
	/// <typeparam name="T">The subclass to register.</typeparam>
	/// <param name="typeDiscriminator">
	/// The name written into the stored JSON to identify <typeparamref name="T"/>. It is what
	/// identifies the type when a stored credential is read back, so it must stay the same for
	/// as long as credentials saved with it exist.
	/// </param>
	/// <remarks>
	/// Register each type once, at start-up, before the first credential of that type is saved
	/// or loaded. Registering the same type with the same discriminator again does nothing.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="typeDiscriminator"/> is empty or whitespace, <typeparamref name="T"/> is
	/// abstract or is one of the library's own subclasses, <typeparamref name="T"/> is already
	/// registered under a different discriminator, or the discriminator already names another
	/// type.
	/// </exception>
	public static void RegisterDerivedType<T>(string typeDiscriminator) where T : Credential
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(typeDiscriminator);

		Type type = typeof(T);
		if (type.IsAbstract)
		{
			throw new ArgumentException($"{type} is abstract, so no stored credential can be one.");
		}

		if (BuiltInTypes.ContainsKey(type))
		{
			throw new ArgumentException($"{type} is one of the library's own credential types and is already known.");
		}

		lock (RegistrationLock)
		{
			if (registeredTypes.TryGetValue(type, out string? existing))
			{
				if (existing == typeDiscriminator)
				{
					return;
				}

				throw new ArgumentException($"{type} is already registered with the discriminator \"{existing}\".");
			}

			Type? owner = BuiltInTypes.Concat(registeredTypes)
				.FirstOrDefault(entry => entry.Value == typeDiscriminator).Key;
			if (owner is not null)
			{
				throw new ArgumentException($"The discriminator \"{typeDiscriminator}\" already identifies {owner}.", nameof(typeDiscriminator));
			}

			Dictionary<Type, string> updated = new(registeredTypes) { [type] = typeDiscriminator };
			options = BuildOptions(updated);
			registeredTypes = updated;
		}
	}

	/// <summary>
	/// Whether credentials of <paramref name="type"/> can be serialized, either because the
	/// library declares it or because it was registered.
	/// </summary>
	internal static bool IsKnownType(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return BuiltInTypes.ContainsKey(type) || registeredTypes.ContainsKey(type);
	}

	/// <summary>
	/// Throws <see cref="CredentialStoreException"/> naming <paramref name="credential"/>'s type
	/// when it is not one that can be serialized.
	/// </summary>
	internal static void ThrowIfUnknownType(Credential credential)
	{
		ArgumentNullException.ThrowIfNull(credential);

		Type type = credential.GetType();
		if (!IsKnownType(type))
		{
			throw new CredentialStoreException(
				$"{type} is not a known credential type, so it cannot be stored. Register it first with "
				+ $"{nameof(CredentialSerialization)}.{nameof(RegisterDerivedType)}.");
		}
	}

	/// <summary>
	/// Throws what <see cref="Serialize(Credential)"/> would throw for <paramref name="credential"/>,
	/// without keeping its serialized form, so a store that never serializes refuses the same
	/// credentials as one that does.
	/// </summary>
	/// <exception cref="CredentialStoreException">
	/// The credential's type is neither one of the library's own nor registered with
	/// <see cref="RegisterDerivedType{T}(string)"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="credential"/> has a non-nullable property set to <c>null</c>, so it could
	/// not be read back, or a value holding a lone surrogate, which would be stored as U+FFFD.
	/// </exception>
	internal static void ThrowIfNotSerializable(Credential credential) => Zero(Serialize(credential));

	/// <summary>
	/// Serializes the credential to a UTF-8 JSON byte array.
	/// </summary>
	/// <exception cref="CredentialStoreException">
	/// The credential's type is neither one of the library's own nor registered with
	/// <see cref="RegisterDerivedType{T}(string)"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="credential"/> has a non-nullable property set to <c>null</c>, so it could
	/// not be read back, or a value holding a lone surrogate, which would be stored as U+FFFD.
	/// </exception>
	public static byte[] Serialize(Credential credential)
	{
		ThrowIfUnknownType(credential);

		try
		{
			return JsonSerializer.SerializeToUtf8Bytes(credential, options);
		}
		catch (JsonException exception)
		{
			throw NotSerializable(credential, exception);
		}
	}

	/// <summary>
	/// Serializes the credential to a UTF-8 JSON string.
	/// </summary>
	/// <exception cref="CredentialStoreException">
	/// The credential's type is neither one of the library's own nor registered with
	/// <see cref="RegisterDerivedType{T}(string)"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="credential"/> has a non-nullable property set to <c>null</c>, so it could
	/// not be read back, or a value holding a lone surrogate, which would be stored as U+FFFD.
	/// </exception>
	public static string SerializeToString(Credential credential)
	{
		ThrowIfUnknownType(credential);

		try
		{
			return JsonSerializer.Serialize(credential, options);
		}
		catch (JsonException exception)
		{
			throw NotSerializable(credential, exception);
		}
	}

	private static ArgumentException NotSerializable(Credential credential, JsonException exception) =>
		new($"{credential.GetType()} cannot be stored: {exception.Message}", nameof(credential), exception);

	/// <summary>
	/// Deserializes a credential from a UTF-8 JSON byte array. Returns <c>null</c> if the bytes
	/// do not represent a known credential, or hold a value its property's type refuses.
	/// </summary>
	public static Credential? Deserialize(byte[] utf8Json)
	{
		if (utf8Json is null || utf8Json.Length == 0)
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<Credential>(utf8Json, options);
		}
		catch (JsonException)
		{
			return null;
		}
		catch (NotSupportedException)
		{
			// Well-formed JSON with no $type discriminator is not a known credential either
			return null;
		}
		catch (ArgumentException)
		{
			// A value its SemanticString type refuses, because it was damaged, written by another
			// tool, or saved before the type's validation was tightened, is unreadable too
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
	/// does not represent a known credential, or holds a value its property's type refuses.
	/// </summary>
	public static Credential? DeserializeFromString(string utf8Json)
	{
		if (string.IsNullOrEmpty(utf8Json))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<Credential>(utf8Json, options);
		}
		catch (JsonException)
		{
			return null;
		}
		catch (NotSupportedException)
		{
			// Well-formed JSON with no $type discriminator is not a known credential either
			return null;
		}
		catch (ArgumentException)
		{
			// A value its SemanticString type refuses, because it was damaged, written by another
			// tool, or saved before the type's validation was tightened, is unreadable too
			return null;
		}
	}
}
