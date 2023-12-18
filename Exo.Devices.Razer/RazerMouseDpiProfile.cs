namespace Exo.Devices.Razer;

public record struct RazerMouseDpiProfile
{
	public RazerMouseDpiProfile(ushort x, ushort y, ushort z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	public ushort X { get; }
	public ushort Y { get; }
	public ushort Z { get; }
}
