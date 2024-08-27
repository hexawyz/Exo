using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.LedEffects;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 11)]
[DebuggerDisplay("{Effect}, Color = {Color}, SpeedOrPeriod = {SpeedOrPeriodInMilliseconds}, Waveform = {Waveform}, Intensity = {Intensity}")]
public readonly struct PulseEffect
{
	private readonly byte _effect;
	private readonly Color _color;
	private readonly byte _speedOrPeriod0;
	private readonly byte _speedOrPeriod1;
	private readonly EffectWaveform _waveform;
	private readonly byte _intensity;

	public PredefinedEffect Effect => (PredefinedEffect)_effect;
	public Color Color => _color;
	public ushort SpeedOrPeriodInMilliseconds => BigEndian.ReadUInt16(in _speedOrPeriod0);
	public EffectWaveform Waveform => _waveform;
	public byte Intensity => _intensity;
}
