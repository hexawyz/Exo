using System.ComponentModel;
using System.Runtime.InteropServices;
using DeviceTools.DisplayDevices.Mccs;

namespace DeviceTools.DisplayDevices;

public sealed class PhysicalMonitor : IDisposable
{
	/// <summary>Gets the native handle associated with this monitor.</summary>
	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public SafePhysicalMonitorHandle Handle { get; }

	/// <summary>Gets the description associated with this monitor.</summary>
	public string Description { get; }

	internal PhysicalMonitor(SafePhysicalMonitorHandle handle, string description)
	{
		Handle = handle;
		Description = description;
	}

	public void Dispose()
	{
		Handle.Dispose();
	}

	/// <summary>Gets the raw Capabilities string for the monitor.</summary>
	/// <returns>A UTF-8 string describing the monitor capabilities according to MCCI specifications.</returns>
	/// <exception cref="Win32Exception"></exception>
	public unsafe ReadOnlyMemory<byte> GetCapabilitiesUtf8String()
	{
		// NB: We need to have a retry logic in there because monitors can occasionally fail to correctly answer the DDC/CI commands.
		// This is likely due to conflicts with other devices on the bus such as shitty HDCP stuff.
		int retryCount = 1;
		uint capabilitiesStringLength;
		while (true)
		{
			if (NativeMethods.GetCapabilitiesStringLength(Handle, out capabilitiesStringLength) != 0) break;

			var errorCode = Marshal.GetLastWin32Error();
			if (errorCode is NativeMethods.ErrorGraphicsDdcCiInvalidMessageCommand or NativeMethods.ErrorGraphicsDdcCiInvalidMessageChecksum && retryCount > 0)
			{
				retryCount--;
			}
			else
			{
				throw new Win32Exception(errorCode);
			}
		}

		if (capabilitiesStringLength == 0)
		{
			return default;
		}

		var buffer = new byte[capabilitiesStringLength];
		fixed (byte* bufferStart = buffer)
		{
			retryCount = 1;
			while (true)
			{
				if (NativeMethods.CapabilitiesRequestAndCapabilitiesReply(Handle, bufferStart, capabilitiesStringLength) != 0) break;

				var errorCode = Marshal.GetLastWin32Error();
				if (errorCode is NativeMethods.ErrorGraphicsDdcCiInvalidMessageCommand or NativeMethods.ErrorGraphicsDdcCiInvalidMessageChecksum && retryCount > 0)
				{
					retryCount--;
				}
				else
				{
					throw new Win32Exception(errorCode);
				}
			}
		}

		// Some capabilities string seem to be returned with trailing null characters, so we trim them to be sure 😑
		return buffer.AsMemory(0, buffer.Length - 1).TrimEnd((byte)0);
	}

	public void SetVcpFeature(byte vcpCode, uint value)
	{
		if (NativeMethods.SetVcpFeature(Handle, vcpCode, value) == 0)
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}

	public VcpFeatureReply GetVcpFeature(byte vcpCode)
	{
		if (NativeMethods.GetVcpFeatureAndVcpFeatureReply(Handle, vcpCode, out var type, out uint currentValue, out uint maximumValue) == 0)
		{
			switch (Marshal.GetLastWin32Error())
			{
			case NativeMethods.ErrorGraphicsDdcCiVcpNotSupported:
				throw new VcpCodeNotSupportedException();
			case int error:
				throw new Win32Exception(error);
			}
		}

		return new VcpFeatureReply((ushort)currentValue, (ushort)maximumValue, type == NativeMethods.VcpCodeType.Momentary);
	}

	public void SaveCurrentSettings()
	{
		if (NativeMethods.SaveCurrentSettings(Handle) == 0)
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}
}
