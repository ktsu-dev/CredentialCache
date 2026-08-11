// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Storage;

/// <summary>
/// An <see cref="ICredentialStore"/> that can enumerate its persisted keys.
/// Only some backends support this efficiently - e.g. Windows Credential
/// Manager via <c>CredEnumerate</c>. The macOS and Linux native APIs require
/// substantial extra marshalling to enumerate, so they intentionally do not
/// implement this interface; callers needing enumeration on those platforms
/// should track <see cref="PersonaGUID"/> values themselves.
/// </summary>
public interface ISearchableCredentialStore : ICredentialStore
{
	/// <summary>
	/// Enumerates every persona key currently persisted in this store.
	/// </summary>
	public IEnumerable<PersonaGUID> EnumerateKeys();
}
