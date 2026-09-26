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
	public void NulTerminatedCopyOfAppendsTheTerminator()
	{
		byte[] blob = SerializedCredential();

		using NativeSecretBuffer buffer = NativeSecretBuffer.NulTerminatedCopyOf(blob);

		Assert.AreEqual(blob.Length + 1, buffer.Length, "The terminator has to be part of the allocation.");
		Assert.AreSequenceEqual([.. blob, (byte)0], ReadUnmanaged(buffer));
	}

	[TestMethod]
	public void NulTerminatedCopyOfZeroesTheWholeAllocationIncludingTheTerminator()
	{
		byte[] blob = SerializedCredential();
		using NativeSecretBuffer buffer = NativeSecretBuffer.NulTerminatedCopyOf(blob);

		buffer.Zero();

		// Length covers the terminator, so a scrub that used the payload length would
		// leave the last byte alone. Read back before Dispose, as above.
		Assert.AreSequenceEqual(new byte[blob.Length + 1], ReadUnmanaged(buffer));
	}

	[TestMethod]
	public void NulTerminatedCopyOfRejectsASourceCarryingItsOwnNul()
	{
		// A native C-string API would read a truncated secret and store it, which fails
		// silently at save time and surfaces as an unparseable blob on the next load.
		byte[] blob = [.. "{\"x\":1}"u8, 0, .. "tail"u8];

		ArgumentException thrown = Assert.ThrowsExactly<ArgumentException>(
			() => NativeSecretBuffer.NulTerminatedCopyOf(blob));

		Assert.AreEqual("source", thrown.ParamName);
	}

	[TestMethod]
	public void NulTerminatedCopyOfHandlesAnEmptySource()
	{
		using NativeSecretBuffer buffer = NativeSecretBuffer.NulTerminatedCopyOf([]);

		Assert.AreEqual(1, buffer.Length);
		Assert.AreSequenceEqual(new byte[] { 0 }, ReadUnmanaged(buffer));
	}

	[TestMethod]
	public void ReadNulTerminatedCopiesUpToTheTerminatorOnly()
	{
		byte[] blob = SerializedCredential();
		// Trailing bytes past the terminator stand in for whatever else the native
		// allocation happens to hold; none of it is part of the secret.
		using NativeSecretBuffer stored = NativeSecretBuffer.CopyOf([.. blob, 0, .. "trailing"u8]);

		byte[] read = NativeSecretBuffer.ReadNulTerminated(stored.Pointer);

		Assert.AreSequenceEqual(blob, read);
	}

	[TestMethod]
	public void ReadNulTerminatedRoundTripsACredentialWithoutAManagedString()
	{
		// The exact composition a C-string store performs: read the bytes, then scrub
		// the managed copy. Nothing in between is a string.
		using NativeSecretBuffer stored = NativeSecretBuffer.NulTerminatedCopyOf(SerializedCredential());

		byte[] read = NativeSecretBuffer.ReadNulTerminated(stored.Pointer);
		Credential? credential = CredentialSerialization.DeserializeAndScrub(read);

		CredentialWithToken? typed = credential as CredentialWithToken;
		Assert.IsNotNull(typed);
		Assert.AreEqual("plaintext-token-to-scrub", typed!.Token.ToString());
		Assert.AreSequenceEqual(new byte[read.Length], read, "The managed copy must be zeroed once deserialized.");
	}

	[TestMethod]
	public void ReadNulTerminatedReturnsEmptyForAnEmptyStringAndForNull()
	{
		using NativeSecretBuffer empty = NativeSecretBuffer.CopyOf([0]);

		Assert.IsEmpty(NativeSecretBuffer.ReadNulTerminated(empty.Pointer));
		Assert.IsEmpty(NativeSecretBuffer.ReadNulTerminated(IntPtr.Zero));
	}

	[TestMethod]
	public void OfCredentialProducesTheSerializedCredentialNulTerminated()
	{
		using NativeSecretBuffer buffer = NativeSecretBuffer.OfCredential(new CredentialWithToken
		{
			Token = SemanticString<CredentialToken>.Create("plaintext-token-to-scrub"),
		});

		// Byte-for-byte what a C-string native API should receive: the same JSON the other two
		// stores hand over as a pointer and a length, plus the terminator.
		Assert.AreSequenceEqual([.. SerializedCredential(), (byte)0], ReadUnmanaged(buffer));
	}

	[TestMethod]
	public void OfCredentialAndReadCredentialRoundTripACredential()
	{
		using NativeSecretBuffer buffer = NativeSecretBuffer.OfCredential(new CredentialWithToken
		{
			Token = SemanticString<CredentialToken>.Create("plaintext-token-to-scrub"),
		});

		Credential? credential = NativeSecretBuffer.ReadCredential(buffer.Pointer);

		CredentialWithToken? typed = credential as CredentialWithToken;
		Assert.IsNotNull(typed);
		Assert.AreEqual("plaintext-token-to-scrub", typed!.Token.ToString());
	}

	[TestMethod]
	public void OfCredentialRejectsANullCredential() =>
		Assert.ThrowsExactly<ArgumentNullException>(() => NativeSecretBuffer.OfCredential(null!));

	[TestMethod]
	public void ReadCredentialReturnsNullWhereThereIsNoCredentialToRead()
	{
		using NativeSecretBuffer empty = NativeSecretBuffer.CopyOf([0]);
		using NativeSecretBuffer garbage = NativeSecretBuffer.NulTerminatedCopyOf([.. "{ not a credential"u8]);

		// A store treats null as "nothing stored for this persona", so all three of these have to
		// answer null rather than throwing: no entry, an empty entry, and an entry that is not
		// parseable as a credential.
		Assert.IsNull(NativeSecretBuffer.ReadCredential(IntPtr.Zero));
		Assert.IsNull(NativeSecretBuffer.ReadCredential(empty.Pointer));
		Assert.IsNull(NativeSecretBuffer.ReadCredential(garbage.Pointer));
	}

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
