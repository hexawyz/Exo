using System.Runtime.CompilerServices;
using Exo.Lighting.Effects;
using Exo.Lighting;
using System.Runtime.InteropServices;
using Exo.Devices.Asus.Aura.Effects;
using Exo.ColorFormats;

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
		ILightingZoneEffect<VariableSpectrumCycleEffect>,
		ILightingZoneEffect<ReversibleVariableSpectrumWaveEffect>,
		ILightingZoneEffect<SpectrumCyclePulseEffect>,
		ILightingZoneEffect<ReversibleVariableColorWaveEffect>,
		ILightingZoneEffect<SpectrumCycleWaveEffect>,
		ILightingZoneEffect<ReversibleVariableColorChaseEffect>,
		ILightingZoneEffect<SpectrumCycleChaseEffect>,
		ILightingZoneEffect<WideSpectrumCycleChaseEffect>,
		ILightingZoneEffect<AlternateSpectrumEffect>,
		ILightingZoneEffect<SparklingSpectrumCycleEffect>
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
				goto InitializeMultiColor;
			case AuraEffect.Breathing:
				goto InitializeMultiColor;
			case AuraEffect.Flash:
				goto InitializeMultiColor;
			case AuraEffect.ColorCycle:
				CurrentEffect = new VariableSpectrumCycleEffect(GetEffectSpeed(description.FrameDelay));
				break;
			case AuraEffect.ColorWave:
				CurrentEffect = new ReversibleVariableSpectrumWaveEffect(GetEffectSpeed(description.FrameDelay), description.IsReversed ? EffectDirection1D.Backward : EffectDirection1D.Forward);
				break;
			case AuraEffect.CyclePulse:
				CurrentEffect = new SpectrumCyclePulseEffect(GetEffectSpeed(description.FrameDelay));
				break;
			case AuraEffect.Wave:
				CurrentEffect = new ReversibleVariableColorWaveEffect(SwapGreenAndBlue(LocalColors[0]), GetEffectSpeed(description.FrameDelay), description.IsReversed ? EffectDirection1D.Backward : EffectDirection1D.Forward);
				break;
			case AuraEffect.CycleWave:
				CurrentEffect = new SpectrumCycleWaveEffect(GetEffectSpeed(description.FrameDelay));
				break;
			case AuraEffect.Chase:
				CurrentEffect = new ReversibleVariableColorChaseEffect(SwapGreenAndBlue(LocalColors[0]), GetEffectSpeed(description.FrameDelay), description.IsReversed ? EffectDirection1D.Backward : EffectDirection1D.Forward);
				break;
			case AuraEffect.CycleChase:
				CurrentEffect = new SpectrumCycleChaseEffect(GetEffectSpeed(description.FrameDelay));
				break;
			case AuraEffect.WideCycleChase:
				CurrentEffect = new WideSpectrumCycleChaseEffect(GetEffectSpeed(description.FrameDelay));
				break;
			case AuraEffect.Alternate:
				CurrentEffect = new AlternateSpectrumEffect(GetEffectSpeed(description.FrameDelay));
				break;
			case AuraEffect.CycleRandomFlashes:
				CurrentEffect = new SparklingSpectrumCycleEffect(GetEffectSpeed(description.FrameDelay));
				break;
			case AuraEffect.Dynamic:
				CurrentEffect = DisabledEffect.SharedInstance;
				break;
			}
			return;
		InitializeMultiColor:;
			InitializeMultiColorCurrentEffect(colors, description.FrameDelay);
		}

		internal static PredeterminedEffectSpeed GetEffectSpeed(sbyte frameDelay)
			=> frameDelay switch
			{
				-2 => PredeterminedEffectSpeed.Faster,
				-1 => PredeterminedEffectSpeed.Fast,
				0 => PredeterminedEffectSpeed.MediumFast,
				1 => PredeterminedEffectSpeed.MediumSlow,
				2 => PredeterminedEffectSpeed.Slow,
				3 => PredeterminedEffectSpeed.Slower,
				_ => PredeterminedEffectSpeed.MediumSlow,
			};

		protected abstract void InitializeMultiColorCurrentEffect(ReadOnlySpan<RgbColor> colors, sbyte frameDelay);

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
							task = WriteByteAsync(Driver._smBus, Address, 0x8022, (byte)FrameDelay);
							break;
						case EffectChanges.Effect | EffectChanges.FrameDelay:
							task = WriteBytesAsync(Driver._smBus, Address, 0x8021, (byte)effect, (byte)FrameDelay);
							break;
						case EffectChanges.Direction:
							task = WriteByteAsync(Driver._smBus, Address, 0x8023, IsReversed ? (byte)1 : (byte)0);
							break;
						case EffectChanges.Effect | EffectChanges.Direction:
						case EffectChanges.Effect | EffectChanges.FrameDelay | EffectChanges.Direction:
							task = WriteBytesAsync(Driver._smBus, Address, 0x8021, (byte)effect, (byte)FrameDelay, IsReversed ? (byte)1 : (byte)0);
							break;
						case EffectChanges.FrameDelay | EffectChanges.Direction:
							task = WriteBytesAsync(Driver._smBus, Address, 0x8022, (byte)FrameDelay, IsReversed ? (byte)1 : (byte)0);
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

		public async ValueTask PersistChangesAsync()
		{
			if (AuraEffect != AuraEffect.Dynamic)
			{
				await WriteByteAsync(Driver._smBus, Address, 0x80A0, 0xAA);
			}
		}

		bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ReversibleVariableSpectrumWaveEffect>.TryGetCurrentEffect(out ReversibleVariableSpectrumWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<SpectrumCyclePulseEffect>.TryGetCurrentEffect(out SpectrumCyclePulseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<SpectrumCycleWaveEffect>.TryGetCurrentEffect(out SpectrumCycleWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<SpectrumCycleChaseEffect>.TryGetCurrentEffect(out SpectrumCycleChaseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<WideSpectrumCycleChaseEffect>.TryGetCurrentEffect(out WideSpectrumCycleChaseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<AlternateSpectrumEffect>.TryGetCurrentEffect(out AlternateSpectrumEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<SparklingSpectrumCycleEffect>.TryGetCurrentEffect(out SparklingSpectrumCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<VariableSpectrumCycleEffect>.TryGetCurrentEffect(out VariableSpectrumCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ReversibleVariableColorWaveEffect>.TryGetCurrentEffect(out ReversibleVariableColorWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ReversibleVariableColorChaseEffect>.TryGetCurrentEffect(out ReversibleVariableColorChaseEffect effect) => CurrentEffect.TryGetEffect(out effect);

		[SkipLocalsInit]
		private bool UpdateRawColors(RgbColor color)
		{
			TenColorArray newColors;
			Unsafe.SkipInit(out newColors);
			AsSpan(ref newColors)[..ColorCount].Fill(color);
			return UpdateRawColors(ref newColors);
		}

		private bool UpdateRawColors(ref TenColorArray colors)
		{
			var oldColors = MemoryMarshal.Cast<RgbColor, byte>(LocalColors);
			var newColors = MemoryMarshal.Cast<RgbColor, byte>(AsSpan(ref colors)[..ColorCount]);

			if (oldColors.SequenceEqual(newColors)) return false;

			newColors.CopyTo(oldColors);
			return true;
		}

		[SkipLocalsInit]
		protected bool UpdateColors(ReadOnlySpan<RgbColor> colors)
		{
			TenColorArray newColors;
			Unsafe.SkipInit(out newColors);
			var span = (Span<RgbColor>)newColors;
			for (int i = 0; i < colors.Length; i++)
			{
				span[i] = SwapGreenAndBlue(colors[i]);
			}
			return UpdateRawColors(ref newColors);
		}

		private void ApplySingleColorEffect<TEffect>(AuraEffect auraEffect, sbyte frameDelay, bool reverse, in TEffect effect)
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
				if (IsReversed != reverse)
				{
					IsReversed = reverse;
					changes |= EffectChanges.Direction;
				}
				if (UpdateRawColors(SwapGreenAndBlue(effect.Color))) changes |= EffectChanges.Colors;
				PendingChanges = changes;
				CurrentEffect = effect;
			}
		}

		protected void ApplyColorEffect(AuraEffect auraEffect, sbyte frameDelay, ReadOnlySpan<RgbColor> colors, ILightingEffect effect)
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
				if (UpdateColors(colors)) changes |= EffectChanges.Colors;
				PendingChanges = changes;
				CurrentEffect = effect;
			}
		}

		private void ApplyPredefinedEffect(AuraEffect auraEffect, sbyte frameDelay, bool reverse, ILightingEffect effect)
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
				if (IsReversed != reverse)
				{
					IsReversed = reverse;
					changes |= EffectChanges.Direction;
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

		void ILightingZoneEffect<ReversibleVariableSpectrumWaveEffect>.ApplyEffect(in ReversibleVariableSpectrumWaveEffect effect) => ApplyPredefinedEffect(AuraEffect.ColorWave, DefaultFrameDelays[(byte)effect.Speed], effect.Direction != EffectDirection1D.Forward, effect);
		void ILightingZoneEffect<SpectrumCyclePulseEffect>.ApplyEffect(in SpectrumCyclePulseEffect effect) => ApplyPredefinedEffect(AuraEffect.CyclePulse, DefaultFrameDelays[(byte)effect.Speed], false, effect);
		void ILightingZoneEffect<ReversibleVariableColorWaveEffect>.ApplyEffect(in ReversibleVariableColorWaveEffect effect) => ApplySingleColorEffect(AuraEffect.Wave, DefaultFrameDelays[(byte)effect.Speed], effect.Direction != EffectDirection1D.Forward, effect);
		void ILightingZoneEffect<SpectrumCycleWaveEffect>.ApplyEffect(in SpectrumCycleWaveEffect effect) => ApplyPredefinedEffect(AuraEffect.CycleWave, DefaultFrameDelays[(byte)effect.Speed], false, effect);
		void ILightingZoneEffect<ReversibleVariableColorChaseEffect>.ApplyEffect(in ReversibleVariableColorChaseEffect effect) => ApplySingleColorEffect(AuraEffect.Chase, DefaultFrameDelays[(byte)effect.Speed], effect.Direction != EffectDirection1D.Forward, effect);
		void ILightingZoneEffect<SpectrumCycleChaseEffect>.ApplyEffect(in SpectrumCycleChaseEffect effect) => ApplyPredefinedEffect(AuraEffect.CycleChase, DefaultFrameDelays[(byte)effect.Speed], false, effect);
		void ILightingZoneEffect<WideSpectrumCycleChaseEffect>.ApplyEffect(in WideSpectrumCycleChaseEffect effect) => ApplyPredefinedEffect(AuraEffect.WideCycleChase, DefaultFrameDelays[(byte)effect.Speed], false, effect);
		void ILightingZoneEffect<AlternateSpectrumEffect>.ApplyEffect(in AlternateSpectrumEffect effect) => ApplyPredefinedEffect(AuraEffect.Alternate, DefaultFrameDelays[(byte)effect.Speed], false, effect);
		void ILightingZoneEffect<SparklingSpectrumCycleEffect>.ApplyEffect(in SparklingSpectrumCycleEffect effect) => ApplyPredefinedEffect(AuraEffect.CycleRandomFlashes, DefaultFrameDelays[(byte)effect.Speed], false, effect);
		void ILightingZoneEffect<VariableSpectrumCycleEffect>.ApplyEffect(in VariableSpectrumCycleEffect effect) => ApplyPredefinedEffect(AuraEffect.ColorCycle, DefaultFrameDelays[(byte)effect.Speed], false, effect);
	}
}
