// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using System.Reflection;
using ktsu.CredentialCache.Storage;

/// <summary>
/// Pins every platform-native store to the scrubbing helpers rather than the
/// string-based ones.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SecretScrubbingTests"/> proves the scrubbing primitives work. Nothing
/// proved that each store actually calls them, and that gap is what this class closes:
/// the Windows store was moved onto them in #144 and the Linux store was left behind
/// until #160, because a store can only be exercised on its own operating system and
/// two of the three are therefore unreachable from any single CI leg.
/// </para>
/// <para>
/// Whether a secret was routed through an immutable managed <see cref="string"/> is not
/// observable at runtime — the plaintext's problem is precisely that it lingers where
/// nothing can see or reach it — so this inspects which helper each store is compiled
/// against instead. The "must call" assertions double as a check on the IL scan itself:
/// a scan that found nothing would fail them rather than quietly passing the "must not
/// call" half.
/// </para>
/// </remarks>
[TestClass]
public class NativeStoreScrubbingWiringTests
{
	private static readonly MethodInfo DeserializeAndScrub = Helper(nameof(CredentialSerialization.DeserializeAndScrub));
	private static readonly MethodInfo DeserializeFromString = Helper(nameof(CredentialSerialization.DeserializeFromString));
	private static readonly MethodInfo Serialize = Helper(nameof(CredentialSerialization.Serialize));
	private static readonly MethodInfo SerializeToString = Helper(nameof(CredentialSerialization.SerializeToString));

	private static MethodInfo Helper(string name) =>
		typeof(CredentialSerialization).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException($"{nameof(CredentialSerialization)}.{name} not found.");

	private static IEnumerable<Type> NativeStores() =>
	[
		typeof(WindowsCredentialStore),
		typeof(MacOsCredentialStore),
		typeof(LinuxSecretServiceCredentialStore),
	];

	private static MethodInfo Method(Type store, string name) =>
		store.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException($"{store.Name}.{name} not found.");

	/// <summary>
	/// Reports whether <paramref name="method"/>'s body contains a <c>call</c> or
	/// <c>callvirt</c> to <paramref name="target"/>.
	/// </summary>
	/// <remarks>
	/// Both live in this assembly, so the call site carries the target's own MethodDef
	/// token and matching on it needs no opcode table. A byte sequence that happened to
	/// look like such a call without being one could only make an assertion stricter,
	/// never let a violation through.
	/// </remarks>
	private static bool Calls(MethodInfo method, MethodInfo target)
	{
		byte[] il = method.GetMethodBody()?.GetILAsByteArray()
			?? throw new InvalidOperationException($"No IL available for {method.DeclaringType?.Name}.{method.Name}.");
		Assert.AreEqual(
			method.Module,
			target.Module,
			"The scan matches a MethodDef token, so both methods must live in one module.");

		byte[] token = BitConverter.GetBytes(target.MetadataToken);

		for (int i = 0; i + 5 <= il.Length; i++)
		{
			if (il[i] is not (0x28 or 0x6F))
			{
				continue;
			}

			if (il[i + 1] == token[0] && il[i + 2] == token[1] && il[i + 3] == token[2] && il[i + 4] == token[3])
			{
				return true;
			}
		}

		return false;
	}

	[TestMethod]
	public void EveryNativeStoreLoadsThroughTheScrubbingDeserializer()
	{
		foreach (Type store in NativeStores())
		{
			MethodInfo tryLoad = Method(store, nameof(ICredentialStore.TryLoad));

			Assert.IsTrue(
				Calls(tryLoad, DeserializeAndScrub),
				$"{store.Name}.TryLoad must deserialize through DeserializeAndScrub so the plaintext "
				+ "copy it read is zeroed before the call returns.");
			Assert.IsFalse(
				Calls(tryLoad, DeserializeFromString),
				$"{store.Name}.TryLoad must not deserialize from a string: an immutable managed string "
				+ "holds the plaintext until the GC happens to collect it, and cannot be scrubbed.");
		}
	}

	[TestMethod]
	public void EveryNativeStoreSavesFromScrubbableBytes()
	{
		foreach (Type store in NativeStores())
		{
			MethodInfo save = Method(store, nameof(ICredentialStore.Save));

			Assert.IsTrue(
				Calls(save, Serialize),
				$"{store.Name}.Save must serialize to a byte array it can zero once the native call returns.");
			Assert.IsFalse(
				Calls(save, SerializeToString),
				$"{store.Name}.Save must not serialize to a string: the plaintext would sit on the managed "
				+ "heap unscrubbable, and marshalling it would add a native copy freed without scrubbing.");
		}
	}
}
