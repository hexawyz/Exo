using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.LedEffects;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 11)]
[DebuggerDisplay("{Effect}, SpeedOrPeriod = {SpeedOrPeriodInMilliseconds}, Intensity = {Intensity}")]
public readonly struct ColorCycleEffect
{
	private readonly byte _effect;
	private readonly byte _unused0;
	private readonly byte _unused1;
	private readonly byte _unused2;
	private readonly byte _unused3;
	private readonly byte _unused4;
	private readonly byte _speedOrPeriod0;
	private readonly byte _speedOrPeriod1;
	private readonly byte _intensity;

	public ColorCycleEffect(ushort speedOrPeriodInMilliseconds, byte intensity)
	{
		_effect = (byte)PredefinedEffect.ColorCycle;
		BigEndian.Write(ref _speedOrPeriod0, speedOrPeriodInMilliseconds);
		_intensity = intensity;
	}

	public PredefinedEffect Effect => (PredefinedEffect)_effect;
	public ushort SpeedOrPeriodInMilliseconds => BigEndian.ReadUInt16(in _speedOrPeriod0);
	public byte Intensity => _intensity;

	public static implicit operator LedEffect(in ColorCycleEffect effect) => Unsafe.As<ColorCycleEffect, LedEffect>(ref Unsafe.AsRef(in effect));
}
