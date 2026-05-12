// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache.Storage;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

/// <summary>
/// A credential store backed by the macOS Keychain Services API. Each
/// <see cref="PersonaGUID"/> is stored as a <c>kSecClassGenericPassword</c> item with the
/// service name set to <see cref="DefaultServiceName"/> (overridable) and the account
/// name set to the persona GUID.
/// </summary>
[SupportedOSPlatform("osx")]
internal sealed class MacOsCredentialStore : ICredentialStore
{
	internal const string DefaultServiceName = "ktsu.CredentialCache";

	private readonly string _serviceName;

	public MacOsCredentialStore(string serviceName = DefaultServiceName)
	{
		ArgumentException.ThrowIfNullOrEmpty(serviceName);
		_serviceName = serviceName;
	}

	/// <inheritdoc/>
	public string Name => "macOS Keychain";

	/// <inheritdoc/>
	public bool TryLoad(PersonaGUID persona, out Credential? credential)
	{
		ArgumentNullException.ThrowIfNull(persona);
		credential = null;

		byte[] account = Encoding.UTF8.GetBytes(persona.ToString());
		byte[] service = Encoding.UTF8.GetBytes(_serviceName);

		int status = NativeMethods.SecKeychainFindGenericPassword(
			keychainOrArray: IntPtr.Zero,
			serviceNameLength: (uint)service.Length, serviceName: service,
			accountNameLength: (uint)account.Length, accountName: account,
			passwordLength: out uint length, passwordData: out IntPtr passwordPtr,
			itemRef: IntPtr.Zero);

		if (status == NativeMethods.errSecItemNotFound)
		{
			return false;
		}
		if (status != NativeMethods.errSecSuccess)
		{
			throw new CredentialStoreException($"SecKeychainFindGenericPassword failed ({status}).");
		}

		try
		{
			byte[] blob = new byte[length];
			Marshal.Copy(passwordPtr, blob, 0, (int)length);
			credential = CredentialSerialization.Deserialize(blob);
			Array.Clear(blob, 0, blob.Length);
			return credential is not null;
		}
		finally
		{
			_ = NativeMethods.SecKeychainItemFreeContent(IntPtr.Zero, passwordPtr);
		}
	}

	/// <inheritdoc/>
	public void Save(PersonaGUID persona, Credential credential)
	{
		ArgumentNullException.ThrowIfNull(persona);
		ArgumentNullException.ThrowIfNull(credential);

		byte[] account = Encoding.UTF8.GetBytes(persona.ToString());
		byte[] service = Encoding.UTF8.GetBytes(_serviceName);
		byte[] blob = CredentialSerialization.Serialize(credential);

		try
		{
			int findStatus = NativeMethods.SecKeychainFindGenericPasswordWithRef(
				keychainOrArray: IntPtr.Zero,
				serviceNameLength: (uint)service.Length, serviceName: service,
				accountNameLength: (uint)account.Length, accountName: account,
				passwordLength: out _, passwordData: out IntPtr existingPtr,
				itemRef: out IntPtr itemRef);

			if (findStatus == NativeMethods.errSecSuccess)
			{
				try
				{
					int modifyStatus = NativeMethods.SecKeychainItemModifyAttributesAndData(
						itemRef, attrList: IntPtr.Zero,
						length: (uint)blob.Length, data: blob);
					if (modifyStatus != NativeMethods.errSecSuccess)
					{
						throw new CredentialStoreException(
							$"SecKeychainItemModifyAttributesAndData failed ({modifyStatus}).");
					}
				}
				finally
				{
					if (existingPtr != IntPtr.Zero)
					{
						_ = NativeMethods.SecKeychainItemFreeContent(IntPtr.Zero, existingPtr);
					}
					if (itemRef != IntPtr.Zero)
					{
						NativeMethods.CFRelease(itemRef);
					}
				}
				return;
			}
			if (findStatus != NativeMethods.errSecItemNotFound)
			{
				throw new CredentialStoreException($"SecKeychainFindGenericPassword failed ({findStatus}).");
			}

			int addStatus = NativeMethods.SecKeychainAddGenericPassword(
				keychain: IntPtr.Zero,
				serviceNameLength: (uint)service.Length, serviceName: service,
				accountNameLength: (uint)account.Length, accountName: account,
				passwordLength: (uint)blob.Length, passwordData: blob,
				itemRef: IntPtr.Zero);

			if (addStatus != NativeMethods.errSecSuccess)
			{
				throw new CredentialStoreException($"SecKeychainAddGenericPassword failed ({addStatus}).");
			}
		}
		finally
		{
			Array.Clear(blob, 0, blob.Length);
		}
	}

