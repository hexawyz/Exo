using System.Runtime.CompilerServices;
using Exo.Lighting.Effects;
using Exo.Lighting;
using System.Runtime.InteropServices;

namespace Exo.Devices.Asus.Aura;

public partial class AuraRamDriver
{
	private static bool IsSingleColor(ReadOnlySpan<RgbColor> colors)
	{
		RgbColor color = colors[0];
		for (int i = 1; i < colors.Length; i++)
		{
			if (colors[i] != color) return false;
		}
		return true;
	}

	private static Span<RgbColor> AsSpan<TColorBuffer>(ref TColorBuffer colors)
		where TColorBuffer : unmanaged
		=> MemoryMarshal.CreateSpan(ref Unsafe.As<TColorBuffer, RgbColor>(ref colors), Unsafe.SizeOf<TColorBuffer>() / 3);

	private static RgbColor SwapGreenAndBlue(RgbColor color) => new RgbColor(color.R, color.B, color.G);

	private static void SwapGreenAndBlue(ref RgbColor color)
	{
		byte g = color.G;
		color.G = color.B;
		color.B = g;
	}

	private abstract class AuraRamLightingZone :
		ILightingZone,
		ILightingZoneEffect<DisabledEffect>,
		ILightingZoneEffect<StaticColorEffect>,
		ILightingZoneEffect<ColorPulseEffect>,
		ILightingZoneEffect<ColorFlashEffect>,
		ILightingZoneEffect<ColorCycleEffect>,
		ILightingZoneEffect<ColorWaveEffect>
	{

		protected readonly AuraRamDriver Driver;
		protected ILightingEffect CurrentEffect;
		protected readonly byte Address;
		protected readonly byte ColorCount;
		protected AuraEffect AuraEffect;
		protected EffectChanges PendingChanges;
		private readonly ushort _dynamicColorBufferAddress;
		private readonly ushort _staticColorBufferAddress;
		private TenColorArray _staticColors;
		private readonly Guid _zoneId;

		protected Span<RgbColor> StaticColors => AsSpan(ref _staticColors)[..ColorCount];

		public AuraRamLightingZone(AuraRamDriver driver, in DiscoveredModuleDescription description)
		{
			Driver = driver;
			CurrentEffect = DisabledEffect.SharedInstance;
			Address = description.Address;
			ColorCount = description.ColorCount;
			AuraEffect = description.Effect;
			if (description.HasExtendedColors)
			{
				_dynamicColorBufferAddress = 0x8100;
				_staticColorBufferAddress = 0x8160;
			}
			else
			{
				_dynamicColorBufferAddress = 0x8000;
				_staticColorBufferAddress = 0x8010;
			}
			((ReadOnlySpan<RgbColor>)description.Colors)[..ColorCount].CopyTo(StaticColors);
			var colors = StaticColors;
			bool isSingleColor = IsSingleColor(colors);
			_zoneId = description.ZoneId;
			switch (description.Effect)
			{
			case AuraEffect.Off:
				break;
			case AuraEffect.Static:
				if (!isSingleColor) goto InitializeMultiColor;
				CurrentEffect = new StaticColorEffect(SwapGreenAndBlue(StaticColors[0]));
				break;
			case AuraEffect.Pulse:
				if (!isSingleColor) goto InitializeMultiColor;
				CurrentEffect = new ColorFlashEffect(SwapGreenAndBlue(StaticColors[0]));
				break;
			case AuraEffect.Flash:
				if (!isSingleColor) goto InitializeMultiColor;
				CurrentEffect = new ColorFlashEffect(SwapGreenAndBlue(StaticColors[0]));
				break;
			case AuraEffect.ColorCycle:
				CurrentEffect = ColorCycleEffect.SharedInstance;
				break;
			case AuraEffect.ColorWave:
				CurrentEffect = ColorWaveEffect.SharedInstance;
				break;
			case AuraEffect.Dynamic:
				CurrentEffect = DisabledEffect.SharedInstance;
				break;
			}
			return;
		InitializeMultiColor:;
			InitializeMultiColorCurrentEffect(colors);
		}

		protected abstract void InitializeMultiColorCurrentEffect(ReadOnlySpan<RgbColor> colors);

		Guid ILightingZone.ZoneId => _zoneId;

		ILightingEffect ILightingZone.GetCurrentEffect() => CurrentEffect;

		public async ValueTask ApplyChangesAsync()
		{
			if (PendingChanges != 0)
			{
				if ((PendingChanges & EffectChanges.Colors) != 0)
				{
					await WriteBytesAsync(Driver._smBus, Address, _staticColorBufferAddress, MemoryMarshal.Cast<RgbColor, byte>(StaticColors).ToArray());
				}
				if ((PendingChanges & EffectChanges.Effect) != 0)
				{
					if ((PendingChanges & EffectChanges.Dynamic) != 0)
					{
						if (AuraEffect == AuraEffect.Dynamic)
						{
							await WriteByteAsync(Driver._smBus, Address, 0x8020, 0x01);
						}
						else
						{
							await WriteBytesAsync(Driver._smBus, Address, 0x8020, 0x00, (byte)AuraEffect);
						}
					}
					else
					{
						await WriteByteAsync(Driver._smBus, Address, 0x8021, (byte)AuraEffect);
					}
				}
				await WriteByteAsync(Driver._smBus, Address, 0x80A0, 0x01);
				PendingChanges = 0;
			}
		}

		bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorPulseEffect>.TryGetCurrentEffect(out ColorPulseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorFlashEffect>.TryGetCurrentEffect(out ColorFlashEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorCycleEffect>.TryGetCurrentEffect(out ColorCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorWaveEffect>.TryGetCurrentEffect(out ColorWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);

		[SkipLocalsInit]
		protected bool UpdateRawColors(RgbColor color)
		{
			TenColorArray newColors;
			Unsafe.SkipInit(out newColors);
			AsSpan(ref newColors)[..ColorCount].Fill(color);
			return UpdateRawColors(ref newColors);
		}

		protected bool UpdateRawColors(ref TenColorArray colors)
		{
			var oldColors = MemoryMarshal.Cast<RgbColor, byte>(StaticColors);
			var newColors = MemoryMarshal.Cast<RgbColor, byte>(AsSpan(ref colors)[..ColorCount]);

			if (oldColors.SequenceEqual(newColors)) return false;

			newColors.CopyTo(oldColors);
			return true;
		}

		private void ApplySingleColorEffect<TEffect>(AuraEffect auraEffect, in TEffect effect)
			where TEffect : ISingleColorLightEffect
		{
			lock (Driver._lock)
			{
				var changes = PendingChanges;
				if (AuraEffect != auraEffect)
				{
					if (AuraEffect == AuraEffect.Dynamic) changes ^= EffectChanges.Dynamic;
					AuraEffect = auraEffect;
					changes |= EffectChanges.Effect;
				}
				if (UpdateRawColors(SwapGreenAndBlue(effect.Color))) changes |= EffectChanges.Colors;
				PendingChanges = changes;
				CurrentEffect = effect;
			}
		}

		void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect)
		{
			lock (Driver._lock)
			{
				if (ReferenceEquals(CurrentEffect, DisabledEffect.SharedInstance)) return;

				var changes = PendingChanges;
				if (AuraEffect == AuraEffect.Dynamic) changes ^= EffectChanges.Dynamic;
				AuraEffect = AuraEffect.Off;
				_staticColors = default;
				changes |= EffectChanges.Effect | EffectChanges.Colors;
				PendingChanges = changes;
				CurrentEffect = DisabledEffect.SharedInstance;
			}
		}

		void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect)
			=> ApplySingleColorEffect(AuraEffect.Static, effect);

		void ILightingZoneEffect<ColorPulseEffect>.ApplyEffect(in ColorPulseEffect effect)
			=> ApplySingleColorEffect(AuraEffect.Pulse, effect);

		void ILightingZoneEffect<ColorFlashEffect>.ApplyEffect(in ColorFlashEffect effect)
			=> ApplySingleColorEffect(AuraEffect.Flash, effect);

		void ILightingZoneEffect<ColorCycleEffect>.ApplyEffect(in ColorCycleEffect effect)
		{
			lock (Driver._lock)
			{
				if (ReferenceEquals(CurrentEffect, DisabledEffect.SharedInstance)) return;

				var changes = PendingChanges;
				if (AuraEffect == AuraEffect.Dynamic) changes ^= EffectChanges.Dynamic;
				AuraEffect = AuraEffect.ColorCycle;
				changes |= EffectChanges.Effect;
				PendingChanges = changes;
				CurrentEffect = ColorCycleEffect.SharedInstance;
			}
		}

		void ILightingZoneEffect<ColorWaveEffect>.ApplyEffect(in ColorWaveEffect effect)
		{
			lock (Driver._lock)
			{
				if (ReferenceEquals(CurrentEffect, DisabledEffect.SharedInstance)) return;

				var changes = PendingChanges;
				if (AuraEffect == AuraEffect.Dynamic) changes ^= EffectChanges.Dynamic;
				AuraEffect = AuraEffect.ColorWave;
				changes |= EffectChanges.Effect;
				PendingChanges = changes;
				CurrentEffect = ColorWaveEffect.SharedInstance;
			}
		}
	}

	private sealed class AuraRam5LightingZone :
		AuraRamLightingZone,
		ILightingZoneEffect<Static5ColorEffect>
	{
		public AuraRam5LightingZone(AuraRamDriver driver, in DiscoveredModuleDescription description)
			: base(driver, description)
		{
		}

		protected override void InitializeMultiColorCurrentEffect(ReadOnlySpan<RgbColor> colors)
		{
			switch (AuraEffect)
			{
			case AuraEffect.Static:
				CurrentEffect = new Static5ColorEffect
				(
					SwapGreenAndBlue(colors[0]),
					SwapGreenAndBlue(colors[1]),
					SwapGreenAndBlue(colors[2]),
					SwapGreenAndBlue(colors[3]),
					SwapGreenAndBlue(colors[4])
				);
				break;
			}
		}

		[SkipLocalsInit]
		private bool UpdateColors(RgbColor color1, RgbColor color2, RgbColor color3, RgbColor color4, RgbColor color5)
		{
			TenColorArray newColors;
			Unsafe.SkipInit(out newColors);
			var span = (Span<RgbColor>)newColors;
			span[0] = SwapGreenAndBlue(color1);
			span[1] = SwapGreenAndBlue(color2);
			span[2] = SwapGreenAndBlue(color3);
			span[3] = SwapGreenAndBlue(color4);
			span[4] = SwapGreenAndBlue(color5);
			return UpdateRawColors(ref newColors);
		}

		bool ILightingZoneEffect<Static5ColorEffect>.TryGetCurrentEffect(out Static5ColorEffect effect) => CurrentEffect.TryGetEffect(out effect);

		void ILightingZoneEffect<Static5ColorEffect>.ApplyEffect(in Static5ColorEffect effect)
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
				if (UpdateColors(effect.Color, effect.Color1, effect.Color2, effect.Color3, effect.Color4)) changes |= EffectChanges.Colors;
				PendingChanges = changes;
				CurrentEffect = effect;
			}
		}
	}

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


