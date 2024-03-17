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
		ILightingZoneEffect<Static8ColorEffect>
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
			var swappedColors = Unsafe.As<RgbColor, FixedArray5<RgbColor>>(ref MemoryMarshal.GetReference(buffers));
			switch (AuraEffect)
			{
			case AuraEffect.Static:
				CurrentEffect = new Static5ColorEffect(swappedColors);
				break;
			}
		}

		[SkipLocalsInit]
		private bool UpdateColors(ReadOnlySpan<RgbColor> colors)
		{
			TenColorArray newColors;
			Unsafe.SkipInit(out newColors);
			var span = (Span<RgbColor>)newColors;
			span[0] = SwapGreenAndBlue(colors[0]);
			span[1] = SwapGreenAndBlue(colors[1]);
			span[2] = SwapGreenAndBlue(colors[2]);
			span[3] = SwapGreenAndBlue(colors[3]);
			span[4] = SwapGreenAndBlue(colors[4]);
			span[5] = SwapGreenAndBlue(colors[5]);
			span[6] = SwapGreenAndBlue(colors[6]);
			span[7] = SwapGreenAndBlue(colors[7]);
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
				if (UpdateColors(effect.Colors)) changes |= EffectChanges.Colors;
				PendingChanges = changes;
				CurrentEffect = effect;
			}
		}
	}
}


