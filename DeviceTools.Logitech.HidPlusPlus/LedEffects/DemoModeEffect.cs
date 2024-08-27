using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.LedEffects;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 11)]
[DebuggerDisplay("{Effect}")]
public readonly struct DemoModeEffect
{
	private readonly byte _effect;

	public DemoModeEffect() => _effect = (byte)PredefinedEffect.DemoMode;

	public PredefinedEffect Effect => (PredefinedEffect)_effect;
}
