using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Exo.ColorFormats;
using Exo.Devices.Nzxt.LightingEffects;
using Exo.Lighting;
using Exo.Lighting.Effects;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Nzxt.Kraken;

public partial class KrakenDriver
{
	private sealed class LightingZone :
		ILightingZone,
		ILightingZoneEffect<DisabledEffect>,
		ILightingZoneEffect<StaticColorEffect>,
		ILightingZoneEffect<VariableMultiColorCycleEffect>,
		ILightingZoneEffect<ReversibleVariableSpectrumWaveEffect>,
		ILightingZoneEffect<LegacyReversibleVariableMultiColorMarqueeEffect>,
		ILightingZoneEffect<AlternatingEffect>,
		ILightingZoneEffect<VariableMultiColorPulseEffect>,
		ILightingZoneEffect<VariableMultiColorBreathingEffect>,
		ILightingZoneEffect<VariableColorBlinkEffect>,
		ILightingZoneEffect<CandleEffect>,
		ILightingZoneEffect<ReversibleVariableRainbowWaveEffect>,
		ILightingZoneEffect<ReversibleVariableSuperRainbowEffect>,
		ILightingZoneEffect<ReversibleVariableRainbowPulseEffect>,
		ILightingZoneEffect<StarryNightEffect>,
		ILightingZoneEffect<TaiChiEffect>,
		ILightingZoneEffect<LiquidCoolerEffect>,
		ILightingZoneEffect<CoveringMarqueeEffect>,
		ILightingZoneEffect<ReversibleVariableColorLoadingEffect>
	{
		const byte DefaultStaticSpeed = 0x32;
		const byte DefaultSize = 0x03;

		// NB: Takes the speeds from CAM but insert an extra one between "normal" and "fast" in order to match the predetermined model used in Exo.
		// CAM <=> Exo:
		// Slower <=> Slower
		// Slow <=> Slow
		// Normal <=> Medium Slow
		// <nothing> <=> Medium fast
		// Fast <=> Fast
		// Faster <=> Faster
		private static ReadOnlySpan<ushort> PulseSpeeds => [0x19, 0x14, 0x0f, 0x0a, 0x07, 0x04];
		private static ReadOnlySpan<ushort> StarryNightSpeeds => PulseSpeeds;
		private static ReadOnlySpan<ushort> BreathingSpeeds => [0x28, 0x1e, 0x14, 0x0f, 0x0a, 0x04];
		private static ReadOnlySpan<ushort> FadeSpeeds => [0x50, 0x3c, 0x28, 0x1e, 0x14, 0x0a];
		// Loading effect in CAM always uses speed 0x14. The values below were chosen by taking pulse timings for reference.
		private static ReadOnlySpan<ushort> LoadingSpeeds => [0x1e, 0x19, 0x14, 0x0f, 0x07, 0x04];
		private static ReadOnlySpan<ushort> SpectrumWaveSpeeds => [350, 300, 250, 220, 150, 80];
		private static ReadOnlySpan<ushort> CoveringMarqueeSpeeds => SpectrumWaveSpeeds;
		private static ReadOnlySpan<ushort> MarqueeSpeeds => SpectrumWaveSpeeds;
		private static ReadOnlySpan<ushort> RainbowWaveSpeeds => SpectrumWaveSpeeds;
		private static ReadOnlySpan<ushort> SuperRainbowSpeeds => SpectrumWaveSpeeds;
		private static ReadOnlySpan<ushort> RainbowPulseSpeeds => SpectrumWaveSpeeds;
		private static ReadOnlySpan<ushort> TaiChiSpeeds => [0x32, 0x28, 0x1e, 0x19, 0x14, 0x0a];
		private static ReadOnlySpan<ushort> LiquidCoolerSpeeds => TaiChiSpeeds;
		// The timings are x2 when the alternating effect is not moving.
		// NB: Mapping from CAM is a bit different than other effects here: Timing 600 is the one that has been inserted.
		private static ReadOnlySpan<ushort> AlternatingBaseSpeeds => [800, 700, 600, 500, 400, 300];
		// Blink effect is quite fast. I took the speeds of the "non-moving" alternating effect for reference, as it is similar to a blinking effect.
		private static ReadOnlySpan<ushort> BlinkSpeeds => [1600, 1400, 1200, 1000, 800, 600];

		private readonly RgbColor[] _colors;
		private readonly Guid _zoneId;
		// NB: As in most types in Exo, fields are ordered to reduce the amount of padding as much as possible.
		private readonly byte _channelId;
		private readonly byte _accessoryId;
		private readonly byte _ledCount;
		private KrakenEffect _effectId;
		private ushort _speed;
		private byte _colorCount;
		private LightingEffectFlags _flags;
		private byte _parameter2;
		private byte _size;
		private bool _hasChanged;

		public LightingZone(Guid zoneId, byte channelId, byte accessoryId, byte colorCount)
		{
			_zoneId = zoneId;
			_channelId = channelId;
			_accessoryId = accessoryId;
			_ledCount = colorCount;
			_colors = new RgbColor[Math.Max(8, (uint)colorCount)];
			_effectId = KrakenEffect.Static;
			_colorCount = 1;
			_speed = 0x32;
			_size = DefaultSize;
		}

		Guid ILightingZone.ZoneId => _zoneId;

		ILightingEffect ILightingZone.GetCurrentEffect()
		{
			ushort speed = _speed;
			int speedIndex;
			switch (_effectId)
			{
			case KrakenEffect.Static:
				return _colors[0] == default ? DisabledEffect.SharedInstance : new StaticColorEffect(_colors[0]);
			case KrakenEffect.Fade:
				return new VariableMultiColorCycleEffect(new(_colors.AsSpan(0, _colorCount)), (speedIndex = FadeSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow);
			case KrakenEffect.SpectrumWave:
				return new ReversibleVariableSpectrumWaveEffect((speedIndex = SpectrumWaveSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
			case KrakenEffect.Marquee:
				return new LegacyReversibleVariableMultiColorMarqueeEffect(new(_colors.AsSpan(0, _colorCount)), (speedIndex = LiquidCoolerSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward, _size);
			case KrakenEffect.CoveringMarquee:
				return new CoveringMarqueeEffect(new(_colors.AsSpan(0, _colorCount)), (speedIndex = LiquidCoolerSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
			case KrakenEffect.Alternating:
				// There are a few deviations from the implementation of other effects for this one.
				// First one is that the default speed will be "medium fast" instead of "medium slow" (to try matching the "normal" setting of CAM)
				// Second one is that speeds are different depending on whether the effect is moving or not.
				if ((_flags & LightingEffectFlags.Moving) == 0) speed >>>= 1;
				return new AlternatingEffect
				(
					CreateTwoColorArray(_colors),
					(speedIndex = AlternatingBaseSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumFast,
					(_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward,
					_size,
					(_flags & LightingEffectFlags.Moving) != 0
				);
			case KrakenEffect.Pulse:
				return new VariableMultiColorPulseEffect(new(_colors.AsSpan(0, _colorCount)), (speedIndex = PulseSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow);
			case KrakenEffect.Breathing:
				return new VariableMultiColorBreathingEffect(new(_colors.AsSpan(0, _colorCount)), (speedIndex = BreathingSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow);
			case KrakenEffect.Candle:
				return new CandleEffect(_colors[0]);
			case KrakenEffect.StarryNight:
				return new StarryNightEffect(_colors[0], (speedIndex = StarryNightSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow);
			case KrakenEffect.Blink:
				return new VariableColorBlinkEffect(_colors[0], (speedIndex = BreathingSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumFast);
			case KrakenEffect.RainbowWave:
				return new ReversibleVariableRainbowWaveEffect((speedIndex = RainbowWaveSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
			case KrakenEffect.SuperRainbow:
				return new ReversibleVariableSuperRainbowEffect((speedIndex = RainbowWaveSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
			case KrakenEffect.RainbowPulse:
				return new ReversibleVariableRainbowPulseEffect((speedIndex = RainbowWaveSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
			case KrakenEffect.TaiChi:
				return new TaiChiEffect(CreateTwoColorArray(_colors), (speedIndex = TaiChiSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
			case KrakenEffect.LiquidCooler:
				return new LiquidCoolerEffect(CreateTwoColorArray(_colors), (speedIndex = LiquidCoolerSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
			case KrakenEffect.Loading:
				return new ReversibleVariableColorLoadingEffect(_colors[0], (speedIndex = StarryNightSpeeds.IndexOf(speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
			default:
				throw new NotImplementedException();
			}
		}

		[SkipLocalsInit]
		private static FixedArray2<RgbColor> CreateTwoColorArray(ReadOnlySpan<RgbColor> colors)
		{
			if (colors.Length < 2) throw new ArgumentException(null, nameof(colors));
			FixedArray2<RgbColor> array;
			Unsafe.SkipInit(out array);
			colors[..2].CopyTo(array);
			return array;
		}

		void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect)
		{
			if (_effectId != KrakenEffect.Static || _colorCount != 1 || _colors[0] != default || _speed != DefaultStaticSpeed || _flags != 0x00 && _parameter2 != 0x00 || _size != DefaultSize)
			{
				_effectId = KrakenEffect.Static;
				_colors.AsSpan(0, _colorCount).Clear();
				_colorCount = 1;
				_speed = DefaultStaticSpeed;
				_flags = 0x00;
				_parameter2 = 0x00;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect)
		{
			if (_effectId != KrakenEffect.Static || _colorCount != 1 || _colors[0] != effect.Color || _speed != DefaultStaticSpeed || _flags != 0x00 && _parameter2 != 0x00 || _size != 0x03)
			{
				_effectId = KrakenEffect.Static;
				_colors[0] = effect.Color;
				if (_colorCount > 1)
				{
					_colors.AsSpan(1, _colorCount - 1).Clear();
				}
				_colorCount = 1;
				_speed = DefaultStaticSpeed;
				_flags = 0x00;
				_parameter2 = 0x00;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<VariableMultiColorCycleEffect>.ApplyEffect(in VariableMultiColorCycleEffect effect)
		{
			if (effect.Colors.Count is < 2 or > 8) throw new ArgumentException("The effect requires between two to eight colors.");

			if (_effectId != KrakenEffect.Fade ||
				_colorCount != effect.Colors.Count ||
				!_colors.AsSpan(0, _colorCount).SequenceEqual(effect.Colors) ||
				FadeSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != 0x00 ||
				_parameter2 != 0x08 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.Fade;
				effect.Colors.CopyTo(_colors);
				_colorCount = (byte)effect.Colors.Count;
				_speed = FadeSpeeds[(int)effect.Speed];
				_flags = 0x00;
				_parameter2 = 0x08;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<VariableMultiColorPulseEffect>.ApplyEffect(in VariableMultiColorPulseEffect effect)
		{
			if (effect.Colors.Count is < 1 or > 8) throw new ArgumentException("The effect requires between one to eight colors.");

			if (_effectId != KrakenEffect.Pulse ||
				_colorCount != effect.Colors.Count ||
				!_colors.AsSpan(0, _colorCount).SequenceEqual(effect.Colors) ||
				PulseSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != 0x00 ||
				_parameter2 != 0x08 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.Pulse;
				effect.Colors.CopyTo(_colors);
				_colorCount = (byte)effect.Colors.Count;
				_speed = PulseSpeeds[(int)effect.Speed];
				_flags = 0x00;
				_parameter2 = 0x08;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<VariableMultiColorBreathingEffect>.ApplyEffect(in VariableMultiColorBreathingEffect effect)
		{
			if (effect.Colors.Count is < 1 or > 8) throw new ArgumentException("The effect requires between one to eight colors.");

			if (_effectId != KrakenEffect.Breathing ||
				_colorCount != effect.Colors.Count ||
				!_colors.AsSpan(0, _colorCount).SequenceEqual(effect.Colors) ||
				BreathingSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != 0x00 ||
				_parameter2 != 0x08 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.Breathing;
				effect.Colors.CopyTo(_colors);
				_colorCount = (byte)effect.Colors.Count;
				_speed = BreathingSpeeds[(int)effect.Speed];
				_flags = 0x00;
				_parameter2 = 0x08;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<VariableColorBlinkEffect>.ApplyEffect(in VariableColorBlinkEffect effect)
		{
			if (_effectId != KrakenEffect.Blink ||
				_colorCount != 1 ||
				_colors[0] != effect.Color ||
				BlinkSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != 0x00 ||
				_parameter2 != 0x08 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.Blink;
				_colors[0] = effect.Color;
				if (_colorCount > 1)
				{
					_colors.AsSpan(1, _colorCount - 1).Clear();
				}
				_colorCount = 1;
				_speed = BlinkSpeeds[(int)effect.Speed];
				_flags = (LightingEffectFlags)0x07;
				_parameter2 = 0x08;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<AlternatingEffect>.ApplyEffect(in AlternatingEffect effect)
		{
			if (_effectId != KrakenEffect.Alternating ||
				_colorCount != 2 ||
				_colors[0] != effect.Colors[0] ||
				_colors[1] != effect.Colors[1] ||
				AlternatingBaseSpeeds.IndexOf((_flags & LightingEffectFlags.Moving) == 0 ? (byte)(_speed >>> 1) : _speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				(_flags & (LightingEffectFlags.Moving | LightingEffectFlags.Reversed)) != 0 ||
				_parameter2 != 0x00 ||
				_size != effect.Size)
			{
				_effectId = KrakenEffect.Alternating;
				_colors[0] = effect.Colors[0];
				_colors[1] = effect.Colors[1];
				if (_colorCount > 2)
				{
					_colors.AsSpan(2, _colorCount - 2).Clear();
				}
				_colorCount = 2;
				ushort speed = AlternatingBaseSpeeds[(int)effect.Speed];
				LightingEffectFlags flags = LightingEffectFlags.None;
				if (effect.Direction != EffectDirection1D.Forward) flags |= LightingEffectFlags.Reversed;
				if (effect.IsMoving) flags |= LightingEffectFlags.Moving;
				else speed <<= 1;
				_speed = speed;
				_flags = flags;
				_parameter2 = 0x00;
				_size = effect.Size;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<CandleEffect>.ApplyEffect(in CandleEffect effect)
		{
			if (_effectId != KrakenEffect.Candle || _colorCount != 1 || _colors[0] != effect.Color || _speed != DefaultStaticSpeed || _flags != 0x00 && _parameter2 != 0x00 || _size != DefaultSize)
			{
				_effectId = KrakenEffect.Candle;
				_colors[0] = effect.Color;
				if (_colorCount > 1)
				{
					_colors.AsSpan(1, _colorCount - 1).Clear();
				}
				_colorCount = 1;
				_speed = DefaultStaticSpeed;
				_flags = 0x00;
				_parameter2 = 0x00;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<StarryNightEffect>.ApplyEffect(in StarryNightEffect effect)
		{
			if (_effectId != KrakenEffect.StarryNight || _colorCount != 1 || _colors[0] != effect.Color || StarryNightSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) || _flags != LightingEffectFlags.Moving && _parameter2 != 0x00 || _size != DefaultSize)
			{
				_effectId = KrakenEffect.StarryNight;
				_colors[0] = effect.Color;
				if (_colorCount > 1)
				{
					_colors.AsSpan(1, _colorCount - 1).Clear();
				}
				_colorCount = 1;
				_speed = StarryNightSpeeds[(int)effect.Speed];
				_flags = LightingEffectFlags.Moving;
				_parameter2 = 0x00;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<TaiChiEffect>.ApplyEffect(in TaiChiEffect effect)
		{
			if (_effectId != KrakenEffect.TaiChi ||
				_colorCount != 2 ||
				_colors[0] != effect.Colors[0] ||
				_colors[1] != effect.Colors[1] ||
				TaiChiSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None) ||
				_parameter2 != 0x05 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.TaiChi;
				_colors[0] = effect.Colors[0];
				_colors[1] = effect.Colors[1];
				if (_colorCount > 2)
				{
					_colors.AsSpan(2, _colorCount - 2).Clear();
				}
				_colorCount = 2;
				_speed = TaiChiSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None;
				_parameter2 = 0x05;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<LiquidCoolerEffect>.ApplyEffect(in LiquidCoolerEffect effect)
		{
			if (_effectId != KrakenEffect.LiquidCooler ||
				_colorCount != 2 ||
				_colors[0] != effect.Colors[0] ||
				_colors[1] != effect.Colors[1] ||
				LiquidCoolerSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None) ||
				_parameter2 != 0x05 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.LiquidCooler;
				_colors[0] = effect.Colors[0];
				_colors[1] = effect.Colors[1];
				if (_colorCount > 2)
				{
					_colors.AsSpan(2, _colorCount - 2).Clear();
				}
				_colorCount = 2;
				_speed = LiquidCoolerSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None;
				_parameter2 = 0x05;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<ReversibleVariableSpectrumWaveEffect>.ApplyEffect(in ReversibleVariableSpectrumWaveEffect effect)
		{
			if (_effectId != KrakenEffect.SpectrumWave ||
				_colorCount != 0 ||
				SpectrumWaveSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None) ||
				_parameter2 != 0x00 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.SpectrumWave;
				_colors.AsSpan(0, _colorCount).Clear();
				_colorCount = 0;
				_speed = SpectrumWaveSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None;
				_parameter2 = 0x00;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<ReversibleVariableRainbowWaveEffect>.ApplyEffect(in ReversibleVariableRainbowWaveEffect effect)
		{
			if (_effectId != KrakenEffect.RainbowWave ||
				_colorCount != 0 ||
				RainbowWaveSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None) ||
				_parameter2 != 0x00 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.RainbowWave;
				_colors.AsSpan(0, _colorCount).Clear();
				_colorCount = 8;
				_speed = RainbowWaveSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None;
				_parameter2 = 0x00;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<ReversibleVariableSuperRainbowEffect>.ApplyEffect(in ReversibleVariableSuperRainbowEffect effect)
		{
			if (_effectId != KrakenEffect.SuperRainbow ||
				_colorCount != 0 ||
				SuperRainbowSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None) ||
				_parameter2 != 0x00 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.SuperRainbow;
				_colors.AsSpan(0, _colorCount).Clear();
				_colorCount = 0;
				_speed = SuperRainbowSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None;
				_parameter2 = 0x00;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<ReversibleVariableRainbowPulseEffect>.ApplyEffect(in ReversibleVariableRainbowPulseEffect effect)
		{
			if (_effectId != KrakenEffect.RainbowPulse ||
				_colorCount != 0 ||
				RainbowPulseSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None) ||
				_parameter2 != 0x00 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.RainbowPulse;
				_colors.AsSpan(0, _colorCount).Clear();
				_colorCount = 0;
				_speed = RainbowPulseSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None;
				_parameter2 = 0x00;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<LegacyReversibleVariableMultiColorMarqueeEffect>.ApplyEffect(in LegacyReversibleVariableMultiColorMarqueeEffect effect)
		{
			if (effect.Colors.Count is < 1 or > 8) throw new ArgumentException("The effect requires between one to eight colors.");

			if (_effectId != KrakenEffect.Marquee ||
				_colorCount != effect.Colors.Count ||
				!_colors.AsSpan(0, _colorCount).SequenceEqual(effect.Colors) ||
				MarqueeSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? (LightingEffectFlags.Unknown1 | LightingEffectFlags.Reversed) : LightingEffectFlags.Unknown1) ||
				_parameter2 != 0x00 ||
				_size != effect.Size)
			{
				_effectId = KrakenEffect.Marquee;
				effect.Colors.CopyTo(_colors);
				_colorCount = (byte)effect.Colors.Count;
				_speed = MarqueeSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? (LightingEffectFlags.Unknown1 | LightingEffectFlags.Reversed) : LightingEffectFlags.Unknown1;
				_parameter2 = 0x00;
				_size = effect.Size;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<CoveringMarqueeEffect>.ApplyEffect(in CoveringMarqueeEffect effect)
		{
			if (effect.Colors.Count is < 1 or > 8) throw new ArgumentException("The effect requires between one to eight colors.");

			if (_effectId != KrakenEffect.CoveringMarquee ||
				_colorCount != effect.Colors.Count ||
				!_colors.AsSpan(0, _colorCount).SequenceEqual(effect.Colors) ||
				CoveringMarqueeSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? (LightingEffectFlags.Unknown1 | LightingEffectFlags.Reversed) : LightingEffectFlags.Unknown1) ||
				_parameter2 != 0x00 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.CoveringMarquee;
				effect.Colors.CopyTo(_colors);
				_colorCount = (byte)effect.Colors.Count;
				_speed = CoveringMarqueeSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? (LightingEffectFlags.Unknown1 | LightingEffectFlags.Reversed) : LightingEffectFlags.Unknown1;
				_parameter2 = 0x00;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<ReversibleVariableColorLoadingEffect>.ApplyEffect(in ReversibleVariableColorLoadingEffect effect)
		{
			if (_effectId != KrakenEffect.Loading ||
				_colorCount != 1 ||
				_colors[0] != effect.Color ||
				LoadingSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None) ||
				_parameter2 != 0x04 ||
				_size != DefaultSize)
			{
				_effectId = KrakenEffect.Loading;
				_colors[0] = effect.Color;
				if (_colorCount > 1)
				{
					_colors.AsSpan(1, _colorCount - 1).Clear();
				}
				_colorCount = 1;
				_speed = LoadingSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? LightingEffectFlags.Reversed : LightingEffectFlags.None;
				_parameter2 = 0x04;
				_size = DefaultSize;
				_hasChanged = true;
			}
		}

		bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect)
		{
			effect = default;
			return _effectId == KrakenEffect.Static && _colorCount == 1 && _colors[0] == default && _speed == DefaultStaticSpeed && _flags == 0x00 && _parameter2 == 0x00 && _size == DefaultSize;
		}

		bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect)
		{
			if (_effectId == KrakenEffect.Static && _colorCount == 1 && _speed == DefaultStaticSpeed && _flags == 0x00 && _parameter2 == 0x00 && _size == DefaultSize)
			{
				effect = new(_colors[0]);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<VariableMultiColorCycleEffect>.TryGetCurrentEffect(out VariableMultiColorCycleEffect effect)
		{
			if (_effectId == KrakenEffect.Pulse &&
				_colorCount >= 1 &&
				FadeSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags == 0x00 &&
				_parameter2 == 0x08 &&
				_size == DefaultSize)
			{
				effect = new(new(_colors.AsSpan(0, _colorCount)), (PredeterminedEffectSpeed)speedIndex);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<VariableMultiColorPulseEffect>.TryGetCurrentEffect(out VariableMultiColorPulseEffect effect)
		{
			if (_effectId == KrakenEffect.Pulse &&
				_colorCount >= 1 &&
				PulseSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags == 0x00 &&
				_parameter2 == 0x08 &&
				_size == DefaultSize)
			{
				effect = new(new(_colors.AsSpan(0, _colorCount)), (PredeterminedEffectSpeed)speedIndex);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<VariableMultiColorBreathingEffect>.TryGetCurrentEffect(out VariableMultiColorBreathingEffect effect)
		{
			if (_effectId == KrakenEffect.Breathing &&
				_colorCount >= 1 &&
				BreathingSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags == 0x00 &&
				_parameter2 == 0x08 &&
				_size == DefaultSize)
			{
				effect = new(new(_colors.AsSpan(0, _colorCount)), (PredeterminedEffectSpeed)speedIndex);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<VariableColorBlinkEffect>.TryGetCurrentEffect(out VariableColorBlinkEffect effect)
		{
			if (_effectId == KrakenEffect.Blink &&
				_colorCount == 1 &&
				BlinkSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags == 0x00 &&
				_parameter2 == 0x08 &&
				_size == DefaultSize)
			{
				effect = new(_colors[0], (PredeterminedEffectSpeed)speedIndex);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<AlternatingEffect>.TryGetCurrentEffect(out AlternatingEffect effect)
		{
			if (_effectId == KrakenEffect.Alternating &&
				_colorCount == 2 &&
				AlternatingBaseSpeeds.IndexOf((_flags & LightingEffectFlags.Moving) == 0 ? (byte)(_speed >>> 1) : _speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is LightingEffectFlags.None or LightingEffectFlags.Reversed &&
				_parameter2 == 0x05 &&
				_size is >= DefaultSize and <= 0x06)
			{
				effect = new
				(
					CreateTwoColorArray(_colors),
					(PredeterminedEffectSpeed)speedIndex,
					(_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward,
					_size,
					(_flags & LightingEffectFlags.Moving) != 0
				);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<CandleEffect>.TryGetCurrentEffect(out CandleEffect effect)
		{
			if (_effectId == KrakenEffect.Candle && _colorCount == 1 && _speed == DefaultStaticSpeed && _flags == 0x00 && _parameter2 == 0x00 && _size == DefaultSize)
			{
				effect = new(_colors[0]);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<StarryNightEffect>.TryGetCurrentEffect(out StarryNightEffect effect)
		{
			if (_effectId == KrakenEffect.Pulse && _colorCount == 1 && StarryNightSpeeds.IndexOf(_speed) is int speedIndex && speedIndex >= 0 && _flags == 0x00 && _parameter2 == 0x08 && _size == DefaultSize)
			{
				effect = new(_colors[0], (PredeterminedEffectSpeed)speedIndex);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<TaiChiEffect>.TryGetCurrentEffect(out TaiChiEffect effect)
		{
			if (_effectId == KrakenEffect.TaiChi &&
				_colorCount == 2 &&
				TaiChiSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is LightingEffectFlags.None or LightingEffectFlags.Reversed &&
				_parameter2 == 0x05 &&
				_size == DefaultSize)
			{
				effect = new(CreateTwoColorArray(_colors), (PredeterminedEffectSpeed)speedIndex, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<LiquidCoolerEffect>.TryGetCurrentEffect(out LiquidCoolerEffect effect)
		{
			if (_effectId == KrakenEffect.LiquidCooler &&
				_colorCount == 2 &&
				LiquidCoolerSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is LightingEffectFlags.None or LightingEffectFlags.Reversed &&
				_parameter2 == 0x05 &&
				_size == DefaultSize)
			{
				effect = new(CreateTwoColorArray(_colors), (PredeterminedEffectSpeed)speedIndex, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<ReversibleVariableSpectrumWaveEffect>.TryGetCurrentEffect(out ReversibleVariableSpectrumWaveEffect effect)
		{
			if (_effectId == KrakenEffect.SpectrumWave &&
				_colorCount == 0 &&
				SpectrumWaveSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is LightingEffectFlags.None or LightingEffectFlags.Reversed &&
				_parameter2 == 0x00 &&
				_size == DefaultSize)
			{
				effect = new((PredeterminedEffectSpeed)speedIndex, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<ReversibleVariableRainbowWaveEffect>.TryGetCurrentEffect(out ReversibleVariableRainbowWaveEffect effect)
		{
			if (_effectId == KrakenEffect.RainbowWave &&
				_colorCount == 0 &&
				RainbowWaveSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is LightingEffectFlags.None or LightingEffectFlags.Reversed &&
				_parameter2 == 0x00 &&
				_size == DefaultSize)
			{
				effect = new((PredeterminedEffectSpeed)speedIndex, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<ReversibleVariableSuperRainbowEffect>.TryGetCurrentEffect(out ReversibleVariableSuperRainbowEffect effect)
		{
			if (_effectId == KrakenEffect.SuperRainbow &&
				_colorCount == 0 &&
				SpectrumWaveSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is LightingEffectFlags.None or LightingEffectFlags.Reversed &&
				_parameter2 == 0x00 &&
				_size == DefaultSize)
			{
				effect = new((PredeterminedEffectSpeed)speedIndex, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<ReversibleVariableRainbowPulseEffect>.TryGetCurrentEffect(out ReversibleVariableRainbowPulseEffect effect)
		{
			if (_effectId == KrakenEffect.RainbowPulse &&
				_colorCount == 0 &&
				SpectrumWaveSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is LightingEffectFlags.None or LightingEffectFlags.Reversed &&
				_parameter2 == 0x00 &&
				_size == DefaultSize)
			{
				effect = new((PredeterminedEffectSpeed)speedIndex, (_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
				return true;
			}
			effect = default;
			return false;
		}


		bool ILightingZoneEffect<LegacyReversibleVariableMultiColorMarqueeEffect>.TryGetCurrentEffect(out LegacyReversibleVariableMultiColorMarqueeEffect effect)
		{
			if (_effectId == KrakenEffect.Marquee &&
				_colorCount >= 1 &&
				MarqueeSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is LightingEffectFlags.Unknown1 or (LightingEffectFlags.Unknown1 | LightingEffectFlags.Reversed) &&
				_parameter2 == 0x00 &&
				_size is >= DefaultSize and <= 0x06)
			{
				effect = new
				(
					new(_colors.AsSpan(0, _colorCount)),
					(PredeterminedEffectSpeed)speedIndex,
					(_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward,
					_size
				);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<CoveringMarqueeEffect>.TryGetCurrentEffect(out CoveringMarqueeEffect effect)
		{
			if (_effectId == KrakenEffect.CoveringMarquee &&
				_colorCount >= 1 &&
				CoveringMarqueeSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is LightingEffectFlags.Unknown1 or (LightingEffectFlags.Unknown1 | LightingEffectFlags.Reversed) &&
				_parameter2 == 0x00 &&
				_size == DefaultSize)
			{
				effect = new
				(
					new(_colors.AsSpan(0, _colorCount)),
					(PredeterminedEffectSpeed)speedIndex,
					(_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward
				);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<ReversibleVariableColorLoadingEffect>.TryGetCurrentEffect(out ReversibleVariableColorLoadingEffect effect)
		{
			if (_effectId == KrakenEffect.Loading &&
				_colorCount >= 1 &&
				LoadingSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is LightingEffectFlags.None or LightingEffectFlags.Reversed &&
				_parameter2 == 0x04 &&
				_size == DefaultSize)
			{
				effect = new
				(
					_colors[0],
					(PredeterminedEffectSpeed)speedIndex,
					(_flags & LightingEffectFlags.Reversed) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward
				);
				return true;
			}
			effect = default;
			return false;
		}

		public bool HasChanged => _hasChanged;

		public async Task ApplyEffectAsync(KrakenHidTransport transport, CancellationToken cancellationToken)
		{
			if (_hasChanged)
			{
				await transport.SetMulticolorEffectAsync(_channelId, (byte)_effectId, _speed, _flags, _parameter2, _ledCount, _size, _colors.AsMemory(0, _colorCount), cancellationToken).ConfigureAwait(false);
				_hasChanged = false;
			}
		}
	}
}
