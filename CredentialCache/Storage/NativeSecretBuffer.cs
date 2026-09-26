// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Storage;

using System.Runtime.InteropServices;

/// <summary>
/// An unmanaged copy of a plaintext credential blob that is overwritten with zeros
/// before the memory is released.
/// </summary>
/// <remarks>
/// <see cref="Marshal.FreeHGlobal(IntPtr)"/> hands memory back to the process heap
/// without scrubbing it, so plaintext passed to a native credential API survives in
/// the process image until that allocation happens to be reused - long enough to be
/// recovered by a memory scanner, or to land in a crash dump, hibernation file, or
/// page file. Owning the copy through this type zeroes the bytes first, on every path
/// out of the caller.
/// </remarks>
internal sealed class NativeSecretBuffer : IDisposable
{
	private NativeSecretBuffer(IntPtr pointer, int length)
	{
		Pointer = pointer;
		Length = length;
	}

	/// <summary>
	/// Gets the number of bytes copied into unmanaged memory.
	/// </summary>
	internal int Length { get; }

	/// <summary>
	/// Gets a pointer to the unmanaged copy, or <see cref="IntPtr.Zero"/> once the
	/// buffer has been disposed.
	/// </summary>
	internal IntPtr Pointer { get; private set; }

	/// <summary>
	/// Copies <paramref name="source"/> into newly allocated unmanaged memory.
	/// </summary>
	/// <param name="source">The plaintext bytes to copy.</param>
	/// <returns>A buffer owning the unmanaged copy, which the caller must dispose.</returns>
	internal static NativeSecretBuffer CopyOf(byte[] source)
	{
		ArgumentNullException.ThrowIfNull(source);

		// AllocHGlobal(0) is implementation defined, so keep at least one byte and the
		// pointer handed to native code is always valid.
		IntPtr pointer = Marshal.AllocHGlobal(Math.Max(source.Length, 1));
		try
		{
			Marshal.Copy(source, 0, pointer, source.Length);
		}
		catch
		{
			Marshal.FreeHGlobal(pointer);
			throw;
		}

		return new NativeSecretBuffer(pointer, source.Length);
	}

	/// <summary>
	/// Copies <paramref name="source"/> into newly allocated unmanaged memory followed by a
	/// single nul byte, for a native API that takes a C string rather than a pointer and a
	/// length.
	/// </summary>
	/// <param name="source">The plaintext bytes to copy. Must contain no nul byte of its own.</param>
	/// <returns>A buffer owning the unmanaged copy, which the caller must dispose.</returns>
	/// <remarks>
	/// <see cref="Length"/> covers the terminator, so <see cref="Zero"/> scrubs the whole
	/// allocation. Marshalling a managed string would be the obvious alternative and is the
	/// thing to avoid: the runtime frees the native copy it makes without scrubbing it, and
	/// the immutable managed string it came from cannot be scrubbed at all.
	/// </remarks>
	internal static NativeSecretBuffer NulTerminatedCopyOf(byte[] source)
	{
		ArgumentNullException.ThrowIfNull(source);

		if (Array.IndexOf(source, (byte)0) >= 0)
		{
			throw new ArgumentException(
				"A nul-terminated copy cannot carry a nul byte of its own; the native call would read a truncated secret.",
				nameof(source));
		}

		byte[] terminated = new byte[source.Length + 1];
		try
		{
			Buffer.BlockCopy(source, 0, terminated, 0, source.Length);
			return CopyOf(terminated);
		}
		finally
		{
			// terminated is a second plaintext copy on the managed heap and has served its
			// purpose by here, whether CopyOf succeeded or threw.
			CredentialSerialization.Zero(terminated);
		}
	}

