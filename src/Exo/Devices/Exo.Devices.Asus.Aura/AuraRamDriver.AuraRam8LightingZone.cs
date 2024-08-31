using System.Runtime.CompilerServices;
using Exo.Lighting.Effects;
using Exo.Lighting;
using Exo.ColorFormats;
using System.Runtime.InteropServices;

namespace Exo.Devices.Asus.Aura;

public partial class AuraRamDriver
{
	private sealed class AuraRam8LightingZone :
		AuraRamLightingZone,
		ILightingZoneEffect<Static8ColorEffect>,
		ILightingZoneEffect<ColorPulse8Effect>,
		ILightingZoneEffect<ColorFlash8Effect>
	{
		public AuraRam8LightingZone(AuraRamDriver driver, in DiscoveredModuleDescription description)
			: base(driver, description)
		{
		}

		protected override void InitializeMultiColorCurrentEffect(ReadOnlySpan<RgbColor> colors)
		{
			Span<RgbColor> buffers = stackalloc RgbColor[colors.Length];
			for (int i = 0; i < colors.Length; i++)
			{
				buffers[i] = SwapGreenAndBlue(colors[i]);
			}
			var swappedColors = Unsafe.As<RgbColor, FixedArray8<RgbColor>>(ref MemoryMarshal.GetReference(buffers));
			switch (AuraEffect)
			{
			case AuraEffect.Static:
				CurrentEffect = new Static8ColorEffect(swappedColors);
				break;
			case AuraEffect.Pulse:
				CurrentEffect = new ColorPulse8Effect(swappedColors);
				break;
			case AuraEffect.Flash:
				CurrentEffect = new ColorFlash8Effect(swappedColors);
				break;
			}
		}

		bool ILightingZoneEffect<Static8ColorEffect>.TryGetCurrentEffect(out Static8ColorEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorPulse8Effect>.TryGetCurrentEffect(out ColorPulse8Effect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorFlash8Effect>.TryGetCurrentEffect(out ColorFlash8Effect effect) => CurrentEffect.TryGetEffect(out effect);

		void ILightingZoneEffect<Static8ColorEffect>.ApplyEffect(in Static8ColorEffect effect) => ApplyColorEffect(AuraEffect.Static, DefaultFrameDelay, effect.Colors, effect);
		void ILightingZoneEffect<ColorPulse8Effect>.ApplyEffect(in ColorPulse8Effect effect) => ApplyColorEffect(AuraEffect.Pulse, DefaultFrameDelay, effect.Colors, effect);
		void ILightingZoneEffect<ColorFlash8Effect>.ApplyEffect(in ColorFlash8Effect effect) => ApplyColorEffect(AuraEffect.Flash, DefaultFrameDelay, effect.Colors, effect);
	}
}


