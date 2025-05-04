# ktsu.CredentialCache

> A secure credential storage and management system for .NET applications.

[![License](https://img.shields.io/github/license/ktsu-dev/CredentialCache)](https://github.com/ktsu-dev/CredentialCache/blob/main/LICENSE.md)
[![NuGet](https://img.shields.io/nuget/v/ktsu.CredentialCache.svg)](https://www.nuget.org/packages/ktsu.CredentialCache/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.CredentialCache.svg)](https://www.nuget.org/packages/ktsu.CredentialCache/)
[![Build Status](https://github.com/ktsu-dev/CredentialCache/workflows/build/badge.svg)](https://github.com/ktsu-dev/CredentialCache/actions)
[![GitHub Stars](https://img.shields.io/github/stars/ktsu-dev/CredentialCache?style=social)](https://github.com/ktsu-dev/CredentialCache/stargazers)

## Introduction

CredentialCache is a .NET library that provides a secure and convenient way to store, retrieve, and manage credentials in applications. It offers a flexible caching mechanism for various credential types while handling encryption, security, and persistence automatically.

## Features

- **Secure Storage**: Encrypts sensitive credentials using platform-specific security features
- **Memory Caching**: Efficiently caches credentials in memory for quick access
- **Multiple Credential Types**: Supports usernames/passwords, API keys, tokens, and certificates
- **Automatic Expiration**: Time-based expiration for cached credentials
- **Credential Rotation**: Support for credential rotation and refresh workflows
- **Thread Safety**: Safe for concurrent access from multiple threads
- **Extensible**: Easily extend with custom credential types and storage providers

## Installation

### Package Manager Console

```powershell
Install-Package ktsu.CredentialCache
```

### .NET CLI

```bash
dotnet add package ktsu.CredentialCache
```

### Package Reference

```xml
<PackageReference Include="ktsu.CredentialCache" Version="x.y.z" />
```

## Usage Examples

### Basic Example

```csharp
using ktsu.CredentialCache;

// Create a credential cache
var cache = new CredentialCache();

// Store a credential
var credential = new UsernamePasswordCredential
{
    Username = "user@example.com",
    Password = "securePassword123",
    Domain = "example.com"
};

cache.Store("myAppLogin", credential);

// Retrieve the credential later
if (cache.TryGet("myAppLogin", out UsernamePasswordCredential retrievedCredential))
{
    Console.WriteLine($"Retrieved username: {retrievedCredential.Username}");
    // Use the credential for authentication
}
```

### Working with API Credentials

```csharp
// Store an API key
var apiCredential = new ApiKeyCredential
{
    Key = "api_12345abcde",
    Secret = "apisecret_xyz789",
    Endpoint = "https://api.example.com"
};

// Store with expiration
cache.Store("apiAccess", apiCredential, TimeSpan.FromHours(1));

// Check if credential exists and is not expired
if (cache.Contains("apiAccess"))
{
    var cred = cache.Get<ApiKeyCredential>("apiAccess");
    // Use the API credential
}
```

### Advanced Usage with Persistent Storage

```csharp
// Create a cache with persistent storage
var options = new CredentialCacheOptions
{
    PersistToStorage = true,
    StoragePath = "credentials.dat",
    EncryptionLevel = EncryptionLevel.High
};

var persistentCache = new CredentialCache(options);

// Store a credential that will be saved to disk
var oauthCredential = new OAuthCredential
{
    AccessToken = "access_token_123",
    RefreshToken = "refresh_token_456",
    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
};

persistentCache.Store("oauth", oauthCredential);

// Later, even after application restart:
var loadedCache = new CredentialCache(options);
if (loadedCache.TryGet("oauth", out OAuthCredential oauth))
{
    if (oauth.IsExpired)
    {
        // Refresh the token
        oauth = RefreshOAuthToken(oauth);
        loadedCache.Store("oauth", oauth);
    }
    
    // Use the OAuth token
}
```

## API Reference

### `CredentialCache` Class

The main class for storing and retrieving credentials.

#### Properties

| Name | Type | Description |
|------|------|-------------|
| `Count` | `int` | Number of credentials in the cache |
| `Options` | `CredentialCacheOptions` | Configuration options for this cache instance |

#### Methods

| Name | Return Type | Description |
|------|-------------|-------------|
| `Store(string key, ICredential credential, TimeSpan? expiration = null)` | `void` | Stores a credential with optional expiration |
| `Get<T>(string key) where T : ICredential` | `T` | Gets a credential by key (throws if not found) |
| `TryGet<T>(string key, out T credential) where T : ICredential` | `bool` | Tries to get a credential by key |
| `Remove(string key)` | `bool` | Removes a credential from the cache |
| `Contains(string key)` | `bool` | Checks if a credential exists and is not expired |
| `Clear()` | `void` | Removes all credentials from the cache |

### Credential Types

| Type | Description |
|------|-------------|
| `UsernamePasswordCredential` | Standard username and password combination |
| `ApiKeyCredential` | API key and optional secret |
| `OAuthCredential` | OAuth access and refresh tokens |
| `CertificateCredential` | Certificate-based authentication |

## Contributing

Contributions are welcome! Here's how you can help:

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

Please ensure your code follows security best practices when dealing with sensitive credential information.

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.
