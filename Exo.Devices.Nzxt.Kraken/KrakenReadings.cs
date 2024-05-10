namespace Exo.Devices.Nzxt.Kraken;

internal readonly struct KrakenReadings
{
	private readonly ulong _rawValue;

	public byte LiquidTemperature => (byte)(_rawValue >> 48);
	public byte FanPower => (byte)(_rawValue >> 40);
	public byte PumpPower => (byte)(_rawValue >> 32);
	public ushort FanSpeed => (ushort)(_rawValue >> 16);
	public ushort PumpSpeed => (ushort)_rawValue;

	internal KrakenReadings(ushort pumpSpeed, ushort fanSpeed, byte pumpPower, byte fanPower, byte liquidTemperature)
	{
		_rawValue = pumpSpeed | (uint)fanSpeed << 16 | (ulong)pumpPower << 32 | (ulong)fanPower << 40 | (ulong)liquidTemperature << 48;
	}
}
