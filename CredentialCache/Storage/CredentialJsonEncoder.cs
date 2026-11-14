// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Storage;

using System.Text.Encodings.Web;

/// <summary>
/// Escapes what <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> escapes in the Basic
/// Multilingual Plane, but writes a well-formed supplementary-plane character (an emoji, CJK
/// Extension B) as its raw four UTF-8 bytes.
/// </summary>
/// <remarks>
/// <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> only allows Basic Multilingual Plane
/// ranges, so it writes every supplementary character as a <c>\uXXXX\uXXXX</c> pair: twelve bytes
/// where UTF-8 needs four. That tripled such a secret's size against Windows Credential Manager's
/// 2560-byte blob cap. Quotes, backslashes, control characters and lone surrogates are still
/// escaped, and a reader accepts either form, so blobs written before this encoder still load.
/// </remarks>
internal sealed class CredentialJsonEncoder : JavaScriptEncoder
{
	private static readonly JavaScriptEncoder Relaxed = UnsafeRelaxedJsonEscaping;

	/// <summary>
	/// The single instance the serializer options use.
	/// </summary>
	public static CredentialJsonEncoder Instance { get; } = new();

	private CredentialJsonEncoder()
	{
	}

	/// <inheritdoc/>
	public override int MaxOutputCharactersPerInputCharacter => Relaxed.MaxOutputCharactersPerInputCharacter;

	/// <inheritdoc/>
	public override bool WillEncode(int unicodeScalar) =>
		unicodeScalar <= char.MaxValue && Relaxed.WillEncode(unicodeScalar);

	/// <inheritdoc/>
	public override unsafe int FindFirstCharacterToEncode(char* text, int textLength) =>
		FindFirstCharacterToEncode(new ReadOnlySpan<char>(text, textLength));

	/// <inheritdoc/>
	public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten) =>
		Relaxed.TryEncodeUnicodeScalar(unicodeScalar, buffer, bufferLength, out numberOfCharactersWritten);

	/// <summary>
	/// The index of the first character to escape in <paramref name="text"/>, or -1 when none
	/// needs to be. A high-low surrogate pair is left as it is; a lone surrogate is escaped.
	/// </summary>
	internal static int FindFirstCharacterToEncode(ReadOnlySpan<char> text)
	{
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
			{
				i++;
				continue;
			}

			if (char.IsSurrogate(c) || Relaxed.WillEncode(c))
			{
				return i;
			}
		}

		return -1;
	}
}
