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

	public DeviceConnectionInfo DeviceInfo
	{
		readonly get => (DeviceConnectionInfo)_deviceInfo;
		set => _deviceInfo = (byte)value;
	}

	public ProtocolType ProtocolType
	{
		readonly get => (ProtocolType)_protocolType;
		set => _protocolType = (byte)value;
	}

	public ushort WirelessProductId
	{
		readonly get => LittleEndian.ReadUInt16(_wirelessProductId0);
		set => LittleEndian.Write(ref _wirelessProductId0, value);
	}
}
