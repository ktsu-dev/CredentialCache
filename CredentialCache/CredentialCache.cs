// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache;

using System.Collections.Concurrent;
using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;

/// <summary>
/// Represents a globally unique identifier for a persona.
/// </summary>
public sealed record class PersonaGUID : SemanticString<PersonaGUID> { }

/// <summary>
/// Caches <see cref="Credential"/> instances in memory and persists each one through
/// an <see cref="ICredentialStore"/>. The default store routes to the platform-native
/// secret manager:
/// <list type="bullet">
///   <item><description>Windows Credential Manager on Windows</description></item>
///   <item><description>Keychain on macOS</description></item>
///   <item><description>libsecret (Secret Service) on Linux</description></item>
/// </list>
/// </summary>
public sealed class CredentialCache : IDisposable
{
	private static readonly Lock _instanceLock = new();
	private static CredentialCache? _instance;
	private static ICredentialStore? _configuredStore;

	private readonly ConcurrentDictionary<PersonaGUID, Credential> _credentials = new();
	private readonly ConcurrentDictionary<Type, ICredentialFactory> _factories = new();
	private bool _disposed;

	/// <summary>
	/// Creates a new credential cache backed by <paramref name="store"/>.
	/// </summary>
	/// <param name="store">The store responsible for persistence. Must not be null.</param>
	public CredentialCache(ICredentialStore store)
	{
		ArgumentNullException.ThrowIfNull(store);
		Store = store;
	}

	/// <summary>
	/// Gets the process-wide singleton instance, constructed on first access.
	/// </summary>
	/// <remarks>
	/// To override the backing store call <see cref="ConfigureStore"/> before
	/// touching this property. The first access constructs the singleton; later
	/// reconfiguration requires <see cref="ResetSingletonForTesting"/>.
	/// </remarks>
	public static CredentialCache Instance
	{
		get
		{
			lock (_instanceLock)
			{
				if (_instance is null)
				{
					ICredentialStore store = _configuredStore ?? CredentialStoreFactory.CreateDefault();
					_instance = new CredentialCache(store);
				}
				return _instance;
			}
		}
	}

	/// <summary>
	/// Gets the backing store used by this instance.
	/// </summary>
	public ICredentialStore Store { get; }

	/// <summary>
	/// Configures the <see cref="ICredentialStore"/> used by the singleton
	/// <see cref="Instance"/>. Must be called before the singleton is first accessed.
	/// </summary>
	/// <param name="store">The store to use.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the singleton has already been constructed.
	/// </exception>
	public static void ConfigureStore(ICredentialStore store)
	{
		ArgumentNullException.ThrowIfNull(store);
		lock (_instanceLock)
		{
			if (_instance is not null)
			{
				throw new InvalidOperationException(
					$"Cannot configure store after {nameof(Instance)} has been constructed. " +
					$"Call {nameof(ConfigureStore)} before first access, or " +
					$"{nameof(ResetSingletonForTesting)} to reset (tests only).");
			}
			_configuredStore = store;
		}
	}

	/// <summary>
	/// Resets the singleton state so a subsequent <see cref="Instance"/> access constructs
	/// a fresh cache. Intended for test isolation only.
	/// </summary>
	public static void ResetSingletonForTesting()
	{
		lock (_instanceLock)
		{
			_instance?.Dispose();
			_instance = null;
			_configuredStore = null;
		}
	}

	/// <summary>
	/// Creates a fresh <see cref="PersonaGUID"/>.
	/// </summary>
	public static PersonaGUID CreatePersonaGUID() =>
		SemanticString<PersonaGUID>.Create(Guid.NewGuid().ToString());

	/// <summary>
	/// Attempts to retrieve the credential associated with <paramref name="persona"/>,
	/// loading it from the backing store if it has not yet been cached in memory.
	/// </summary>
	public bool TryGet(PersonaGUID persona, out Credential? credential)
	{
		ArgumentNullException.ThrowIfNull(persona);
		ThrowIfDisposed();

		if (_credentials.TryGetValue(persona, out credential))
		{
			return true;
		}

		if (Store.TryLoad(persona, out Credential? loaded) && loaded is not null)
		{
			credential = _credentials.GetOrAdd(persona, loaded);
			return true;
		}

		credential = null;
		return false;
	}

	/// <summary>
	/// Adds or replaces the credential for <paramref name="persona"/> and persists it.
	/// </summary>
	public void AddOrReplace(PersonaGUID persona, Credential credential)
	{
		ArgumentNullException.ThrowIfNull(persona);
		ArgumentNullException.ThrowIfNull(credential);
		ThrowIfDisposed();

		_credentials[persona] = credential;
		Store.Save(persona, credential);
	}

	/// <summary>
	/// Removes the credential associated with <paramref name="persona"/> from both
	/// the in-memory cache and the backing store.
	/// </summary>
	public bool Remove(PersonaGUID persona)
	{
		ArgumentNullException.ThrowIfNull(persona);
		ThrowIfDisposed();

		bool removedInMemory = _credentials.TryRemove(persona, out _);
		bool removedFromStore = Store.Remove(persona);
		return removedInMemory || removedFromStore;
	}

	/// <summary>
	/// Registers a factory for credentials of type <typeparamref name="T"/>.
	/// </summary>
	public void RegisterCredentialFactory<T>(ICredentialFactory<T> factory) where T : Credential
	{
		ArgumentNullException.ThrowIfNull(factory);
		ThrowIfDisposed();
		_factories[typeof(T)] = factory;
	}

	/// <summary>
	/// Unregisters any factory previously registered for type <typeparamref name="T"/>.
	/// </summary>
	public void UnregisterCredentialFactory<T>() where T : Credential
	{
		ThrowIfDisposed();
		_factories.TryRemove(typeof(T), out _);
	}

	/// <summary>
	/// Attempts to create a credential of type <typeparamref name="T"/> using a previously
	/// registered factory.
	/// </summary>
	public bool TryCreate<T>(out Credential? credential) where T : Credential
	{
		ThrowIfDisposed();
		if (_factories.TryGetValue(typeof(T), out ICredentialFactory? factory) && factory is ICredentialFactory<T> typed)
		{
			credential = typed.Create();
			return true;
		}
		credential = null;
		return false;
	}

	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

	/// <summary>
	/// Releases the in-memory state. Stored credentials remain in the backing
	/// store; nothing is persisted or deleted at dispose-time because every
	/// mutation is already flushed eagerly in <see cref="AddOrReplace"/>.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		_credentials.Clear();
		_factories.Clear();
	}
}
