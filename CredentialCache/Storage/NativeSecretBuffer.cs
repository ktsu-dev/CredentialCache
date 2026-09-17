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
