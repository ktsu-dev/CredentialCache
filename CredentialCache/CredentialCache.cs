// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache;

using System.Collections.Concurrent;
using System.Text.Json.Serialization;

using ktsu.Extensions;
using ktsu.FileSystemProvider;
using ktsu.PersistenceProvider;
using ktsu.Semantics.Strings;
using ktsu.UniversalSerializer.Json;
using ktsu.UniversalSerializer.Services;

/// <summary>
/// Represents a globally unique identifier for a persona.
/// </summary>
public sealed record class PersonaGUID : SemanticString<PersonaGUID> { }

/// <summary>
/// Data model for credential cache persistence.
/// </summary>
internal sealed class CredentialCacheData
{
	/// <summary>
	/// Gets or sets the dictionary of credentials.
	/// </summary>
	[JsonInclude]
	public ConcurrentDictionary<PersonaGUID, Credential> Credentials { get; set; } = new();
}

/// <summary>
/// Manages the caching of credentials and their associated factories.
/// </summary>
public sealed class CredentialCache : IDisposable
{
	private const string CacheKey = "CredentialCache";
	private static readonly object _lock = new();
	private static CredentialCache? _instance;
	private static IPersistenceProvider<string>? _persistenceProvider;

	/// <summary>
	/// Gets the dictionary of credential factories.
	/// </summary>
	private ConcurrentDictionary<Type, ICredentialFactory> CredentialFactories { get; } = new();

	/// <summary>
	/// Gets the underlying data model.
	/// </summary>
	private CredentialCacheData Data { get; set; }