	/// <summary>
	/// Serializes <paramref name="credential"/> and copies it into unmanaged memory as a
	/// nul-terminated UTF-8 JSON string, for a native API that takes a C string.
	/// </summary>
	/// <param name="credential">The credential to persist.</param>
	/// <returns>A buffer owning the unmanaged copy, which the caller must dispose.</returns>
	/// <remarks>
	/// Both managed copies made along the way — the serialized bytes and the nul-terminated
	/// array — are zeroed before this returns, on the throwing path as well. Owning the whole
	/// sequence here rather than in each store is what keeps it exercised by tests: a store's
	/// own body only runs on its own operating system.
	/// </remarks>
	internal static NativeSecretBuffer OfCredential(Credential credential)
	{
		ArgumentNullException.ThrowIfNull(credential);

		byte[] blob = CredentialSerialization.Serialize(credential);
		try
		{
			return NulTerminatedCopyOf(blob);
		}
		finally
		{
			CredentialSerialization.Zero(blob);
		}
	}

	/// <summary>
	/// Reads the nul-terminated UTF-8 JSON at <paramref name="pointer"/> and deserializes it,
	/// scrubbing the managed copy it read.
	/// </summary>
	/// <param name="pointer">A pointer to a nul-terminated plaintext credential blob.</param>
	/// <returns>
	/// The credential, or <see langword="null"/> when <paramref name="pointer"/> addresses
	/// nothing, an empty string, or bytes that are not a known credential.
	/// </returns>
	/// <remarks>
	/// The counterpart of <see cref="OfCredential"/>, and the read path a store whose native API
	/// returns a C string should use. Nothing here is ever a managed <see cref="string"/>: one
	/// would hold the plaintext until the GC happened to collect it and could not be scrubbed
	/// at all.
	/// </remarks>
	internal static Credential? ReadCredential(IntPtr pointer)
	{
		byte[] blob = ReadNulTerminated(pointer);
		return blob.Length == 0 ? null : CredentialSerialization.DeserializeAndScrub(blob);
	}

	/// <summary>
	/// Copies the nul-terminated bytes at <paramref name="pointer"/> into a managed array,
	/// excluding the terminator.
	/// </summary>
	/// <param name="pointer">A pointer to nul-terminated plaintext in unmanaged memory.</param>
	/// <returns>
	/// The bytes before the first nul, or an empty array when <paramref name="pointer"/> is
	/// <see cref="IntPtr.Zero"/> or addresses an empty string.
	/// </returns>
	/// <remarks>
	/// The caller owns the returned array and is expected to hand it to
	/// <see cref="CredentialSerialization.DeserializeAndScrub(byte[])"/>, which scrubs it.
	/// This exists so a store whose native API returns a C string can take the same
	/// byte-array-and-scrub path as one that returns a pointer and a length, instead of
	/// going through <see cref="Marshal.PtrToStringUTF8(IntPtr)"/> and stranding the
	/// plaintext in an immutable managed string.
	/// </remarks>
	internal static byte[] ReadNulTerminated(IntPtr pointer)
	{
		if (pointer == IntPtr.Zero)
		{
			return [];
		}

		int length = 0;
		while (Marshal.ReadByte(pointer, length) != 0)
		{
			length++;
		}

		if (length == 0)
		{
			return [];
		}

		byte[] copy = new byte[length];
		Marshal.Copy(pointer, copy, 0, length);
		return copy;
	}

	/// <summary>
	/// Overwrites the unmanaged copy with zeros while the memory is still allocated.
	/// Idempotent, and a no-op once the buffer has been disposed.
	/// </summary>
	internal void Zero()
	{
		if (Pointer == IntPtr.Zero || Length == 0)
		{
			return;
		}

		// Marshal.Copy into unmanaged memory is an opaque interop call, so unlike a
		// managed Array.Clear the runtime cannot elide it as a dead store. The source
		// is a fresh zero-filled array, which holds no secret of its own.
		Marshal.Copy(new byte[Length], 0, Pointer, Length);
	}

	/// <summary>
	/// Zeroes the unmanaged copy and then releases it.
	/// </summary>
	public void Dispose()
	{
		if (Pointer == IntPtr.Zero)
		{
			return;
		}

		Zero();
		Marshal.FreeHGlobal(Pointer);
		Pointer = IntPtr.Zero;
	}
}
