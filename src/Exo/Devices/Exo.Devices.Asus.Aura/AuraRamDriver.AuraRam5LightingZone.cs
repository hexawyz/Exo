using System.Runtime.CompilerServices;
using Exo.Lighting.Effects;
using Exo.Lighting;
using Exo.ColorFormats;
using System.Runtime.InteropServices;

namespace Exo.Devices.Asus.Aura;

public partial class AuraRamDriver
{
	private sealed class AuraRam5LightingZone :
		AuraRamLightingZone,
		ILightingZoneEffect<Static5ColorEffect>,
		ILightingZoneEffect<ColorPulse5Effect>,
		ILightingZoneEffect<ColorFlash5Effect>
	{
		public AuraRam5LightingZone(AuraRamDriver driver, in DiscoveredModuleDescription description)
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
			var swappedColors = Unsafe.As<RgbColor, FixedArray5<RgbColor>>(ref MemoryMarshal.GetReference(buffers));
			switch (AuraEffect)
			{
			case AuraEffect.Static:
				CurrentEffect = new Static5ColorEffect(swappedColors);
				break;
			case AuraEffect.Pulse:
				CurrentEffect = new ColorPulse5Effect(swappedColors);
				break;
			case AuraEffect.Flash:
				CurrentEffect = new ColorFlash5Effect(swappedColors);
				break;
			}
		}

		bool ILightingZoneEffect<Static5ColorEffect>.TryGetCurrentEffect(out Static5ColorEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorPulse5Effect>.TryGetCurrentEffect(out ColorPulse5Effect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorFlash5Effect>.TryGetCurrentEffect(out ColorFlash5Effect effect) => CurrentEffect.TryGetEffect(out effect);

		void ILightingZoneEffect<Static5ColorEffect>.ApplyEffect(in Static5ColorEffect effect) => ApplyColorEffect(AuraEffect.Static, DefaultFrameDelay, effect.Colors, effect);
		void ILightingZoneEffect<ColorPulse5Effect>.ApplyEffect(in ColorPulse5Effect effect) => ApplyColorEffect(AuraEffect.Pulse, DefaultFrameDelay, effect.Colors, effect);
		void ILightingZoneEffect<ColorFlash5Effect>.ApplyEffect(in ColorFlash5Effect effect) => ApplyColorEffect(AuraEffect.Flash, DefaultFrameDelay, effect.Colors, effect);
	}
}


