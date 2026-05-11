// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache.Storage;

using System.Runtime.InteropServices;

/// <summary>
/// Selects the appropriate <see cref="ICredentialStore"/> implementation for the
/// current operating system.
/// </summary>
public static class CredentialStoreFactory
{
	/// <summary>
	/// Returns the default platform-native credential store for the current OS.
	/// </summary>
	/// <param name="serviceName">
	/// A logical service name used to scope the stored credentials. Defaults to
	/// <c>ktsu.CredentialCache</c>. Use a per-application name when several
	/// applications share a host to avoid collisions.
	/// </param>
	/// <exception cref="PlatformNotSupportedException">
	/// Thrown when the current platform is not Windows, macOS, or Linux.
	/// </exception>
	public static ICredentialStore CreateDefault(string serviceName = "ktsu.CredentialCache")
	{
		ArgumentException.ThrowIfNullOrEmpty(serviceName);

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return new WindowsCredentialStore(serviceName);
		}
		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			return new MacOsCredentialStore(serviceName);
		}
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return new LinuxSecretServiceCredentialStore(serviceName);
		}

		throw new PlatformNotSupportedException(
			$"CredentialCache has no native credential store for '{RuntimeInformation.OSDescription}'. " +
			$"Use {nameof(InMemoryCredentialStore)} or supply a custom {nameof(ICredentialStore)} implementation.");
	}
}
