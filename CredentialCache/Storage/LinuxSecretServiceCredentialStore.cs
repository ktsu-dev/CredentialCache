// Copyright (c) 2023-2026 ktsu-dev contributors

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
/// <para>
/// Requires libsecret to be installed on the host. On headless systems without
/// a running secret-service implementation (e.g. minimal containers, build agents)
/// this provider throws <see cref="CredentialStoreException"/> from every operation;
/// consumers should catch it and fall back to <see cref="InMemoryCredentialStore"/> if
/// appropriate. A missing <c>libsecret-1.so.0</c> surfaces the same way, with the
/// <see cref="DllNotFoundException"/> as its inner exception.
/// </para>
/// <para>
/// Plaintext credential bytes are scrubbed on the same terms as the Windows and macOS
/// stores: every copy this code owns is a <see langword="byte"/> array or a
/// <see cref="NativeSecretBuffer"/> that is zeroed on the way out, and no plaintext is
/// ever marshalled through a managed <see cref="string"/>. libsecret scrubs its own
/// copy in <c>secret_password_free</c>.
/// </para>
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
			GetSchemaHandle(),
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
			// Read as bytes and scrubbed, never through Marshal.PtrToStringUTF8: an immutable
			// string cannot be scrubbed, so the plaintext would outlive the call. Same
			// byte-array-and-scrub path the Windows and macOS stores take; libsecret just hands
			// back a C string rather than a pointer and a length.
			credential = NativeSecretBuffer.ReadCredential(passwordPtr);
			return credential is not null;
		}
		finally
		{
			// secret_password_free wipes libsecret's own copy before releasing it.
			NativeMethods.secret_password_free(passwordPtr);
		}
	}

	/// <inheritdoc/>
	public void Save(PersonaGUID persona, Credential credential)
	{
		ArgumentNullException.ThrowIfNull(persona);
		ArgumentNullException.ThrowIfNull(credential);

		// An unmanaged nul-terminated copy this code owns and zeroes, not a managed string:
		// marshalling a string would put the plaintext on the managed heap where it cannot be
		// scrubbed, and leave the runtime's own native copy to be freed unscrubbed.
		using NativeSecretBuffer value = NativeSecretBuffer.OfCredential(credential);
		string label = $"{_serviceName}:{persona}";

		IntPtr error = IntPtr.Zero;
		bool stored = NativeMethods.secret_password_store_sync(
			GetSchemaHandle(),
			IntPtr.Zero,
			label,
			value.Pointer,
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
			GetSchemaHandle(),
			IntPtr.Zero,
			ref error,
			"service", _serviceName,
			"account", persona.ToString(),
			IntPtr.Zero);

		ThrowIfError(error, "secret_password_clear_sync");
		return removed;
	}

	internal static void ThrowIfError(IntPtr error, string operation)
	{
		if (error == IntPtr.Zero)
		{
			return;
		}
		string? message = null;
		try
		{
			message = GError.ReadMessage(error);
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

	private static readonly Lock SchemaLock = new();
	private static IntPtr schemaHandle;

	/// <summary>
	/// Builds the schema on first use rather than in a type initializer: a load failure
	/// there would reach callers as a <see cref="TypeInitializationException"/> on every
	/// call, which nobody would think to catch.
	/// </summary>
	private static IntPtr GetSchemaHandle()
	{
		lock (SchemaLock)
		{
			if (schemaHandle == IntPtr.Zero)
			{
				schemaHandle = TranslateMissingLibrary(() => NativeMethods.secret_schema_new(
					"dev.ktsu.CredentialCache",
					flags: 0,
					"service", 0,
					"account", 0,
					IntPtr.Zero));
			}

			return schemaHandle;
		}
	}

	/// <summary>
	/// Runs a native call, turning a missing or incompatible libsecret into the store's
	/// documented <see cref="CredentialStoreException"/>.
	/// </summary>
	internal static T TranslateMissingLibrary<T>(Func<T> nativeCall)
	{
		try
		{
			return nativeCall();
		}
		catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
		{
			throw new CredentialStoreException(
				$"libsecret-1.so.0 could not be loaded. Install libsecret-1-0 and a Secret Service implementation, or use {nameof(InMemoryCredentialStore)}.",
				ex);
		}
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

		// password is an IntPtr, not a string, so the plaintext is never marshalled by the
		// runtime into a copy it frees without scrubbing. The caller owns a
		// NativeSecretBuffer for it.
		[DllImport(Lib, CharSet = CharSet.Ansi)]
		internal static extern bool secret_password_store_sync(
			IntPtr schema,
			IntPtr collection,
			string label,
			IntPtr password,
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
