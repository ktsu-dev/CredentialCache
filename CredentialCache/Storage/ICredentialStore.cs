// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache.Storage;

/// <summary>
/// Persists <see cref="Credential"/> instances keyed by <see cref="PersonaGUID"/>.
/// Implementations are expected to delegate the at-rest protection of secrets
/// to the platform's native credential store (Windows Credential Manager, macOS
/// Keychain, Linux libsecret) or to an explicit in-memory backing.
/// </summary>
public interface ICredentialStore
{
	/// <summary>
	/// Gets a human-readable name identifying the backing store (for diagnostics).
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Attempts to load the credential associated with <paramref name="persona"/>.
	/// </summary>
	public bool TryLoad(PersonaGUID persona, out Credential? credential);

	/// <summary>
	/// Stores or replaces the credential associated with <paramref name="persona"/>.
	/// </summary>
	public void Save(PersonaGUID persona, Credential credential);

	/// <summary>
	/// Removes any credential associated with <paramref name="persona"/>.
	/// </summary>
	public bool Remove(PersonaGUID persona);
}
