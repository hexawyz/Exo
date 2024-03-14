using System.Runtime.CompilerServices;
using Exo.Lighting.Effects;
using Exo.Lighting;
using System.Runtime.InteropServices;
using Exo.Devices.Asus.Aura.Effects;

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
		ILightingZoneEffect<ColorWaveEffect>,
		ILightingZoneEffect<ColorCyclePulseEffect>,
		ILightingZoneEffect<WaveEffect>,
		ILightingZoneEffect<ColorCycleWaveEffect>,
		ILightingZoneEffect<ColorChaseEffect>,
		ILightingZoneEffect<ColorCycleChaseEffect>,
		ILightingZoneEffect<WideColorCycleChaseEffect>,
		ILightingZoneEffect<AlternateEffect>,
		ILightingZoneEffect<CycleRandomFlashesEffect>,
		ILightingZoneEffect<VariableColorFlashEffect>,
		ILightingZoneEffect<VariableColorPulseEffect>,
		ILightingZoneEffect<VariableColorChaseEffect>,
		ILightingZoneEffect<VariableColorWaveEffect>,
		ILightingZoneEffect<VariableColorCycleEffect>
	{
		protected readonly AuraRamDriver Driver;
		protected ILightingEffect CurrentEffect;
		protected readonly byte Address;
		protected readonly byte ColorCount;
		protected AuraEffect AuraEffect;
		protected EffectChanges PendingChanges;
		protected sbyte FrameDelay;
		protected bool IsReversed;
		private readonly ushort _dynamicColorBufferAddress;
		private readonly ushort _staticColorBufferAddress;
		private TenColorArray _localColors;
		private readonly Guid _zoneId;

		protected Span<RgbColor> LocalColors => AsSpan(ref _localColors)[..ColorCount];

		public AuraRamLightingZone(AuraRamDriver driver, in DiscoveredModuleDescription description)
		{
			Driver = driver;
			CurrentEffect = DisabledEffect.SharedInstance;
			Address = description.Address;
			ColorCount = description.ColorCount;
			AuraEffect = description.Effect;
			FrameDelay = description.FrameDelay;
			IsReversed = description.IsReversed;
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
			((ReadOnlySpan<RgbColor>)description.Colors)[..ColorCount].CopyTo(LocalColors);
			var colors = LocalColors;
			bool isSingleColor = IsSingleColor(colors);
			_zoneId = description.ZoneId;
			switch (description.Effect)
			{
			case AuraEffect.Off:
				break;
			case AuraEffect.Static:
				if (!isSingleColor) goto InitializeMultiColor;
				CurrentEffect = new StaticColorEffect(SwapGreenAndBlue(LocalColors[0]));
				break;
			case AuraEffect.Pulse:
				if (!isSingleColor) goto InitializeMultiColor;
				CurrentEffect = new ColorFlashEffect(SwapGreenAndBlue(LocalColors[0]));
				break;
			case AuraEffect.Flash:
				if (!isSingleColor) goto InitializeMultiColor;
				CurrentEffect = new ColorFlashEffect(SwapGreenAndBlue(LocalColors[0]));
				break;
			case AuraEffect.ColorCycle:
				CurrentEffect = ColorCycleEffect.SharedInstance;
				break;
			case AuraEffect.ColorWave:
				CurrentEffect = ColorWaveEffect.SharedInstance;
				break;
			case AuraEffect.CyclePulse:
				CurrentEffect = ColorCyclePulseEffect.SharedInstance;
				break;
			case AuraEffect.Wave:
				CurrentEffect = new WaveEffect(SwapGreenAndBlue(LocalColors[0]));
				break;
			case AuraEffect.CycleWave:
				CurrentEffect = ColorCycleWaveEffect.SharedInstance;
				break;
			case AuraEffect.Chase:
				CurrentEffect = new ColorChaseEffect(SwapGreenAndBlue(LocalColors[0]));
				break;
			case AuraEffect.CycleChase:
				CurrentEffect = ColorCycleChaseEffect.SharedInstance;
				break;
			case AuraEffect.WideCycleChase:
				CurrentEffect = WideColorCycleChaseEffect.SharedInstance;
				break;
			case AuraEffect.Alternate:
				CurrentEffect = AlternateEffect.SharedInstance;
				break;
			case AuraEffect.CycleRandomFlashes:
				CurrentEffect = CycleRandomFlashesEffect.SharedInstance;
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

		// Changes applying is split in two parts:
		// First, we prepare the bulk of the changes and send them to the devices
		// Then, we send the 
		public async ValueTask<FinalPendingChanges> UploadDeferredChangesAsync()
		{
			FinalPendingChanges finalPendingChanges = FinalPendingChanges.None;
			var pendingChanges = PendingChanges;
			if (pendingChanges != 0)
			{
				var effect = AuraEffect;

				if ((pendingChanges & EffectChanges.Colors) != 0)
				{
					if (effect == AuraEffect.Dynamic)
					{
						// In the initial change from static to dynamic, push the new colors here.
						if ((pendingChanges & EffectChanges.Dynamic) != 0)
						{
							await WriteBytesAsync(Driver._smBus, Address, _dynamicColorBufferAddress, MemoryMarshal.Cast<RgbColor, byte>(LocalColors).ToArray());
						}
						else
						{
							finalPendingChanges |= FinalPendingChanges.DynamicColors;
						}
					}
					else
					{
						await WriteBytesAsync(Driver._smBus, Address, _staticColorBufferAddress, MemoryMarshal.Cast<RgbColor, byte>(LocalColors).ToArray());
						finalPendingChanges |= FinalPendingChanges.Commit;
					}
				}
				if ((pendingChanges & ~EffectChanges.Colors) != 0)
				{
					if ((pendingChanges & EffectChanges.Dynamic) != 0)
					{
						if (effect == AuraEffect.Dynamic)
						{
							await WriteByteAsync(Driver._smBus, Address, 0x8020, 0x01);
						}
						else
						{
							// When switching from dynamic to predefined effects, update all other states.
							// TODO: Fix the possible missed updates for colors when switching away from dynamic lighting.
							await WriteBytesAsync(Driver._smBus, Address, 0x8020, 0x00, (byte)effect, (byte)FrameDelay, IsReversed ? (byte)1 : (byte)0);
						}
					}
					else
					{
						// The goal here is to minimize changes, as SMBus operations are relatively costly.
						// However, it might make the code itself les efficient. This can be revisited later if necessary.
						ValueTask task;
						switch (pendingChanges & ~(EffectChanges.Colors | EffectChanges.Dynamic))
						{
						case EffectChanges.None:
							goto default;
						case EffectChanges.Effect:
							task = WriteByteAsync(Driver._smBus, Address, 0x8021, (byte)effect);
							break;
						case EffectChanges.FrameDelay:
							task = WriteByteAsync(Driver._smBus, Address, 0x8021, (byte)FrameDelay);
							break;
						case EffectChanges.Effect | EffectChanges.FrameDelay:
							task = WriteBytesAsync(Driver._smBus, Address, 0x8021, (byte)effect, (byte)FrameDelay);
							break;
						case EffectChanges.Direction:
							task = WriteByteAsync(Driver._smBus, Address, 0x8021, (byte)FrameDelay);
							break;
						case EffectChanges.Effect | EffectChanges.Direction:
						case EffectChanges.Effect | EffectChanges.FrameDelay | EffectChanges.Direction:
							task = WriteBytesAsync(Driver._smBus, Address, 0x8021, (byte)effect, (byte)FrameDelay, IsReversed ? (byte)1 : (byte)0);
							break;
						case EffectChanges.FrameDelay | EffectChanges.Direction:
							task = WriteBytesAsync(Driver._smBus, Address, 0x8021, (byte)FrameDelay, IsReversed ? (byte)1 : (byte)0);
							break;
						default:
							task = ValueTask.CompletedTask;
							break;
						}
						await task;
					}
					finalPendingChanges |= FinalPendingChanges.Commit;
				}
			}
			return finalPendingChanges;
		}

		public async ValueTask ApplyChangesAsync(FinalPendingChanges pendingChanges)
		{
			if ((pendingChanges & FinalPendingChanges.DynamicColors) != 0)
			{
				await WriteBytesAsync(Driver._smBus, Address, _dynamicColorBufferAddress, MemoryMarshal.Cast<RgbColor, byte>(LocalColors).ToArray());
			}
			if ((pendingChanges & FinalPendingChanges.Commit) != 0)
			{
				await WriteByteAsync(Driver._smBus, Address, 0x80A0, 0x01);
			}
			PendingChanges = 0;
		}

		bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorPulseEffect>.TryGetCurrentEffect(out ColorPulseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorFlashEffect>.TryGetCurrentEffect(out ColorFlashEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorCycleEffect>.TryGetCurrentEffect(out ColorCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorWaveEffect>.TryGetCurrentEffect(out ColorWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<WaveEffect>.TryGetCurrentEffect(out WaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorCyclePulseEffect>.TryGetCurrentEffect(out ColorCyclePulseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorCycleWaveEffect>.TryGetCurrentEffect(out ColorCycleWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorChaseEffect>.TryGetCurrentEffect(out ColorChaseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorCycleChaseEffect>.TryGetCurrentEffect(out ColorCycleChaseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<WideColorCycleChaseEffect>.TryGetCurrentEffect(out WideColorCycleChaseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<AlternateEffect>.TryGetCurrentEffect(out AlternateEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<CycleRandomFlashesEffect>.TryGetCurrentEffect(out CycleRandomFlashesEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<VariableColorFlashEffect>.TryGetCurrentEffect(out VariableColorFlashEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<VariableColorPulseEffect>.TryGetCurrentEffect(out VariableColorPulseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<VariableColorChaseEffect>.TryGetCurrentEffect(out VariableColorChaseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<VariableColorWaveEffect>.TryGetCurrentEffect(out VariableColorWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<VariableColorCycleEffect>.TryGetCurrentEffect(out VariableColorCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);

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
			var oldColors = MemoryMarshal.Cast<RgbColor, byte>(LocalColors);
			var newColors = MemoryMarshal.Cast<RgbColor, byte>(AsSpan(ref colors)[..ColorCount]);

			if (oldColors.SequenceEqual(newColors)) return false;

			newColors.CopyTo(oldColors);
			return true;
		}

		private void ApplySingleColorEffect<TEffect>(AuraEffect auraEffect, sbyte frameDelay, in TEffect effect)
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
				if (FrameDelay != frameDelay)
				{
					FrameDelay = frameDelay;
					changes |= EffectChanges.FrameDelay;
				}
				if (UpdateRawColors(SwapGreenAndBlue(effect.Color))) changes |= EffectChanges.Colors;
				PendingChanges = changes;
				CurrentEffect = effect;
			}
		}

		private void ApplyPredefinedEffect(AuraEffect auraEffect, sbyte frameDelay, ILightingEffect effect)
		{
			lock (Driver._lock)
			{
				if (AuraEffect == auraEffect) return;

				var changes = PendingChanges;
				if (AuraEffect == AuraEffect.Dynamic) changes ^= EffectChanges.Dynamic;
				AuraEffect = auraEffect;
				changes |= EffectChanges.Effect;
				if (FrameDelay != frameDelay)
				{
					FrameDelay = frameDelay;
					changes |= EffectChanges.FrameDelay;
				}
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
				_localColors = default;
				changes |= EffectChanges.Effect | EffectChanges.Colors;
				PendingChanges = changes;
				CurrentEffect = DisabledEffect.SharedInstance;
			}
		}

		void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect) => ApplySingleColorEffect(AuraEffect.Static, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<ColorPulseEffect>.ApplyEffect(in ColorPulseEffect effect) => ApplySingleColorEffect(AuraEffect.Pulse, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<ColorFlashEffect>.ApplyEffect(in ColorFlashEffect effect) => ApplySingleColorEffect(AuraEffect.Flash, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<ColorCycleEffect>.ApplyEffect(in ColorCycleEffect effect) => ApplyPredefinedEffect(AuraEffect.ColorCycle, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<ColorWaveEffect>.ApplyEffect(in ColorWaveEffect effect) => ApplyPredefinedEffect(AuraEffect.ColorWave, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<ColorCyclePulseEffect>.ApplyEffect(in ColorCyclePulseEffect effect) => ApplyPredefinedEffect(AuraEffect.CyclePulse, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<WaveEffect>.ApplyEffect(in WaveEffect effect) => ApplySingleColorEffect(AuraEffect.Wave, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<ColorCycleWaveEffect>.ApplyEffect(in ColorCycleWaveEffect effect) => ApplyPredefinedEffect(AuraEffect.CycleWave, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<ColorChaseEffect>.ApplyEffect(in ColorChaseEffect effect) => ApplySingleColorEffect(AuraEffect.Chase, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<ColorCycleChaseEffect>.ApplyEffect(in ColorCycleChaseEffect effect) => ApplyPredefinedEffect(AuraEffect.CycleChase, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<WideColorCycleChaseEffect>.ApplyEffect(in WideColorCycleChaseEffect effect) => ApplyPredefinedEffect(AuraEffect.WideCycleChase, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<AlternateEffect>.ApplyEffect(in AlternateEffect effect) => ApplyPredefinedEffect(AuraEffect.Alternate, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<CycleRandomFlashesEffect>.ApplyEffect(in CycleRandomFlashesEffect effect) => ApplyPredefinedEffect(AuraEffect.CycleRandomFlashes, DefaultFrameDelays[4], effect);
		void ILightingZoneEffect<VariableColorFlashEffect>.ApplyEffect(in VariableColorFlashEffect effect) => ApplySingleColorEffect(AuraEffect.Flash, DefaultFrameDelays[(byte)effect.Speed], effect);
		void ILightingZoneEffect<VariableColorPulseEffect>.ApplyEffect(in VariableColorPulseEffect effect) => ApplySingleColorEffect(AuraEffect.Pulse, DefaultFrameDelays[(byte)effect.Speed], effect);
		void ILightingZoneEffect<VariableColorCycleEffect>.ApplyEffect(in VariableColorCycleEffect effect) => ApplyPredefinedEffect(AuraEffect.ColorCycle, DefaultFrameDelays[(byte)effect.Speed], effect);
		void ILightingZoneEffect<VariableColorWaveEffect>.ApplyEffect(in VariableColorWaveEffect effect) => ApplyPredefinedEffect(AuraEffect.ColorWave, DefaultFrameDelays[(byte)effect.Speed], effect);
		void ILightingZoneEffect<VariableColorChaseEffect>.ApplyEffect(in VariableColorChaseEffect effect) => ApplyPredefinedEffect(AuraEffect.Chase, DefaultFrameDelays[(byte)effect.Speed], effect);
	}
}


