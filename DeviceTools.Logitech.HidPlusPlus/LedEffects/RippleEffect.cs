using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.LedEffects;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 11)]
[DebuggerDisplay("{Effect}, Color = {Color}, SpeedOrPeriod = {SpeedOrPeriodInMilliseconds}")]
public readonly struct RippleEffect
{
	private readonly byte _effect;
	private readonly Color _color;
	private readonly byte _unused;
	private readonly byte _speedOrPeriod0;
	private readonly byte _speedOrPeriod1;

	public PredefinedEffect Effect => (PredefinedEffect)_effect;
	public Color Color => _color;
	public ushort SpeedOrPeriodInMilliseconds => BigEndian.ReadUInt16(in _speedOrPeriod0);
}
