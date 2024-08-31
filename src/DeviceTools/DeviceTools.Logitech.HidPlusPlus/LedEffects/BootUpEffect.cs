using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.LedEffects;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 11)]
[DebuggerDisplay("{Effect}")]
public readonly struct BootUpEffect
{
	private readonly byte _effect;

	public BootUpEffect() => _effect = (byte)PredefinedEffect.BootUp;

	public PredefinedEffect Effect => (PredefinedEffect)_effect;
}
