using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.LedEffects;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 11)]
[DebuggerDisplay("{Effect}, PressColor = {PressColor}, ReleaseColor = {ReleaseColor}, DelayInMilliseconds = {DelayInMilliseconds}")]
public readonly struct LightOnPressEffect
{
	private readonly byte _effect;
	private readonly Color _pressColor;
	private readonly Color _releaseColor;
	private readonly byte _delay0;
	private readonly byte _delay1;

	public PredefinedEffect Effect => (PredefinedEffect)_effect;
	public Color PressColor => _pressColor;
	public Color ReleaseColor => _releaseColor;
	public ushort DelayInMilliseconds => BigEndian.ReadUInt16(in _delay0);
}
