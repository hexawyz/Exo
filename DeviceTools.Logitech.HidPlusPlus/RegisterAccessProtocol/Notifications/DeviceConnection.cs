using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Notifications;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
internal struct DeviceConnectionParameters
{
	private byte _protocolType;
	private byte _deviceInfo;
#pragma warning disable IDE0044 // Add readonly modifier
	private byte _wirelessProductId0;
	private byte _wirelessProductId1;
#pragma warning restore IDE0044 // Add readonly modifier

	public DeviceType DeviceType
	{
		get => (DeviceType)(_deviceInfo & 0x0F);
		set
		{
			if (((byte)value & 0xF0) != 0) throw new ArgumentException();
			_deviceInfo = (byte)(_deviceInfo & 0xF0 | (byte)value);
		}
	}

	public DeviceConnectionFlags ConnectionFlags
	{
		get => (DeviceConnectionFlags)(_deviceInfo & 0xF0);
		set
		{
			if (((byte)value & 0x0F) != 0) throw new ArgumentException();
			_deviceInfo = (byte)(_deviceInfo & 0x0F | (byte)value);
		}
	}

	public bool IsSoftwarePresent => (_deviceInfo & (byte)DeviceConnectionFlags.SoftwarePresent) != 0;
	public bool IsLinkEncrypted => (_deviceInfo & (byte)DeviceConnectionFlags.LinkEncrypted) != 0;
	public bool IsLinkEstablished => (_deviceInfo & (byte)DeviceConnectionFlags.LinkEstablished) != 0;
	public bool IsPacketWithPayload => (_deviceInfo & (byte)DeviceConnectionFlags.PacketWithPayload) != 0;

	public ProtocolType ProtocolType
	{
		get => (ProtocolType)_protocolType;
		set => _protocolType = (byte)value;
	}

	public ushort WirelessProductId
	{
		get => LittleEndian.ReadUInt16(_wirelessProductId0);
		set => LittleEndian.Write(ref _wirelessProductId0, value);
	}
}

