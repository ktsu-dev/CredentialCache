namespace ktsu.CredentialCache;

using System.Collections.Concurrent;
using System.Text.Json.Serialization;

using ktsu.AppDataStorage;
using ktsu.Extensions;
using ktsu.StrongStrings;

/// <summary>
/// Represents a globally unique identifier for a persona.
/// </summary>
public sealed record class PersonaGUID : StrongStringAbstract<PersonaGUID> { }

/// <summary>
/// Manages the caching of credentials and their associated factories.
/// </summary>
public class CredentialCache : AppData<CredentialCache>
{
	/// <summary>
	/// Gets the dictionary of credential factories.
	/// </summary>
	private ConcurrentDictionary<Type, ICredentialFactory> CredentialFactories { get; init; } = new();

	/// <summary>
	/// Gets the dictionary of credentials.
	/// </summary>
	[JsonInclude]
	private ConcurrentDictionary<PersonaGUID, Credential> Credentials { get; init; } = new();

	/// <summary>
	/// Gets the singleton instance of the <see cref="CredentialCache"/> class.
	/// </summary>
	private static Lazy<CredentialCache> LazyInstance { get; } = new(LoadOrCreate);

	/// <summary>
	/// Retrieves the singleton instance of the <see cref="CredentialCache"/> class.
	/// </summary>
	public static CredentialCache Instance => LazyInstance.Value;

	/// <summary>
	/// Tries to get a credential associated with the specified persona GUID.
	/// </summary>
	/// <param name="providerGuid">The GUID of the persona.</param>
	/// <param name="gitCredential">When this method returns, contains the credential associated with the specified GUID, if the GUID is found; otherwise, null.</param>
	/// <returns><c>true</c> if the credential was found; otherwise, <c>false</c>.</returns>
	public bool TryGet(PersonaGUID providerGuid, out Credential? gitCredential) =>
		Credentials.TryGetValue(providerGuid, out gitCredential);

	/// <summary>
	/// Adds or replaces a credential associated with the specified persona GUID.
	/// </summary>
	/// <param name="providerGuid">The GUID of the persona.</param>
	/// <param name="gitCredential">The credential to add or replace.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="gitCredential"/> is null.</exception>
	public void AddOrReplace(PersonaGUID providerGuid, Credential gitCredential)
	{
		ArgumentNullException.ThrowIfNull(gitCredential);
		Credentials[providerGuid] = gitCredential;
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
}

