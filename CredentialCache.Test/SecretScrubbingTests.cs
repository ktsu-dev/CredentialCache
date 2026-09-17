// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using System.Runtime.InteropServices;
using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;

/// <summary>
/// Covers the two primitives the platform-native stores use to keep plaintext
/// credential bytes from outliving the call that read or wrote them:
/// <see cref="CredentialSerialization.DeserializeAndScrub(byte[])"/> for the managed
/// copy a store deserializes from, and <see cref="NativeSecretBuffer"/> for the
/// unmanaged copy a store hands to a native API.
///
/// These run on every platform. The native stores themselves are only reachable on
/// their own OS, so the scrubbing lives in these two shared primitives rather than
/// being re-implemented (and left untested) in each store.
/// </summary>
[TestClass]
public class SecretScrubbingTests
{
	private static byte[] SerializedCredential() =>
		CredentialSerialization.Serialize(new CredentialWithToken
		{
			Token = SemanticString<CredentialToken>.Create("plaintext-token-to-scrub"),
		});

	private static byte[] ReadUnmanaged(NativeSecretBuffer buffer)
	{
		byte[] copy = new byte[buffer.Length];
		Marshal.Copy(buffer.Pointer, copy, 0, buffer.Length);
		return copy;
	}

	[TestMethod]
	public void DeserializeAndScrubReturnsTheCredential()
	{
		byte[] blob = SerializedCredential();

		Credential? credential = CredentialSerialization.DeserializeAndScrub(blob);

		CredentialWithToken? typed = credential as CredentialWithToken;
		Assert.IsNotNull(typed);
		Assert.AreEqual("plaintext-token-to-scrub", typed!.Token.ToString());
	}

	[TestMethod]
	public void DeserializeAndScrubZeroesTheManagedCopy()
	{
		byte[] blob = SerializedCredential();
		Assert.AreNotSequenceEqual(new byte[blob.Length], blob, "Precondition: the blob starts out as plaintext.");

		_ = CredentialSerialization.DeserializeAndScrub(blob);

		Assert.AreSequenceEqual(new byte[blob.Length], blob,
			"The plaintext blob must be zeroed once it has been deserialized.");
	}

	[TestMethod]
	public void DeserializeAndScrubZeroesTheManagedCopyForUnparseableBytes()
	{
		// A blob that isn't a credential still came out of the platform store, so it
		// is still secret-bearing and must be scrubbed on the failure path too.
		byte[] blob = [.. "{ not a credential"u8];

		Credential? credential = CredentialSerialization.DeserializeAndScrub(blob);

		Assert.IsNull(credential);
		Assert.AreSequenceEqual(new byte[blob.Length], blob);
	}

	[TestMethod]
	public void NativeSecretBufferCopiesTheSourceBytes()
	{
		byte[] blob = SerializedCredential();

		using NativeSecretBuffer buffer = NativeSecretBuffer.CopyOf(blob);

		Assert.AreEqual(blob.Length, buffer.Length);
		Assert.AreNotEqual(IntPtr.Zero, buffer.Pointer);
		Assert.AreSequenceEqual(blob, ReadUnmanaged(buffer));
	}

	[TestMethod]
	public void NativeSecretBufferZeroOverwritesThePlaintextWhileStillAllocated()
	{
		byte[] blob = SerializedCredential();
		using NativeSecretBuffer buffer = NativeSecretBuffer.CopyOf(blob);
		Assert.AreSequenceEqual(blob, ReadUnmanaged(buffer), "Precondition: the unmanaged copy is plaintext.");

		buffer.Zero();

		// Read back before Dispose - reading freed memory would be undefined, so the
		// scrub has to be observable while the allocation is still live. This is the
		// exact ordering Dispose relies on: zero, then free.
		Assert.AreSequenceEqual(new byte[blob.Length], ReadUnmanaged(buffer),
			"The unmanaged copy must be zeroed before the memory is released.");
	}

	[TestMethod]
	public void NativeSecretBufferZeroIsIdempotent()
	{
		byte[] blob = SerializedCredential();
		using NativeSecretBuffer buffer = NativeSecretBuffer.CopyOf(blob);

		buffer.Zero();
		buffer.Zero();

		Assert.AreSequenceEqual(new byte[blob.Length], ReadUnmanaged(buffer));
	}

	[TestMethod]
	public void NativeSecretBufferDisposeReleasesThePointer()
	{
		NativeSecretBuffer buffer = NativeSecretBuffer.CopyOf(SerializedCredential());

		buffer.Dispose();

		Assert.AreEqual(IntPtr.Zero, buffer.Pointer, "A disposed buffer must not keep a dangling pointer.");
		Assert.AreEqual(0, buffer.Pointer.ToInt64());
	}

	[TestMethod]
	public void NativeSecretBufferDisposeIsIdempotent()
	{
		NativeSecretBuffer buffer = NativeSecretBuffer.CopyOf(SerializedCredential());

		buffer.Dispose();
		buffer.Dispose();

		Assert.AreEqual(IntPtr.Zero, buffer.Pointer);
	}

	[TestMethod]
	public void NativeSecretBufferZeroAfterDisposeDoesNotTouchFreedMemory()
	{
		NativeSecretBuffer buffer = NativeSecretBuffer.CopyOf(SerializedCredential());
		buffer.Dispose();

		// Must be a no-op rather than a write through a freed pointer.
		buffer.Zero();

		Assert.AreEqual(IntPtr.Zero, buffer.Pointer);
	}

	[TestMethod]
	public void NativeSecretBufferHandlesAnEmptySource()
	{
		using NativeSecretBuffer buffer = NativeSecretBuffer.CopyOf([]);

		Assert.AreEqual(0, buffer.Length);
		Assert.AreNotEqual(IntPtr.Zero, buffer.Pointer);
		buffer.Zero();
	}

	[TestMethod]
	public void NativeSecretBufferRejectsANullSource() =>
		Assert.ThrowsExactly<ArgumentNullException>(() => NativeSecretBuffer.CopyOf(null!));

	[TestMethod]
	public void ZeroOverwritesTheBuffer()
	{
		byte[] blob = SerializedCredential();

		CredentialSerialization.Zero(blob);

		Assert.AreSequenceEqual(new byte[blob.Length], blob);
	}

	[TestMethod]
	public void ZeroToleratesEmptyAndNullBuffers()
	{
		byte[] empty = [];

		// The guard clauses exist so a store can scrub whatever it has without
		// length- or null-checking first. Both calls returning rather than throwing
		// is the behaviour under test; an exception from either fails the test.
		CredentialSerialization.Zero(empty);
		CredentialSerialization.Zero(null!);

		Assert.IsEmpty(empty);
	}
}
