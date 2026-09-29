// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CredentialCache.Test;

using System.Runtime.InteropServices;
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
}
