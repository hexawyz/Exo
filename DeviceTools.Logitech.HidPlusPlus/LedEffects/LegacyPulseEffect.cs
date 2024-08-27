using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.LedEffects;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 11)]
[DebuggerDisplay("{Effect}, Color = {Color}, SpeedInMilliseconds = {SpeedInMilliseconds}")]
public readonly struct LegacyPulseEffect
{
	private readonly byte _effect;
	private readonly Color _color;
	private readonly byte _speedInMilliseconds;

	public PredefinedEffect Effect => (PredefinedEffect)_effect;
	public Color Color => _color;
	public byte SpeedInMilliseconds => _speedInMilliseconds;
}
