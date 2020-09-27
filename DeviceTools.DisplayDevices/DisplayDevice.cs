using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeviceTools.DisplayDevices
{
	public abstract class DisplayDevice
	{
		public string DeviceName { get; }
		public string Description { get; }
		public string DeviceId { get; }
		public string RegistryPath { get; }
		private protected readonly DisplayDeviceFlags Flags;

		private protected bool HasFlag(DisplayDeviceFlags flag)
			=> (Flags & flag) == flag;

		private protected DisplayDevice(string deviceName, string description, string deviceId, string registryKey, DisplayDeviceFlags flags)
		{
			DeviceName = deviceName;
			Description = description;
			DeviceId = deviceId;
			RegistryPath = registryKey;
			Flags = flags;
		}
	}

	public class DisplayAdapterDevice : DisplayDevice
	{
		public static IEnumerable<DisplayAdapterDevice> GetAll(bool onlyAttachedToDesktop = true)
		{
			uint i = 0;
			while (true)
			{
				var displayDevice = new NativeMethods.DisplayDevice { Size = Unsafe.SizeOf<NativeMethods.DisplayDevice>() };
				if (NativeMethods.EnumDisplayDevices(null, i, ref displayDevice, 0) == 0)
				{
					yield break;
				}
				if (!onlyAttachedToDesktop || (displayDevice.StateFlags & DisplayDeviceFlags.AttachedToDesktop) != 0)
				{
					yield return new DisplayAdapterDevice
					(
						displayDevice.DeviceName.ToString(),
						displayDevice.DeviceString.ToString(),
						displayDevice.DeviceId.ToString(),
						displayDevice.DeviceKey.ToString(),
						displayDevice.StateFlags
					);
				}
				i++;
			}
		}

		private DisplayAdapterDevice(string deviceName, string description, string deviceId, string registryKey, DisplayDeviceFlags flags)
			: base(deviceName, description, deviceId, registryKey, flags)
		{
		}

		public bool IsAttachedToDesktop => HasFlag(DisplayDeviceFlags.AttachedToDesktop);
		public bool IsPrimaryDevice => HasFlag(DisplayDeviceFlags.PrimaryDevice);

		public IEnumerable<MonitorDevice> GetMonitors(bool onlyActiveAndAttached = true)
			=> MonitorDevice.GetAll(DeviceName, onlyActiveAndAttached);
	}

	public class MonitorDevice : DisplayDevice
	{
		internal static IEnumerable<MonitorDevice> GetAll(string device, bool onlyActiveAndAttached)
		{
			uint i = 0;
			while (true)
			{
				var displayDevice = new NativeMethods.DisplayDevice { Size = Unsafe.SizeOf<NativeMethods.DisplayDevice>() };
				if (NativeMethods.EnumDisplayDevices(device, i, ref displayDevice, 0) == 0)
				{
					yield break;
				}
				if (!onlyActiveAndAttached || (displayDevice.StateFlags & (DisplayDeviceFlags.Active | DisplayDeviceFlags.Attached)) == (DisplayDeviceFlags.Active | DisplayDeviceFlags.Attached))
				{
					yield return new MonitorDevice
					(
						displayDevice.DeviceName.ToString(),
						displayDevice.DeviceString.ToString(),
						displayDevice.DeviceId.ToString(),
						displayDevice.DeviceKey.ToString(),
						displayDevice.StateFlags
					);
				}
				i++;
			}
		}

		private MonitorDevice(string deviceName, string description, string deviceId, string registryKey, DisplayDeviceFlags flags)
			: base(deviceName, description, deviceId, registryKey, flags)
		{
		}

		public bool IsActive => HasFlag(DisplayDeviceFlags.Active);
		public bool IsAttached => HasFlag(DisplayDeviceFlags.Attached);
	}
}
