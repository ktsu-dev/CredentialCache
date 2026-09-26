// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;

/// <summary>
/// An <see cref="ICredentialStore"/> that parks the first thread to enter
/// <see cref="Remove"/> so a test can drive a second thread through
/// <see cref="CredentialCache.AddOrReplace"/> while it is held there.
/// </summary>
/// <remarks>
/// The wait is bounded rather than indefinite on purpose. Once mutations of one
/// persona are serialized, the second thread cannot reach <see cref="Release"/> —
/// it is blocked behind the removal — so an unbounded wait would deadlock the
/// fixed code instead of letting it finish.
/// </remarks>
internal sealed class RemoveBlockingCredentialStore : ICredentialStore, IDisposable
{
	private readonly InMemoryCredentialStore _inner = new();
	private readonly ManualResetEventSlim _release = new(false);
	private int _removeCalls;

	/// <inheritdoc/>
	public string Name => "RemoveBlocking";

	/// <summary>
	/// Gets an event signalled once a thread has entered <see cref="Remove"/>.
	/// </summary>
	public ManualResetEventSlim RemoveEntered { get; } = new(false);

	/// <summary>
	/// Gets how long the parked <see cref="Remove"/> waits before giving up.
	/// </summary>
	public TimeSpan ReleaseTimeout { get; init; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets a value indicating whether the parked <see cref="Remove"/> gave up waiting
	/// rather than being released. True means the interleaving the test tried to force
	/// could not happen.
	/// </summary>
	public bool ReleaseTimedOut { get; private set; }

	/// <summary>
	/// Lets the parked <see cref="Remove"/> continue.
	/// </summary>
	public void Release() => _release.Set();

	/// <summary>
	/// Reports whether the backing store currently holds <paramref name="persona"/>,
	/// without going through the cache.
	/// </summary>
	public bool Holds(PersonaGUID persona) => _inner.TryLoad(persona, out _);

	/// <inheritdoc/>
	public bool TryLoad(PersonaGUID persona, out Credential? credential) =>
		_inner.TryLoad(persona, out credential);

	/// <inheritdoc/>
	public void Save(PersonaGUID persona, Credential credential) => _inner.Save(persona, credential);

	/// <inheritdoc/>
	public bool Remove(PersonaGUID persona)
	{
		if (Interlocked.Increment(ref _removeCalls) == 1)
		{
			RemoveEntered.Set();
			ReleaseTimedOut = !_release.Wait(ReleaseTimeout);
		}

		return _inner.Remove(persona);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		RemoveEntered.Dispose();
		_release.Dispose();
	}
}

[TestClass]
public class PersonaMutationRaceTests
{
	private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(30);

	[TestMethod]
	// The second row is not a duplicate: two PersonaGUID instances carrying the same value
	// are one key to the cache, so they must also be one unit of mutual exclusion. It fails
	// an implementation that serializes on the instance rather than on the value.
	[DataRow(false, DisplayName = "the same PersonaGUID instance")]
	[DataRow(true, DisplayName = "a distinct PersonaGUID of equal value")]
	public void RemoveInterleavedWithAddOrReplaceDoesNotLeaveACredentialLiveOnlyInMemory(bool useEqualInstance)
	{
		using RemoveBlockingCredentialStore store = new();
		using CredentialCache cache = new(store);
		PersonaGUID persona = CredentialCache.CreatePersonaGUID();
		PersonaGUID adderPersona = useEqualInstance
			? SemanticString<PersonaGUID>.Create(persona.ToString())
			: persona;

		Assert.AreEqual(persona, adderPersona, "The two personas must be one key to the cache.");
		Assert.AreEqual(
			persona.GetHashCode(),
			adderPersona.GetHashCode(),
			"Equal personas must hash equally, or they cannot share a lock.");

		cache.AddOrReplace(persona, new CredentialWithNothing());

		Task remover = Task.Run(() => cache.Remove(persona));
		Task adder = Task.Run(() =>
		{
			Assert.IsTrue(
				store.RemoveEntered.Wait(HandshakeTimeout),
				"No thread entered the store's Remove, so the interleaving was never set up.");
			cache.AddOrReplace(adderPersona, new CredentialWithNothing());
			store.Release();
		});

		Assert.IsTrue(
			Task.WaitAll([remover, adder], CompletionTimeout),
			"The removal and the replacement did not both finish.");

		bool heldInStore = store.Holds(persona);
		bool readable = cache.TryGet(persona, out _);

		Assert.AreEqual(
			heldInStore,
			readable,
			$"A credential must be readable if and only if the store holds it, but readable={readable} "
			+ $"and store={heldInStore}. A credential readable from a store that no longer holds it is a "
			+ $"removal that left it live in memory. (The removal "
			+ $"{(store.ReleaseTimedOut ? "was not" : "was")} interleaved with the replacement.)");
	}

	[TestMethod]
	public void ConcurrentAddOrReplaceAndRemoveOnTheSamePersonaAgreeOnTheFinalState()
	{
		const int attempts = 500;

		for (int attempt = 0; attempt < attempts; attempt++)
		{
			InMemoryCredentialStore store = new();
			using CredentialCache cache = new(store);
			PersonaGUID persona = CredentialCache.CreatePersonaGUID();
			cache.AddOrReplace(persona, new CredentialWithNothing());

			using Barrier gate = new(2);
			Task remover = Task.Run(() =>
			{
				gate.SignalAndWait();
				cache.Remove(persona);
			});
			Task adder = Task.Run(() =>
			{
				gate.SignalAndWait();
				cache.AddOrReplace(persona, new CredentialWithNothing());
			});

			Assert.IsTrue(
				Task.WaitAll([remover, adder], CompletionTimeout),
				$"Attempt {attempt}: the removal and the replacement did not both finish.");

			bool heldInStore = store.TryLoad(persona, out _);
			bool readable = cache.TryGet(persona, out _);

			Assert.AreEqual(
				heldInStore,
				readable,
				$"Attempt {attempt}: readable={readable} but store={heldInStore}. Whichever of the two "
				+ "operations ran second, the cache and the store must end up agreeing.");
		}
	}
}
