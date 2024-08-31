namespace Exo.Devices.Lg.Monitors;

public readonly struct SimpleVersion
{
	public SimpleVersion(byte major, byte minor)
	{
		Major = major;
		Minor = minor;
	}

	public byte Major { get; }
	public byte Minor { get; }

	public override string ToString() => $"{Major}.{Minor}";
}
