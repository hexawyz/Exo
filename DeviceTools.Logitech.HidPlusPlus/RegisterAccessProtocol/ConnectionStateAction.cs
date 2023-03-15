namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

[Flags]
public enum ConnectionStateAction : byte
{
	FakeDeviceArrival = 0x02,
}
