// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Storage;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Refuses to write a string, or a value written as its string form, that is not well-formed
/// UTF-16, and otherwise defers to <paramref name="inner"/> for the types it handles.
/// </summary>
/// <remarks>
/// <see cref="Utf8JsonWriter"/> silently writes U+FFFD in place of a lone surrogate. For a credential
/// that would persist a different secret from the one the caller saved, and the cache would keep
/// returning the original until the process restarts. Refusing it at save time is the only point
/// where the caller can still do something about it.
/// </remarks>
/// <param name="inner">The factory for the types written as their string form.</param>
internal sealed class WellFormedTextConverterFactory(JsonConverterFactory inner) : JsonConverterFactory
{
	/// <inheritdoc/>
	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert == typeof(string) || inner.CanConvert(typeToConvert);

	/// <inheritdoc/>
	public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		if (typeToConvert == typeof(string))
		{
			return new WellFormedStringConverter();
		}

		JsonConverter? converter = inner.CreateConverter(typeToConvert, options);
		return converter is null
			? null
			: (JsonConverter)Activator.CreateInstance(typeof(WellFormedConverter<>).MakeGenericType(typeToConvert), converter)!;
	}

	/// <summary>
	/// Throws <see cref="JsonException"/> when <paramref name="value"/> holds a lone surrogate. The
	/// message does not quote the value, which is usually a secret.
	/// </summary>
	internal static void ThrowIfIllFormed(string? value)
	{
		if (value is not null && !IsWellFormed(value))
		{
			throw new JsonException(
				"A value contains a lone surrogate, which is not well-formed UTF-16. Saving it would store U+FFFD in its place, a different value from the one given.");
		}
	}

	/// <summary>
	/// Whether every surrogate in <paramref name="value"/> is half of a high-low pair.
	/// </summary>
	internal static bool IsWellFormed(ReadOnlySpan<char> value)
	{
		for (int i = 0; i < value.Length; i++)
		{
			char c = value[i];
			if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
			{
				i++;
			}
			else if (char.IsSurrogate(c))
			{
				return false;
			}
		}

		return true;
	}

	private sealed class WellFormedStringConverter : JsonConverter<string>
	{
		public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			reader.GetString();

		public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
		{
			ThrowIfIllFormed(value);
			writer.WriteStringValue(value);
		}

		public override string ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			reader.GetString()!;

		public override void WriteAsPropertyName(Utf8JsonWriter writer, [DisallowNull] string value, JsonSerializerOptions options)
		{
			ThrowIfIllFormed(value);
			writer.WritePropertyName(value);
		}
	}

	private sealed class WellFormedConverter<T>(JsonConverter<T> converter) : JsonConverter<T>
	{
		public override bool HandleNull => converter.HandleNull;

		public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			converter.Read(ref reader, typeToConvert, options);

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			ThrowIfIllFormed(value?.ToString());
			converter.Write(writer, value, options);
		}

		public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			converter.ReadAsPropertyName(ref reader, typeToConvert, options);

		public override void WriteAsPropertyName(Utf8JsonWriter writer, [DisallowNull] T value, JsonSerializerOptions options)
		{
			ThrowIfIllFormed(value.ToString());
			converter.WriteAsPropertyName(writer, value, options);
		}
	}
}