	/// <inheritdoc/>
	public bool Remove(PersonaGUID persona)
	{
		ArgumentNullException.ThrowIfNull(persona);

		byte[] account = Encoding.UTF8.GetBytes(persona.ToString());
		byte[] service = Encoding.UTF8.GetBytes(_serviceName);

		int findStatus = NativeMethods.SecKeychainFindGenericPasswordWithRef(
			keychainOrArray: IntPtr.Zero,
			serviceNameLength: (uint)service.Length, serviceName: service,
			accountNameLength: (uint)account.Length, accountName: account,
			passwordLength: out _, passwordData: out IntPtr passwordPtr,
			itemRef: out IntPtr itemRef);

		if (findStatus == NativeMethods.errSecItemNotFound)
		{
			return false;
		}
		if (findStatus != NativeMethods.errSecSuccess)
		{
			throw new CredentialStoreException($"SecKeychainFindGenericPassword failed ({findStatus}).");
		}

		try
		{
			int deleteStatus = NativeMethods.SecKeychainItemDelete(itemRef);
			if (deleteStatus != NativeMethods.errSecSuccess)
			{
				throw new CredentialStoreException($"SecKeychainItemDelete failed ({deleteStatus}).");
			}
			return true;
		}
		finally
		{
			if (passwordPtr != IntPtr.Zero)
			{
				_ = NativeMethods.SecKeychainItemFreeContent(IntPtr.Zero, passwordPtr);
			}
			if (itemRef != IntPtr.Zero)
			{
				NativeMethods.CFRelease(itemRef);
			}
		}
	}

	private static class NativeMethods
	{
		internal const int errSecSuccess = 0;
		internal const int errSecItemNotFound = -25300;

		private const string Security = "/System/Library/Frameworks/Security.framework/Security";
		private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

		[DllImport(Security)]
		internal static extern int SecKeychainFindGenericPassword(
			IntPtr keychainOrArray,
			uint serviceNameLength, byte[] serviceName,
			uint accountNameLength, byte[] accountName,
			out uint passwordLength, out IntPtr passwordData,
			IntPtr itemRef);

		[DllImport(Security, EntryPoint = "SecKeychainFindGenericPassword")]
		internal static extern int SecKeychainFindGenericPasswordWithRef(
			IntPtr keychainOrArray,
			uint serviceNameLength, byte[] serviceName,
			uint accountNameLength, byte[] accountName,
			out uint passwordLength, out IntPtr passwordData,
			out IntPtr itemRef);

		[DllImport(Security)]
		internal static extern int SecKeychainAddGenericPassword(
			IntPtr keychain,
			uint serviceNameLength, byte[] serviceName,
			uint accountNameLength, byte[] accountName,
			uint passwordLength, byte[] passwordData,
			IntPtr itemRef);

		[DllImport(Security)]
		internal static extern int SecKeychainItemModifyAttributesAndData(
			IntPtr itemRef, IntPtr attrList,
			uint length, byte[] data);

		[DllImport(Security)]
		internal static extern int SecKeychainItemDelete(IntPtr itemRef);

		[DllImport(Security)]
		internal static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

		[DllImport(CoreFoundation)]
		internal static extern void CFRelease(IntPtr cf);
	}
}
