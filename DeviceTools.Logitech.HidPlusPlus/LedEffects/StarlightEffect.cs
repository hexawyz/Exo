using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.LedEffects;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 11)]
[DebuggerDisplay("{Effect}, SkyColor = {SkyColor}, StarColor = {StarColor}")]
public readonly struct StarlightEffect
{
	private readonly byte _effect;
	private readonly Color _skyColor;
	private readonly Color _starColor;

	public PredefinedEffect Effect => (PredefinedEffect)_effect;
	public Color SkyColor => _skyColor;
	public Color StarColor => _starColor;
}
