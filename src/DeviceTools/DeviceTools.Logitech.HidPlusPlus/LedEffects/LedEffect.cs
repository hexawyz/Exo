using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.LedEffects;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 11)]
[DebuggerDisplay("{DebugView}")]
public readonly struct LedEffect
{
	private readonly byte _effect;
	private readonly byte _parameter0;
	private readonly byte _parameter1;
	private readonly byte _parameter2;
	private readonly byte _parameter3;
	private readonly byte _parameter4;
	private readonly byte _parameter5;
	private readonly byte _parameter6;
	private readonly byte _parameter7;
	private readonly byte _parameter8;
	private readonly byte _parameter9;

	public PredefinedEffect Effect => (PredefinedEffect)_effect;

	public FixedEffect AsFixed() => Effect == PredefinedEffect.Fixed ? Unsafe.BitCast<LedEffect, FixedEffect>(this) : throw new InvalidCastException();
	public LegacyPulseEffect AsLegacyPulse() => Effect == PredefinedEffect.LegacyPulse ? Unsafe.BitCast<LedEffect, LegacyPulseEffect>(this) : throw new InvalidCastException();
	public ColorCycleEffect AsColorCycle() => Effect == PredefinedEffect.ColorCycle ? Unsafe.BitCast<LedEffect, ColorCycleEffect>(this) : throw new InvalidCastException();
	public ColorWaveEffect AsColorWave() => Effect == PredefinedEffect.ColorWave ? Unsafe.BitCast<LedEffect, ColorWaveEffect>(this) : throw new InvalidCastException();
	public StarlightEffect AsStarlight() => Effect == PredefinedEffect.Starlight ? Unsafe.BitCast<LedEffect, StarlightEffect>(this) : throw new InvalidCastException();
	public LightOnPressEffect AsLightOnPress() => Effect == PredefinedEffect.LightOnPress ? Unsafe.BitCast<LedEffect, LightOnPressEffect>(this) : throw new InvalidCastException();
	public BootUpEffect AsBootUp() => Effect == PredefinedEffect.BootUp ? Unsafe.BitCast<LedEffect, BootUpEffect>(this) : throw new InvalidCastException();
	public DemoModeEffect AsDemoMode() => Effect == PredefinedEffect.DemoMode ? Unsafe.BitCast<LedEffect, DemoModeEffect>(this) : throw new InvalidCastException();
	public PulseEffect AsPulse() => Effect == PredefinedEffect.Pulse ? Unsafe.BitCast<LedEffect, PulseEffect>(this) : throw new InvalidCastException();
	public RippleEffect AsRipple() => Effect == PredefinedEffect.Ripple ? Unsafe.BitCast<LedEffect, RippleEffect>(this) : throw new InvalidCastException();

	private object DebugView
		=> Effect switch
		{
			PredefinedEffect.Fixed => AsFixed(),
			PredefinedEffect.LegacyPulse => AsLegacyPulse(),
			PredefinedEffect.ColorCycle => AsColorCycle(),
			PredefinedEffect.ColorWave => AsColorWave(),
			PredefinedEffect.Starlight => AsStarlight(),
			PredefinedEffect.LightOnPress => AsLightOnPress(),
			PredefinedEffect.BootUp => AsBootUp(),
			PredefinedEffect.DemoMode => AsDemoMode(),
			PredefinedEffect.Pulse => AsPulse(),
			PredefinedEffect.Ripple => AsRipple(),
			_ => Effect,
		};
}
