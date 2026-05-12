# ktsu.CredentialCache

> A cross-platform credential cache for .NET that stores secrets in the host's native keyring.

[![License](https://img.shields.io/github/license/ktsu-dev/CredentialCache)](https://github.com/ktsu-dev/CredentialCache/blob/main/LICENSE.md)
[![NuGet](https://img.shields.io/nuget/v/ktsu.CredentialCache.svg)](https://www.nuget.org/packages/ktsu.CredentialCache/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.CredentialCache.svg)](https://www.nuget.org/packages/ktsu.CredentialCache/)
[![Build Status](https://github.com/ktsu-dev/CredentialCache/workflows/build/badge.svg)](https://github.com/ktsu-dev/CredentialCache/actions)

## Overview

CredentialCache keeps credentials in memory for fast lookup during the lifetime of a process and persists each one through an `ICredentialStore` whose default implementation delegates to the platform-native secret manager:

| Platform | Backing store | API |
|----------|--------------|-----|
| Windows  | Windows Credential Manager | `advapi32` (`CredRead`/`CredWrite`/`CredDelete`) |
| macOS    | Keychain Services | `Security.framework` (`SecKeychain*`) |
| Linux    | freedesktop.org Secret Service | `libsecret-1.so.0` |
| Other / opt-out | None | `InMemoryCredentialStore` |

Each persona's credential is stored as its own entry in the OS keyring &mdash; the library never writes a plaintext blob to disk.

## Installation

```bash
dotnet add package ktsu.CredentialCache
```

## Quick start

```csharp
using ktsu.CredentialCache;
using ktsu.CredentialCache.Storage;

// Pick the platform-native store explicitly...
ICredentialStore store = CredentialStoreFactory.CreateDefault();
using CredentialCache cache = new(store);

// ...or just use the singleton, which calls CreateDefault() on first access.
CredentialCache singleton = CredentialCache.Instance;

PersonaGUID persona = CredentialCache.CreatePersonaGUID();

cache.AddOrReplace(persona, new CredentialWithUsernamePassword
{
    Username = ktsu.Semantics.Strings.SemanticString<CredentialUsername>.Create("alice"),
    Password = ktsu.Semantics.Strings.SemanticString<CredentialPassword>.Create("hunter2"),
});

if (cache.TryGet(persona, out Credential? stored)
    && stored is CredentialWithUsernamePassword creds)
{
    Console.WriteLine($"Hello, {creds.Username}");
}

cache.Remove(persona);
```

## Credential types

- `CredentialWithNothing` &mdash; sentinel for "no credential required".
- `CredentialWithToken` &mdash; opaque bearer / API token.
- `CredentialWithUsernamePassword` &mdash; classic username + password pair.

New credential types must derive from `Credential` and be registered with a `[JsonDerivedType]` attribute on `Credential` so polymorphic serialization round-trips through the keyring entry.

## Customising the backing store

`ICredentialStore` is a small CRUD interface (`TryLoad`/`Save`/`Remove`/`EnumerateKeys`). Bring your own implementation when you need a different backend (HashiCorp Vault, an encrypted file, a test double):

```csharp
ICredentialStore store = new MyCustomStore();
CredentialCache.ConfigureStore(store); // must be called before first Instance access
```

For unit tests, use the in-memory store and skip the singleton entirely:

```csharp
using CredentialCache cache = new(new InMemoryCredentialStore());
```

## Platform notes

- **Windows Credential Manager** caps the credential blob at 2560 bytes. Tokens larger than that will throw `CredentialStoreException` &mdash; split or compress before storing.
- **Linux** requires `libsecret-1` (e.g. `apt install libsecret-1-0`) plus a running Secret Service implementation (gnome-keyring, KWallet's secret-service bridge, KeePassXC, &hellip;). Headless CI agents typically have neither &mdash; use `InMemoryCredentialStore` there.
- **macOS** uses the user's default login keychain. The first access from an application prompts the user for permission, as with any keychain client.
- `EnumerateKeys()` is fully implemented on Windows. On macOS and Linux it currently returns an empty sequence (implementing it would require substantially more native marshalling for a use-case most consumers can satisfy by tracking persona GUIDs themselves).

## API summary

### `CredentialCache`

| Member | Description |
|--------|-------------|
| `CredentialCache(ICredentialStore store)` | Construct an instance with an explicit store. |
| `static Instance` | Process-wide singleton (lazy, thread-safe). |
| `static ConfigureStore(ICredentialStore)` | Override the singleton's store. Must precede first `Instance` access. |
| `static ResetSingletonForTesting()` | Dispose the singleton and clear configuration. Tests only. |
| `static CreatePersonaGUID()` | Allocates a new `PersonaGUID`. |
| `TryGet(persona, out cred)` | Memory-cache lookup with fallthrough to the backing store. |
| `AddOrReplace(persona, cred)` | Persists eagerly through the store. |
| `Remove(persona)` | Deletes from both the in-memory cache and the store. |
| `RegisterCredentialFactory<T>(factory)` | Optional factory hook used by `TryCreate<T>`. |
| `TryCreate<T>(out cred)` | Constructs a credential via a registered factory. |
| `Dispose()` | Releases in-memory state. The OS store is left untouched. |

### `ICredentialStore`

| Member | Description |
|--------|-------------|
| `Name` | Diagnostic identifier (`"Windows Credential Manager"`, `"macOS Keychain"`, `"Linux libsecret (Secret Service)"`, `"InMemory"`). |
| `TryLoad(persona, out cred)` | Load a single credential. |
| `Save(persona, cred)` | Persist or overwrite a single credential. |
| `Remove(persona)` | Delete a single credential. |
| `EnumerateKeys()` | Enumerate persona keys (Windows only by default). |

## License

MIT &mdash; see [LICENSE.md](LICENSE.md).
