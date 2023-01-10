namespace Exo.Devices.Logitech.HidPlusPlus;

public struct GetVersionRequestParameters : IMessageParameters
{
	public ushort Zero;
	public byte Beacon;
}
