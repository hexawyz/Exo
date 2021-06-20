using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using DeviceTools.DisplayDevices.Mccs;

namespace DeviceTools.DisplayDevices
{
	public sealed class PhysicalMonitor : IDisposable
	{
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public SafePhysicalMonitorHandle Handle { get; }
		public string Description { get; }

		public PhysicalMonitor(SafePhysicalMonitorHandle handle, string description)
		{
			Handle = handle;
			Description = description;
		}

		public void Dispose()
		{
			Handle.Dispose();
		}

		public ReadOnlySpan<byte> GetCapabilitiesUtf8String()
		{
			if (NativeMethods.GetCapabilitiesStringLength(Handle, out uint capabilitesStringLength) == 0)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}

			if (capabilitesStringLength == 0)
			{
				return default;
			}

			var buffer = new byte[capabilitesStringLength];

			if (NativeMethods.CapabilitiesRequestAndCapabilitiesReply(Handle, ref buffer[0], capabilitesStringLength) == 0)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}

			// Some capabilities string seem to be returned with trailing null characters, so we trim them to be sure 😑
			return buffer.AsSpan(0, buffer.Length - 1).TrimEnd((byte)0);
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
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}

			return new VcpFeatureReply(currentValue, maximumValue);
		}

		public void SaveCurrentSettings()
		{
			if (NativeMethods.SaveCurrentSettings(Handle) == 0)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}
	}
}
