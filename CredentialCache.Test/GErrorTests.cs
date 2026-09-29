// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ktsu.CredentialCache.Storage;

/// <summary>
/// Covers reading the message out of a glib <c>GError</c>, which the Linux store does on
/// every libsecret failure. The GError here is hand-built, so this runs on every platform.
/// </summary>
[TestClass]
public class GErrorTests
{
	// GError is { guint32 domain; gint code; gchar *message; }, so the message pointer
	// sits at offset 8 on both 32-bit and 64-bit, with no padding before it.
	private const int MessageOffset = sizeof(uint) + sizeof(int);

	[TestMethod]
	public void ReadMessageReturnsTheMessageField()
	{
		IntPtr message = Marshal.StringToCoTaskMemUTF8("Cannot autolaunch D-Bus without X11 $DISPLAY");

		// One pointer longer than the struct and zeroed, so a read past the end of the struct
		// finds a null pointer rather than whatever happens to follow the allocation.
		int size = MessageOffset + (IntPtr.Size * 2);
		IntPtr error = Marshal.AllocHGlobal(size);
		try
		{
			Marshal.Copy(new byte[size], 0, error, size);
			Marshal.WriteInt32(error, 0, 42);
			Marshal.WriteInt32(error, sizeof(uint), 7);
			Marshal.WriteIntPtr(error, MessageOffset, message);

			Assert.AreEqual("Cannot autolaunch D-Bus without X11 $DISPLAY", GError.ReadMessage(error));
		}
		finally
		{
			Marshal.FreeHGlobal(error);
			Marshal.FreeCoTaskMem(message);
		}
	}

	[TestMethod]
	public void ReadMessageReturnsNullForANullMessage()
	{
		int size = MessageOffset + (IntPtr.Size * 2);
		IntPtr error = Marshal.AllocHGlobal(size);
		try
		{
			Marshal.Copy(new byte[size], 0, error, size);

			Assert.IsNull(GError.ReadMessage(error));
		}
		finally
		{
			Marshal.FreeHGlobal(error);
		}
	}

	[TestMethod]
	public void ThrowIfErrorReportsTheMessageOfARealGError()
	{
		if (!OperatingSystem.IsLinux())
		{
			Assert.Inconclusive("GError comes from glib, which is only loaded on Linux.");
			return;
		}

		AssertThrowIfErrorReportsTheMessage();
	}

	// Built by glib itself rather than by hand, so this checks the real layout. glib frees
	// the error inside ThrowIfError.
	[SupportedOSPlatform("linux")]
	private static void AssertThrowIfErrorReportsTheMessage()
	{
		IntPtr glib = NativeLibrary.Load("libglib-2.0.so.0");
		GErrorNewLiteral newLiteral = Marshal.GetDelegateForFunctionPointer<GErrorNewLiteral>(
			NativeLibrary.GetExport(glib, "g_error_new_literal"));
		IntPtr error = newLiteral(1, 2, "Cannot autolaunch D-Bus without X11 $DISPLAY");

		CredentialStoreException exception = Assert.ThrowsExactly<CredentialStoreException>(
			() => LinuxSecretServiceCredentialStore.ThrowIfError(error, "secret_password_lookup_sync"));

		Assert.AreEqual("secret_password_lookup_sync failed: Cannot autolaunch D-Bus without X11 $DISPLAY", exception.Message);
	}

	private delegate IntPtr GErrorNewLiteral(uint domain, int code, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
}
