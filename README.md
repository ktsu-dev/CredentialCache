# ktsu.CredentialCache

> A cross-platform credential cache for .NET that stores secrets in the host's native keyring.

[![License](https://img.shields.io/github/license/ktsu-dev/CredentialCache)](https://github.com/ktsu-dev/CredentialCache/blob/main/LICENSE.md)
[![NuGet](https://img.shields.io/nuget/v/ktsu.CredentialCache.svg)](https://www.nuget.org/packages/ktsu.CredentialCache/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.CredentialCache.svg)](https://www.nuget.org/packages/ktsu.CredentialCache/)
[![Build Status](https://github.com/ktsu-dev/CredentialCache/workflows/build/badge.svg)](https://github.com/ktsu-dev/CredentialCache/actions)

## Overview

CredentialCache keeps credentials in memory for fast lookup during the lifetime of a process and persists each one through an `ICredentialStore` whose default implementation delegates to the platform-native secret manager:

| Platform | Backing store | Native API |
|----------|--------------|-----|
| Windows  | Credential Manager | `advapi32` &mdash; `CredReadW` / `CredWriteW` / `CredDeleteW` / `CredEnumerateW` |
| macOS    | Keychain Services | `Security.framework` &mdash; `SecKeychainAddGenericPassword` and friends |
| Linux    | freedesktop.org Secret Service | `libsecret-1.so.0` &mdash; `secret_password_store_sync` / `lookup_sync` / `clear_sync` |
| Other / opt-out | None | `InMemoryCredentialStore` |

Each persona's credential is stored as its own entry in the OS keyring scoped by a `service` name &mdash; the library never writes a plaintext blob to disk.

## Installation

```bash
dotnet add package ktsu.CredentialCache
```

Requires .NET 9 or .NET 10.

### Linux runtime prerequisites

The Linux store requires `libsecret` and a running Secret Service implementation (gnome-keyring, KWallet's secret-service bridge, KeePassXC, &hellip;). On Debian/Ubuntu:

```bash
sudo apt-get install libsecret-1-0 gnome-keyring
```

On headless or CI hosts you'll typically need a session bus and an unlocked keyring &mdash; see the `cross-platform.yml` workflow in this repo for the `dbus-run-session` + `gnome-keyring-daemon` incantation. If a Secret Service isn't available in your deployment, fall back to `InMemoryCredentialStore` (or roll your own `ICredentialStore`).

## Quick start

```csharp
using ktsu.CredentialCache;
using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;

// Pick the platform-native store explicitly...
ICredentialStore store = CredentialStoreFactory.CreateDefault("MyApp");
using CredentialCache cache = new(store);

// ...or just use the singleton, which calls CreateDefault() on first access.
CredentialCache singleton = CredentialCache.Instance;

PersonaGUID persona = CredentialCache.CreatePersonaGUID();

cache.AddOrReplace(persona, new CredentialWithUsernamePassword
{
    Username = SemanticString<CredentialUsername>.Create("alice"),
    Password = SemanticString<CredentialPassword>.Create("hunter2"),
});

if (cache.TryGet(persona, out Credential? stored)
    && stored is CredentialWithUsernamePassword creds)
{
    Console.WriteLine($"Hello, {creds.Username}");
}

cache.Remove(persona);
```

### Pick your own service name

`CredentialStoreFactory.CreateDefault(serviceName)` scopes entries by a logical service name (defaults to `"ktsu.CredentialCache"`). If two applications share a host, pass per-app names so their keyring entries don't collide.

## Credential types

The library ships with three concrete `Credential` subclasses:

| Type | Use it for |
|------|-----------|
| `CredentialWithNothing` | Sentinel for "no credential required" |
| `CredentialWithToken` | Opaque bearer or API token |
| `CredentialWithUsernamePassword` | Classic username + password pair |

### Adding your own credential type

`Credential` is a polymorphic record class round-tripped through `System.Text.Json`. New subclasses need a `[JsonDerivedType]` on the base so deserialization can resolve them:

```csharp
// 1. Add the subclass.
public sealed class CredentialWithCertificate : Credential
{
    public string Thumbprint { get; init; } = "";
}

// 2. Register it on the base in Credential.cs.
[JsonDerivedType(typeof(CredentialWithCertificate), nameof(CredentialWithCertificate))]
public abstract class Credential { /* ... */ }

// 3. Optional: register a factory so TryCreate<T> works.
public sealed class CertificateFactory : ICredentialFactory<CredentialWithCertificate>
{
    public CredentialWithCertificate Create() => new();
}
cache.RegisterCredentialFactory(new CertificateFactory());
```

If a subclass uses `SemanticString<T>` properties, they round-trip through `ktsu.RoundTripStringJsonConverter` automatically.

## Customising the backing store

`ICredentialStore` is a small CRUD interface (`TryLoad` / `Save` / `Remove`). Bring your own implementation when you need a different backend (HashiCorp Vault, an encrypted file, a test double):

```csharp
public sealed class MyCustomStore : ICredentialStore { /* ... */ }

ICredentialStore store = new MyCustomStore();
CredentialCache.ConfigureStore(store); // must be called before first Instance access
```

For unit tests, use the in-memory store and skip the singleton entirely:

```csharp
using CredentialCache cache = new(new InMemoryCredentialStore());
```

### Enumerating stored personas

`ICredentialStore` deliberately has no `EnumerateKeys` method, because macOS Keychain and libsecret require substantially more native marshalling for enumeration than the simple key-value ops. The optional `ISearchableCredentialStore` interface adds it, and only the Windows and in-memory stores implement it:

```csharp
if (cache.Store is ISearchableCredentialStore searchable)
{
    foreach (PersonaGUID key in searchable.EnumerateKeys())
    {
        // ...
    }
}
else
{
    // Track persona GUIDs yourself on macOS / Linux.
}
```

## Platform notes

- **Windows Credential Manager** caps the credential blob at 2560 bytes (`5 * 512`). Tokens larger than that throw `CredentialStoreException` &mdash; split or compress before storing.
- **macOS** uses the user's default login keychain. The first access from an application prompts the user for permission, as with any keychain client.
- **Linux** requires `libsecret-1` plus an active Secret Service. Headless CI agents typically have neither &mdash; use `InMemoryCredentialStore` there, or set up `dbus-run-session` + `gnome-keyring-daemon` as the `cross-platform.yml` workflow does.
- All native calls happen on the thread the API is invoked from. The library's in-memory cache is thread-safe (`ConcurrentDictionary`); the native APIs themselves are documented as thread-safe by their respective platform owners, but blocking calls (especially libsecret) are not cheap &mdash; treat `Save` / `Remove` as I/O, not as cheap accessors.

## API summary

### `CredentialCache`

| Member | Description |
|--------|-------------|
| `CredentialCache(ICredentialStore store)` | Construct an instance with an explicit store. |
| `static Instance` | Process-wide singleton (lazy, thread-safe). |
| `Store` | The backing store passed to the constructor. |
| `static ConfigureStore(ICredentialStore)` | Override the singleton's store. Must precede first `Instance` access. |
| `static ResetSingletonForTesting()` | Dispose the singleton and clear configuration. Tests only. |
| `static CreatePersonaGUID()` | Allocates a new `PersonaGUID`. |
| `TryGet(persona, out cred)` | Memory-cache lookup with fall-through to the backing store. |
| `AddOrReplace(persona, cred)` | Persists eagerly through the store. |
| `Remove(persona)` | Deletes from both the in-memory cache and the store. |
| `RegisterCredentialFactory<T>(factory)` | Optional factory hook used by `TryCreate<T>`. |
| `TryCreate<T>(out cred)` | Constructs a credential via a registered factory. |
| `Dispose()` | Releases in-memory state. The OS store is left untouched. |

### `ICredentialStore`

| Member | Description |
|--------|-------------|
| `Name` | Diagnostic identifier (e.g. `"Windows Credential Manager"`, `"macOS Keychain"`, `"Linux libsecret (Secret Service)"`, `"InMemory"`). |
| `TryLoad(persona, out cred)` | Load a single credential. |
| `Save(persona, cred)` | Persist or overwrite a single credential. |
| `Remove(persona)` | Delete a single credential. |

### `ISearchableCredentialStore : ICredentialStore`

| Member | Description |
|--------|-------------|
| `EnumerateKeys()` | Enumerate every persona key currently persisted (Windows, in-memory). |

## Don't dispose the singleton

`CredentialCache.Instance` returns a process-wide singleton. Calling `Dispose()` on it (e.g. via `using var c = CredentialCache.Instance;`) puts the singleton in a disposed state and the next consumer in the process gets `ObjectDisposedException`. If you need disposal semantics, construct your own instance with `new CredentialCache(store)`.

## License

MIT &mdash; see [LICENSE.md](LICENSE.md).
