// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache.Storage;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ktsu.Semantics.Strings;

/// <summary>
/// A credential store backed by Windows Credential Manager (advapi32). Each
/// <see cref="PersonaGUID"/> is stored as a generic credential whose target name is
/// <c>{ServicePrefix}:{persona}</c> and whose <c>CredentialBlob</c> is the UTF-8 JSON
/// representation of the <see cref="Credential"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsCredentialStore : ISearchableCredentialStore
{
	internal const string DefaultServicePrefix = "ktsu.CredentialCache";

	private readonly string _servicePrefix;

	public WindowsCredentialStore(string servicePrefix = DefaultServicePrefix)
	{
		ArgumentException.ThrowIfNullOrEmpty(servicePrefix);
		_servicePrefix = servicePrefix;
	}

	/// <inheritdoc/>
	public string Name => "Windows Credential Manager";

	private string TargetFor(PersonaGUID persona) => $"{_servicePrefix}:{persona}";

	/// <inheritdoc/>
	public bool TryLoad(PersonaGUID persona, out Credential? credential)
	{
		ArgumentNullException.ThrowIfNull(persona);
		credential = null;

		if (!NativeMethods.CredRead(TargetFor(persona), NativeMethods.CRED_TYPE_GENERIC, 0, out nint handle))
		{
			int err = Marshal.GetLastWin32Error();
			if (err == NativeMethods.ERROR_NOT_FOUND)
			{
				return false;
			}
			throw new CredentialStoreException($"CredRead failed for '{persona}'.", new Win32Exception(err));
		}

		try
		{
			NativeMethods.CREDENTIAL cred = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(handle);
			if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0)
			{
				return false;
			}

			byte[] blob = new byte[cred.CredentialBlobSize];
			Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);
			credential = CredentialSerialization.Deserialize(blob);
			return credential is not null;
		}
		finally
		{
			NativeMethods.CredFree(handle);
		}
	}

	/// <inheritdoc/>
	public void Save(PersonaGUID persona, Credential credential)
	{
		ArgumentNullException.ThrowIfNull(persona);
		ArgumentNullException.ThrowIfNull(credential);

		byte[] blob = CredentialSerialization.Serialize(credential);
		if (blob.Length > NativeMethods.CRED_MAX_CREDENTIAL_BLOB_SIZE)
		{
			throw new CredentialStoreException(
				$"Credential exceeds the Windows Credential Manager blob size limit of " +
				$"{NativeMethods.CRED_MAX_CREDENTIAL_BLOB_SIZE} bytes (was {blob.Length}).");
		}

		IntPtr blobPtr = Marshal.AllocHGlobal(blob.Length);
		try
		{
			Marshal.Copy(blob, 0, blobPtr, blob.Length);

			NativeMethods.CREDENTIAL native = new()
			{
				Type = NativeMethods.CRED_TYPE_GENERIC,
				TargetName = TargetFor(persona),
				CredentialBlob = blobPtr,
				CredentialBlobSize = blob.Length,
				Persist = NativeMethods.CRED_PERSIST_LOCAL_MACHINE,
				UserName = Environment.UserName,
			};

			if (!NativeMethods.CredWrite(ref native, 0))
			{
				int err = Marshal.GetLastWin32Error();
				throw new CredentialStoreException($"CredWrite failed for '{persona}'.", new Win32Exception(err));
			}
		}
		finally
		{
			Marshal.FreeHGlobal(blobPtr);
			Array.Clear(blob, 0, blob.Length);
		}
	}

	/// <inheritdoc/>
	public bool Remove(PersonaGUID persona)
	{
		ArgumentNullException.ThrowIfNull(persona);
		if (NativeMethods.CredDelete(TargetFor(persona), NativeMethods.CRED_TYPE_GENERIC, 0))
		{
			return true;
		}
		int err = Marshal.GetLastWin32Error();
		if (err == NativeMethods.ERROR_NOT_FOUND)
		{
			return false;
		}
		throw new CredentialStoreException($"CredDelete failed for '{persona}'.", new Win32Exception(err));
	}

	/// <inheritdoc/>
	public IEnumerable<PersonaGUID> EnumerateKeys()
	{
		string filter = $"{_servicePrefix}:*";
		if (!NativeMethods.CredEnumerate(filter, 0, out int count, out IntPtr credentialsPtr))
		{
			int err = Marshal.GetLastWin32Error();
			if (err == NativeMethods.ERROR_NOT_FOUND)
			{
				return [];
			}
			throw new CredentialStoreException("CredEnumerate failed.", new Win32Exception(err));
		}

		try
		{
			List<PersonaGUID> keys = new(count);
			int ptrSize = Marshal.SizeOf<IntPtr>();
			string prefix = $"{_servicePrefix}:";

			for (int i = 0; i < count; i++)
			{
				IntPtr credPtr = Marshal.ReadIntPtr(credentialsPtr, i * ptrSize);
				NativeMethods.CREDENTIAL native = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(credPtr);
				string? target = native.TargetName;
				if (target is null || !target.StartsWith(prefix, StringComparison.Ordinal))
				{
					continue;
				}
				string personaText = target[prefix.Length..];
				keys.Add(SemanticString<PersonaGUID>.Create(personaText));
			}
			return keys;
		}
		finally
		{
			NativeMethods.CredFree(credentialsPtr);
		}
	}

	private static class NativeMethods
	{
		internal const int CRED_TYPE_GENERIC = 1;
		internal const int CRED_PERSIST_LOCAL_MACHINE = 2;
		internal const int ERROR_NOT_FOUND = 1168;
		internal const int CRED_MAX_CREDENTIAL_BLOB_SIZE = 5 * 512;

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct CREDENTIAL
		{
			public int Flags;
			public int Type;
			[MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
			[MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
			public long LastWritten;
			public int CredentialBlobSize;
			public IntPtr CredentialBlob;
			public int Persist;
			public int AttributeCount;
			public IntPtr Attributes;
			[MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
			[MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
		}

		[DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

		[DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CredWrite([In] ref CREDENTIAL credential, [In] uint flags);

		[DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CredDelete(string target, int type, int reservedFlag);

		[DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CredEnumerate(string? filter, int flag, out int count, out IntPtr credentialsPtr);

		[DllImport("advapi32.dll")]
		internal static extern void CredFree([In] IntPtr cred);
	}
}
