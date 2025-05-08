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
		ILightingZoneEffect<Variable5ColorBreathingEffect>,
		ILightingZoneEffect<Variable5ColorBlinkEffect>
	{
		public AuraRam5LightingZone(AuraRamDriver driver, in DiscoveredModuleDescription description)
			: base(driver, description)
		{
		}

		protected override void InitializeMultiColorCurrentEffect(ReadOnlySpan<RgbColor> colors, sbyte frameDelay)
		{
			Span<RgbColor> colorBuffer = stackalloc RgbColor[5];
			for (int i = 0; i < colors.Length; i++)
			{
				colorBuffer[i] = SwapGreenAndBlue(colors[i]);
			}
			var swappedColors = Unsafe.As<RgbColor, FixedArray5<RgbColor>>(ref MemoryMarshal.GetReference(colorBuffer));
			switch (AuraEffect)
			{
			case AuraEffect.Static:
				CurrentEffect = new Static5ColorEffect(swappedColors);
				break;
			case AuraEffect.Breathing:
				CurrentEffect = new Variable5ColorBreathingEffect(swappedColors, GetEffectSpeed(frameDelay));
				break;
			case AuraEffect.Blink:
				CurrentEffect = new Variable5ColorBlinkEffect(swappedColors, GetEffectSpeed(frameDelay));
				break;
			}
		}

		bool ILightingZoneEffect<Static5ColorEffect>.TryGetCurrentEffect(out Static5ColorEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<Variable5ColorBreathingEffect>.TryGetCurrentEffect(out Variable5ColorBreathingEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<Variable5ColorBlinkEffect>.TryGetCurrentEffect(out Variable5ColorBlinkEffect effect) => CurrentEffect.TryGetEffect(out effect);

		void ILightingZoneEffect<Static5ColorEffect>.ApplyEffect(in Static5ColorEffect effect) => ApplyColorEffect(AuraEffect.Static, DefaultFrameDelay, effect.Colors, effect);
		void ILightingZoneEffect<Variable5ColorBreathingEffect>.ApplyEffect(in Variable5ColorBreathingEffect effect) => ApplyColorEffect(AuraEffect.Breathing, DefaultFrameDelays[(byte)effect.Speed], effect.Colors, effect);
		void ILightingZoneEffect<Variable5ColorBlinkEffect>.ApplyEffect(in Variable5ColorBlinkEffect effect) => ApplyColorEffect(AuraEffect.Blink, DefaultFrameDelays[(byte)effect.Speed], effect.Colors, effect);
	}
}


