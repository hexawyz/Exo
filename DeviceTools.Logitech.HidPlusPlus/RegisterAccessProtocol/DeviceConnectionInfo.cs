using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1)]
public struct DeviceConnectionInfo
{
	private byte _deviceInfo;

	public DeviceConnectionInfo(DeviceType deviceType)
		: this(deviceType, 0)
	{
	}

	public DeviceConnectionInfo(DeviceType deviceType, DeviceConnectionFlags connectionFlags)
	{
		if (((byte)deviceType & 0xF0) != 0) throw new ArgumentOutOfRangeException(nameof(deviceType));
		if (((byte)connectionFlags & 0x0F) != 0) throw new ArgumentOutOfRangeException(nameof(connectionFlags));

		_deviceInfo = (byte)((byte)deviceType | (byte)connectionFlags);
	}

	public DeviceType DeviceType
	{
		readonly get => (DeviceType)(_deviceInfo & 0x0F);
		set
		{
			if (((byte)value & 0xF0) != 0) throw new ArgumentException();
			_deviceInfo = (byte)(_deviceInfo & 0xF0 | (byte)value);
		}
	}

	public DeviceConnectionFlags ConnectionFlags
	{
		readonly get => (DeviceConnectionFlags)(_deviceInfo & 0xF0);
		set
		{
			if (((byte)value & 0x0F) != 0) throw new ArgumentException();
			_deviceInfo = (byte)(_deviceInfo & 0x0F | (byte)value);
		}
	}

	public readonly bool IsSoftwarePresent => (_deviceInfo & (byte)DeviceConnectionFlags.SoftwarePresent) != 0;
	public readonly bool IsLinkEncrypted => (_deviceInfo & (byte)DeviceConnectionFlags.LinkEncrypted) != 0;
	public readonly bool IsLinkEstablished => (_deviceInfo & (byte)DeviceConnectionFlags.LinkNotEstablished) == 0;
	public readonly bool IsPacketWithPayload => (_deviceInfo & (byte)DeviceConnectionFlags.PacketWithPayload) != 0;

	public static explicit operator byte(DeviceConnectionInfo value) => value._deviceInfo;
	public static explicit operator DeviceConnectionInfo(byte value) => Unsafe.As<byte, DeviceConnectionInfo>(ref value);
}

