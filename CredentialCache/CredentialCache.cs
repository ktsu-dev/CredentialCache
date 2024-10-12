namespace ktsu.CredentialCache;

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using ktsu.AppDataStorage;
using ktsu.Extensions;
using ktsu.StrongStrings;

public sealed record class PersonaGUID : StrongStringAbstract<PersonaGUID> { }

public class CredentialCache : AppData<CredentialCache>
{
	[JsonInclude]
	private ConcurrentDictionary<PersonaGUID, Credential> Credentials { get; init; } = [];

	private static Lazy<CredentialCache> Instance { get; } = new(LoadOrCreate);

	public static CredentialCache GetInstance() => Instance.Value;

	public bool TryGet(PersonaGUID providerGuid, out Credential? gitCredential) =>
		Credentials.TryGetValue(providerGuid, out gitCredential);

	public void AddOrReplace(PersonaGUID providerGuid, Credential gitCredential)
	{
		_ = Credentials.AddOrUpdate(providerGuid, gitCredential, (_, _) => gitCredential);
		Save();
	}

	public static PersonaGUID CreatePersonaGUID() => Guid.NewGuid().ToString().As<PersonaGUID>();
}
