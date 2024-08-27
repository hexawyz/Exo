using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.LedEffects;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 11)]
[DebuggerDisplay("{Effect}, StartColor = {StartColor}, StopColor = {StopColor}, SpeedOrPeriodInMilliseconds = {SpeedOrPeriodInMilliseconds}, Direction = {Direction}, Intensity = {Intensity}")]
public readonly struct ColorWaveEffect
{
	private readonly byte _effect;
	private readonly Color _startColor;
	private readonly Color _stopColor;
	private readonly byte _speedOrPeriod1;
	private readonly EffectDirection _direction;
	private readonly byte _intensity;
	private readonly byte _speedOrPeriod0;

	public PredefinedEffect Effect => (PredefinedEffect)_effect;
	public Color StartColor => _startColor;
	public Color StopColor => _stopColor;
	public ushort SpeedOrPeriodInMilliseconds => (ushort)(_speedOrPeriod0 << 8 | _speedOrPeriod1);
	public EffectDirection Direction => _direction;
	public byte Intensity => _intensity;
}
