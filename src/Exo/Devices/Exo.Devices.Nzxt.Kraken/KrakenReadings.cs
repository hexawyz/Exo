namespace Exo.Devices.Nzxt.Kraken;

internal readonly struct KrakenReadings
{
	private readonly byte _liquidTemperature;
	private readonly byte _liquidTemperatureDecimal;
	private readonly byte _fanPower;
	private readonly byte _pumpPower;
	private readonly ushort _fanSpeed;
	private readonly ushort _pumpSpeed;

	public KrakenReadings(byte liquidTemperature, byte liquidTemperatureDecimal, byte fanPower, byte pumpPower, ushort fanSpeed, ushort pumpSpeed)
	{
		_liquidTemperature = liquidTemperature;
		_liquidTemperatureDecimal = liquidTemperatureDecimal;
		_fanPower = fanPower;
		_pumpPower = pumpPower;
		_fanSpeed = fanSpeed;
		_pumpSpeed = pumpSpeed;
	}

	public byte LiquidTemperature => _liquidTemperature;
	public byte LiquidTemperatureDecimal => _liquidTemperatureDecimal;
	public byte FanPower => _fanPower;
	public byte PumpPower => _pumpPower;
	public ushort FanSpeed => _fanSpeed;
	public ushort PumpSpeed => _pumpSpeed;
}
