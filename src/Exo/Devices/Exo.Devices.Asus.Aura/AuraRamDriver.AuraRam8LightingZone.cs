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
		ILightingZoneEffect<Variable8ColorBreathingEffect>,
		ILightingZoneEffect<Variable8ColorFlashEffect>
	{
		public AuraRam8LightingZone(AuraRamDriver driver, in DiscoveredModuleDescription description)
			: base(driver, description)
		{
		}

		protected override void InitializeMultiColorCurrentEffect(ReadOnlySpan<RgbColor> colors, sbyte frameDelay)
		{
			Span<RgbColor> colorBuffer = stackalloc RgbColor[8];
			for (int i = 0; i < colors.Length; i++)
			{
				colorBuffer[i] = SwapGreenAndBlue(colors[i]);
			}
			var swappedColors = Unsafe.As<RgbColor, FixedArray8<RgbColor>>(ref MemoryMarshal.GetReference(colorBuffer));
			switch (AuraEffect)
			{
			case AuraEffect.Static:
				CurrentEffect = new Static8ColorEffect(swappedColors);
				break;
			case AuraEffect.Breathing:
				CurrentEffect = new Variable8ColorBreathingEffect(swappedColors, GetEffectSpeed(frameDelay));
				break;
			case AuraEffect.Flash:
				CurrentEffect = new Variable8ColorFlashEffect(swappedColors, GetEffectSpeed(frameDelay));
				break;
			}
		}

		bool ILightingZoneEffect<Static8ColorEffect>.TryGetCurrentEffect(out Static8ColorEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<Variable8ColorBreathingEffect>.TryGetCurrentEffect(out Variable8ColorBreathingEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<Variable8ColorFlashEffect>.TryGetCurrentEffect(out Variable8ColorFlashEffect effect) => CurrentEffect.TryGetEffect(out effect);

		void ILightingZoneEffect<Static8ColorEffect>.ApplyEffect(in Static8ColorEffect effect) => ApplyColorEffect(AuraEffect.Static, DefaultFrameDelay, effect.Colors, effect);
		void ILightingZoneEffect<Variable8ColorBreathingEffect>.ApplyEffect(in Variable8ColorBreathingEffect effect) => ApplyColorEffect(AuraEffect.Breathing, DefaultFrameDelays[(byte)effect.Speed], effect.Colors, effect);
		void ILightingZoneEffect<Variable8ColorFlashEffect>.ApplyEffect(in Variable8ColorFlashEffect effect) => ApplyColorEffect(AuraEffect.Flash, DefaultFrameDelays[(byte)effect.Speed], effect.Colors, effect);
	}
}


