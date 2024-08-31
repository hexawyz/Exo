using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.LedEffects;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 11)]
[DebuggerDisplay("{Effect}, Color = {Color}, Option = {Option}")]
public readonly struct FixedEffect
{
	private readonly byte _effect;
	private readonly Color _color;
	private readonly FixedEffectOption _option;

	public FixedEffect(Color color, FixedEffectOption option)
	{
		_effect = (byte)PredefinedEffect.Fixed;
		_color = color;
		_option = option;
	}

	public PredefinedEffect Effect => (PredefinedEffect)_effect;
	public Color Color => _color;
	public FixedEffectOption Option => _option;

	public static implicit operator LedEffect(in FixedEffect effect) => Unsafe.As<FixedEffect, LedEffect>(ref Unsafe.AsRef(in effect));
}
