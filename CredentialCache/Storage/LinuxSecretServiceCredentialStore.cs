// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache.Storage;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>
/// A credential store backed by the freedesktop.org Secret Service API via
/// <c>libsecret-1.so.0</c>. Each <see cref="PersonaGUID"/> is stored as an item in the
/// default collection (typically the user's login keyring) with attributes
/// <c>service</c> and <c>account</c> identifying it.
/// </summary>
/// <remarks>
/// Requires libsecret to be installed on the host. On headless systems without
/// a running secret-service implementation (e.g. minimal containers, build agents)
/// this provider will fail at the first operation; consumers should detect this
/// and fall back to <see cref="InMemoryCredentialStore"/> if appropriate.
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class LinuxSecretServiceCredentialStore : ICredentialStore
{
	internal const string DefaultServiceName = "ktsu.CredentialCache";

	private readonly string _serviceName;

	public LinuxSecretServiceCredentialStore(string serviceName = DefaultServiceName)
	{
		ArgumentException.ThrowIfNullOrEmpty(serviceName);
		_serviceName = serviceName;
	}

	/// <inheritdoc/>
	public string Name => "Linux libsecret (Secret Service)";

	/// <inheritdoc/>
	public bool TryLoad(PersonaGUID persona, out Credential? credential)
	{
		ArgumentNullException.ThrowIfNull(persona);
		credential = null;

		IntPtr error = IntPtr.Zero;
		IntPtr passwordPtr = NativeMethods.secret_password_lookup_sync(
			Schema.Handle,
			IntPtr.Zero,
			ref error,
			"service", _serviceName,
			"account", persona.ToString(),
			IntPtr.Zero);

		ThrowIfError(error, "secret_password_lookup_sync");

		if (passwordPtr == IntPtr.Zero)
		{
			return false;
		}

		try
		{
			string? value = Marshal.PtrToStringUTF8(passwordPtr);
			if (string.IsNullOrEmpty(value))
			{
				return false;
			}
			credential = CredentialSerialization.DeserializeFromString(value);
			return credential is not null;
		}
		finally
		{
			NativeMethods.secret_password_free(passwordPtr);
		}
	}

	/// <inheritdoc/>
	public void Save(PersonaGUID persona, Credential credential)
	{
		ArgumentNullException.ThrowIfNull(persona);
		ArgumentNullException.ThrowIfNull(credential);

		string value = CredentialSerialization.SerializeToString(credential);
		string label = $"{_serviceName}:{persona}";

		IntPtr error = IntPtr.Zero;
		bool stored = NativeMethods.secret_password_store_sync(
			Schema.Handle,
			IntPtr.Zero,
			label,
			value,
			IntPtr.Zero,
			ref error,
			"service", _serviceName,
			"account", persona.ToString(),
			IntPtr.Zero);

		ThrowIfError(error, "secret_password_store_sync");

		if (!stored)
		{
			throw new CredentialStoreException($"secret_password_store_sync returned false for '{persona}'.");
		}
	}

	/// <inheritdoc/>
	public bool Remove(PersonaGUID persona)
	{
		ArgumentNullException.ThrowIfNull(persona);

		IntPtr error = IntPtr.Zero;
		bool removed = NativeMethods.secret_password_clear_sync(
			Schema.Handle,
			IntPtr.Zero,
			ref error,
			"service", _serviceName,
			"account", persona.ToString(),
			IntPtr.Zero);

		ThrowIfError(error, "secret_password_clear_sync");
		return removed;
	}

	private static void ThrowIfError(IntPtr error, string operation)
	{
		if (error == IntPtr.Zero)
		{
			return;
		}
		string? message = null;
		try
		{
			IntPtr messagePtr = Marshal.ReadIntPtr(error, IntPtr.Size * 2);
			message = Marshal.PtrToStringUTF8(messagePtr);
		}
		catch
		{
			// Best-effort: message extraction shouldn't mask the real failure.
		}
		finally
		{
			NativeMethods.g_error_free(error);
		}

		throw new CredentialStoreException($"{operation} failed: {message ?? "<no detail>"}");
	}

	private static class Schema
	{
		internal static readonly IntPtr Handle = NativeMethods.secret_schema_new(
			"dev.ktsu.CredentialCache",
			flags: 0,
			"service", 0,
			"account", 0,
			IntPtr.Zero);
	}

	private static class NativeMethods
	{
		private const string Lib = "libsecret-1.so.0";

		[DllImport(Lib, CharSet = CharSet.Ansi)]
		internal static extern IntPtr secret_schema_new(
			string name, int flags,
			string attribute1Name, int attribute1Type,
			string attribute2Name, int attribute2Type,
			IntPtr terminator);

		[DllImport(Lib, CharSet = CharSet.Ansi)]
		internal static extern IntPtr secret_password_lookup_sync(
			IntPtr schema,
			IntPtr cancellable,
			ref IntPtr error,
			string attribute1Name, string attribute1Value,
			string attribute2Name, string attribute2Value,
			IntPtr terminator);

		[DllImport(Lib, CharSet = CharSet.Ansi)]
		internal static extern void secret_password_free(IntPtr password);

		[DllImport(Lib, CharSet = CharSet.Ansi)]
		internal static extern bool secret_password_store_sync(
			IntPtr schema,
			IntPtr collection,
			string label,
			string password,
			IntPtr cancellable,
			ref IntPtr error,
			string attribute1Name, string attribute1Value,
			string attribute2Name, string attribute2Value,
			IntPtr terminator);

		[DllImport(Lib, CharSet = CharSet.Ansi)]
		internal static extern bool secret_password_clear_sync(
			IntPtr schema,
			IntPtr cancellable,
			ref IntPtr error,
			string attribute1Name, string attribute1Value,
			string attribute2Name, string attribute2Value,
			IntPtr terminator);

		[DllImport("libglib-2.0.so.0")]
		internal static extern void g_error_free(IntPtr error);
	}
}
