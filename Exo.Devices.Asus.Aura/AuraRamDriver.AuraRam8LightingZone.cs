using System.Runtime.CompilerServices;
using Exo.Lighting.Effects;
using Exo.Lighting;

namespace Exo.Devices.Asus.Aura;

public partial class AuraRamDriver
{
	private sealed class AuraRam8LightingZone :
		AuraRamLightingZone,
		ILightingZoneEffect<Static8ColorEffect>
	{
		public AuraRam8LightingZone(AuraRamDriver driver, in DiscoveredModuleDescription description)
			: base(driver, description)
		{
		}

		protected override void InitializeMultiColorCurrentEffect(ReadOnlySpan<RgbColor> colors)
		{
			switch (AuraEffect)
			{
			case AuraEffect.Static:
				CurrentEffect = new Static8ColorEffect
				(
					SwapGreenAndBlue(colors[0]),
					SwapGreenAndBlue(colors[1]),
					SwapGreenAndBlue(colors[2]),
					SwapGreenAndBlue(colors[3]),
					SwapGreenAndBlue(colors[4]),
					SwapGreenAndBlue(colors[5]),
					SwapGreenAndBlue(colors[6]),
					SwapGreenAndBlue(colors[7])
				);
				break;
			}
		}

		[SkipLocalsInit]
		private bool UpdateColors(RgbColor color1, RgbColor color2, RgbColor color3, RgbColor color4, RgbColor color5, RgbColor color6, RgbColor color7, RgbColor color8)
		{
			TenColorArray newColors;
			Unsafe.SkipInit(out newColors);
			var span = (Span<RgbColor>)newColors;
			span[0] = SwapGreenAndBlue(color1);
			span[1] = SwapGreenAndBlue(color2);
			span[2] = SwapGreenAndBlue(color3);
			span[3] = SwapGreenAndBlue(color4);
			span[4] = SwapGreenAndBlue(color5);
			span[5] = SwapGreenAndBlue(color6);
			span[6] = SwapGreenAndBlue(color7);
			span[7] = SwapGreenAndBlue(color8);
			return UpdateRawColors(ref newColors);
		}

		bool ILightingZoneEffect<Static8ColorEffect>.TryGetCurrentEffect(out Static8ColorEffect effect) => CurrentEffect.TryGetEffect(out effect);

		void ILightingZoneEffect<Static8ColorEffect>.ApplyEffect(in Static8ColorEffect effect)
		{
			lock (Driver._lock)
			{
				var changes = PendingChanges;
				if (AuraEffect != AuraEffect.Static)
				{
					if (AuraEffect == AuraEffect.Dynamic) changes ^= EffectChanges.Dynamic;
					AuraEffect = AuraEffect.Static;
					changes |= EffectChanges.Effect;
				}
				if (UpdateColors(effect.Color, effect.Color1, effect.Color2, effect.Color3, effect.Color4, effect.Color5, effect.Color6, effect.Color7)) changes |= EffectChanges.Colors;
				PendingChanges = changes;
				CurrentEffect = effect;
			}
		}
	}
}


