// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Storage;

using System.Runtime.InteropServices;

/// <summary>
/// glib's <c>GError</c>: <c>{ guint32 domain; gint code; gchar *message; }</c>.
/// </summary>
/// <remarks>
/// Declared rather than read at a hand-computed offset. The message pointer follows two
/// 4-byte fields, so it sits at offset 8 on both 32-bit and 64-bit; an offset derived from
/// <see cref="IntPtr.Size"/> reads past the end of the struct on 64-bit.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct GError
{
	internal readonly uint Domain;
	internal readonly int Code;
	internal readonly IntPtr Message;

	/// <summary>
	/// Reads the message of the <c>GError</c> at <paramref name="error"/>.
	/// </summary>
	/// <param name="error">A non-null pointer to a <c>GError</c>.</param>
	/// <returns>The message, or <see langword="null"/> if the error has none.</returns>
	internal static string? ReadMessage(IntPtr error) =>
		Marshal.PtrToStringUTF8(Marshal.PtrToStructure<GError>(error).Message);
}
