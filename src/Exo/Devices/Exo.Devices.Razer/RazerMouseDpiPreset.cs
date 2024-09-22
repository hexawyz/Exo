using System.Diagnostics;

namespace Exo.Devices.Razer;

[DebuggerDisplay("{X,d}x{Y,d}x{Z,d}")]
public record struct RazerMouseDpiPreset
{
	public RazerMouseDpiPreset(ushort x, ushort y, ushort z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	public ushort X { get; }
	public ushort Y { get; }
	public ushort Z { get; }
}
