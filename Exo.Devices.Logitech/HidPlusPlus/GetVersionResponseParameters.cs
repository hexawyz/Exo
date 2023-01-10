namespace Exo.Devices.Logitech.HidPlusPlus;

public struct GetVersionResponseParameters : IMessageParameters
{
	public byte Major;
	public byte Minor;
	public byte Beacon;
}