	/// <summary>
	/// Gets the persistence provider used for storage operations.
	/// </summary>
	private IPersistenceProvider<string> PersistenceProvider { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="CredentialCache"/> class.
	/// </summary>
	/// <param name="persistenceProvider">The persistence provider for storage operations.</param>
	private CredentialCache(IPersistenceProvider<string> persistenceProvider)
	{
		PersistenceProvider = persistenceProvider ?? throw new ArgumentNullException(nameof(persistenceProvider));
		Data = LoadOrCreateData().GetAwaiter().GetResult();
	}

	/// <summary>
	/// Gets the singleton instance of the <see cref="CredentialCache"/> class.
	/// </summary>
	public static CredentialCache Instance
	{
		get
		{
			lock (_lock)
			{
				if (_instance is null)
				{
					_persistenceProvider ??= CreateDefaultPersistenceProvider();
					_instance = new CredentialCache(_persistenceProvider);
				}
				return _instance;
			}
		}
	}

	/// <summary>
	/// Configures the persistence provider for the credential cache.
	/// This must be called before accessing the Instance property if you want to use a custom provider.
	/// </summary>
	/// <param name="persistenceProvider">The persistence provider to use.</param>
	public static void ConfigurePersistenceProvider(IPersistenceProvider<string> persistenceProvider)
	{
		lock (_lock)
		{
			if (_instance is not null)
			{
				throw new InvalidOperationException("Cannot configure persistence provider after instance has been created. Call this method before accessing Instance.");
			}
			_persistenceProvider = persistenceProvider ?? throw new ArgumentNullException(nameof(persistenceProvider));
		}
	}

	/// <summary>
	/// Tries to get a credential associated with the specified persona GUID.
	/// </summary>
	/// <param name="providerGuid">The GUID of the persona.</param>
	/// <param name="gitCredential">When this method returns, contains the credential associated with the specified GUID, if the GUID is found; otherwise, null.</param>
	/// <returns><c>true</c> if the credential was found; otherwise, <c>false</c>.</returns>
	public bool TryGet(PersonaGUID providerGuid, out Credential? gitCredential) =>
		Data.Credentials.TryGetValue(providerGuid, out gitCredential);

	/// <summary>
	/// Adds or replaces a credential associated with the specified persona GUID.
	/// </summary>
	/// <param name="providerGuid">The GUID of the persona.</param>
	/// <param name="gitCredential">The credential to add or replace.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="gitCredential"/> is null.</exception>
	public async Task AddOrReplaceAsync(PersonaGUID providerGuid, Credential gitCredential, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(gitCredential);
		Data.Credentials[providerGuid] = gitCredential;
		await SaveAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Adds or replaces a credential associated with the specified persona GUID synchronously.
	/// </summary>
	/// <param name="providerGuid">The GUID of the persona.</param>
	/// <param name="gitCredential">The credential to add or replace.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="gitCredential"/> is null.</exception>
	public void AddOrReplace(PersonaGUID providerGuid, Credential gitCredential)
	{
		AddOrReplaceAsync(providerGuid, gitCredential).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Tries to create a credential of the specified type.
	/// </summary>
	/// <typeparam name="T">The type of the credential.</typeparam>
	/// <param name="credential">When this method returns, contains the created credential, if the factory is found; otherwise, null.</param>
	/// <returns><c>true</c> if the credential was created; otherwise, <c>false</c>.</returns>
	public bool TryCreate<T>(out Credential? credential) where T : Credential
	{
		if (CredentialFactories.TryGetValue(typeof(T), out var credentialFactory))
		{
			if (credentialFactory is ICredentialFactory<T> factory)
			{
				credential = factory.Create();
				return true;
			}
		}

		credential = null;
		return false;
	}

	/// <summary>
	/// Creates a new persona GUID.
	/// </summary>
	/// <returns>A new <see cref="PersonaGUID"/>.</returns>
	public static PersonaGUID CreatePersonaGUID() => Guid.NewGuid().ToString().As<PersonaGUID>();

	/// <summary>
	/// Registers a credential factory for the specified credential type.
	/// </summary>
	/// <typeparam name="T">The type of the credential.</typeparam>
	/// <param name="factory">The factory to register.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
	public void RegisterCredentialFactory<T>(ICredentialFactory<T> factory) where T : Credential
	{
		ArgumentNullException.ThrowIfNull(factory);
		CredentialFactories[typeof(T)] = factory;
	}

	/// <summary>
	/// Unregisters a credential factory for the specified credential type.
	/// </summary>
	/// <typeparam name="T">The type of the credential.</typeparam>
	public void UnregisterCredentialFactory<T>() where T : Credential =>
		CredentialFactories.TryRemove(typeof(T), out _);

	/// <summary>
	/// Saves the current credential cache data to persistent storage.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous save operation.</returns>
	public async Task SaveAsync(CancellationToken cancellationToken = default)
	{
		await PersistenceProvider.StoreAsync(CacheKey, Data, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Saves the current credential cache data to persistent storage synchronously.
	/// </summary>
	public void Save()
	{
		SaveAsync().GetAwaiter().GetResult();
	}

	/// <summary>
	/// Loads or creates the credential cache data from persistent storage.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>The loaded or newly created credential cache data.</returns>
	private async Task<CredentialCacheData> LoadOrCreateData(CancellationToken cancellationToken = default)
	{
		return await PersistenceProvider.RetrieveOrCreateAsync<CredentialCacheData>(CacheKey, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Creates the default persistence provider for the credential cache.
	/// </summary>
	/// <returns>A new instance of the default persistence provider.</returns>
	private static IPersistenceProvider<string> CreateDefaultPersistenceProvider()
	{
		var fileSystemProvider = new FileSystemProvider();
		var jsonSerializer = new JsonSerializer();
		var serializationProvider = new UniversalSerializationProvider(jsonSerializer, providerName: "CredentialCache.Json");
		return new AppDataPersistenceProvider<string>(
			fileSystemProvider,
			serializationProvider,
			applicationName: "ktsu",
			subdirectory: "CredentialCache");
	}

	/// <summary>
	/// Disposes the credential cache instance and saves any pending changes.
	/// </summary>
	public void Dispose()
	{
		Save();
	}
}

