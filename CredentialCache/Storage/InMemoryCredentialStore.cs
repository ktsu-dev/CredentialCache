// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache.Storage;

using System.Collections.Concurrent;

/// <summary>
/// A non-persistent credential store backed by an in-memory dictionary. Intended for
/// tests and applications that explicitly opt out of platform-level persistence.
/// </summary>
public sealed class InMemoryCredentialStore : ISearchableCredentialStore
{
	private readonly ConcurrentDictionary<PersonaGUID, Credential> _items = new();

	/// <inheritdoc/>
	public string Name => "InMemory";

	/// <inheritdoc/>
	public bool TryLoad(PersonaGUID persona, out Credential? credential)
	{
		ArgumentNullException.ThrowIfNull(persona);
		return _items.TryGetValue(persona, out credential);
	}

	/// <inheritdoc/>
	public void Save(PersonaGUID persona, Credential credential)
	{
		ArgumentNullException.ThrowIfNull(persona);
		ArgumentNullException.ThrowIfNull(credential);
		_items[persona] = credential;
	}

	/// <inheritdoc/>
	public bool Remove(PersonaGUID persona)
	{
		ArgumentNullException.ThrowIfNull(persona);
		return _items.TryRemove(persona, out _);
	}

	/// <inheritdoc/>
	public IEnumerable<PersonaGUID> EnumerateKeys() => [.. _items.Keys];
}
