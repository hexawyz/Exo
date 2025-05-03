using System.Collections.Immutable;
using Exo.ColorFormats;
using Exo.Devices.Nzxt.LightingEffects;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.Kraken;

public partial class KrakenDriver
{
	private sealed class LightingZone :
		ILightingZone,
		ILightingZoneEffect<DisabledEffect>,
		ILightingZoneEffect<StaticColorEffect>,
		ILightingZoneEffect<ColorPulseEffect>,
		ILightingZoneEffect<VariableColorPulseEffect>,
		ILightingZoneEffect<CandleEffect>,
		ILightingZoneEffect<StarryNightEffect>,
		ILightingZoneEffect<TaiChiEffect>,
		ILightingZoneEffect<LiquidCoolerEffect>,
		ILightingZoneEffect<ReversibleVariableSpectrumWaveEffect>,
		ILightingZoneEffect<CoveringMarqueeEffect>
	{
		const byte DefaultStaticSpeed = 0x32;

		// NB: Takes the speeds from CAM but insert an extra one between "normal" and "fast" in order to match the predetermined model used in Exo.
		// CAM <=> Exo:
		// Slower <=> Slower
		// Slow <=> Slow
		// Normal <=> Medium Slow
		// <nothing> <=> Medium fast
		// Fast <=> Fast
		// Faster <=> Faster
		private static ReadOnlySpan<ushort> PulseSpeeds => [0x19, 0x14, 0x0f, 0xa, 0x07, 0x04];
		private static ReadOnlySpan<ushort> StarryNightSpeeds => PulseSpeeds;
		private static ReadOnlySpan<ushort> BreathingSpeeds => [0x28, 0x1e, 0x14, 0x0f, 0x0a, 0x04];
		private static ReadOnlySpan<ushort> FadeSpeeds => [0x50, 0x3c, 0x28, 0x1e, 0x14, 0x0a];
		private static ReadOnlySpan<ushort> SpectrumWaveSpeeds => [0x015e, 0x012c, 0x00fa, 0x00dc, 0x0096, 0x0050];
		private static ReadOnlySpan<ushort> CoveringMarqueeSpeeds => SpectrumWaveSpeeds;
		private static ReadOnlySpan<ushort> TaiChiSpeeds => [0x32, 0x28, 0x1e, 0x19, 0x14, 0x0a];
		private static ReadOnlySpan<ushort> LiquidCoolerSpeeds => TaiChiSpeeds;

		private readonly RgbColor[] _colors;
		private readonly Guid _zoneId;
		// NB: As in most types in Exo, fields are ordered to reduce the amount of padding as much as possible.
		private readonly byte _channelId;
		private readonly byte _accessoryId;
		private readonly byte _ledCount;
		private KrakenEffect _effectId;
		private ushort _speed;
		private byte _colorCount;
		private byte _flags;
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
			_size = 0x03;
		}

		Guid ILightingZone.ZoneId => _zoneId;

		ILightingEffect ILightingZone.GetCurrentEffect()
		{
			int speedIndex;
			switch (_effectId)
			{
			case KrakenEffect.Static:
				return _colors[0] == default ? DisabledEffect.SharedInstance : new StaticColorEffect(_colors[0]);
			case KrakenEffect.SpectrumWave:
				return new ReversibleVariableSpectrumWaveEffect((speedIndex = SpectrumWaveSpeeds.IndexOf(_speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & 0x02) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
			case KrakenEffect.Pulse:
				return (speedIndex = PulseSpeeds.IndexOf(_speed)) >= 0 ? new VariableColorPulseEffect(_colors[0], (PredeterminedEffectSpeed)speedIndex) : new ColorPulseEffect(_colors[0]);
			case KrakenEffect.Candle:
				return new CandleEffect(_colors[0]);
			case KrakenEffect.StarryNight:
				return new StarryNightEffect(_colors[0], (speedIndex = StarryNightSpeeds.IndexOf(_speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow);
			case KrakenEffect.TaiChi:
				return new TaiChiEffect(_colors[0], _colors[1], (speedIndex = TaiChiSpeeds.IndexOf(_speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & 0x02) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
			case KrakenEffect.LiquidCooler:
				return new LiquidCoolerEffect(_colors[0], _colors[1], (speedIndex = LiquidCoolerSpeeds.IndexOf(_speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & 0x02) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
			case KrakenEffect.CoveringMarquee:
				return new CoveringMarqueeEffect(_colors.AsSpan(0, _colorCount).ToImmutableArray(), (speedIndex = LiquidCoolerSpeeds.IndexOf(_speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & 0x02) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward, _size);
			default:
				throw new NotImplementedException();
			}
		}

		void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect)
		{
			if (_effectId != KrakenEffect.Static || _colorCount != 1 || _colors[0] != default || _speed != DefaultStaticSpeed || _flags != 0x00 && _parameter2 != 0x00 || _size != 0x03)
			{
				_effectId = KrakenEffect.Static;
				_colors.AsSpan(0, _colorCount).Clear();
				_colorCount = 1;
				_speed = DefaultStaticSpeed;
				_flags = 0x00;
				_parameter2 = 0x00;
				_size = 0x03;
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
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<ColorPulseEffect>.ApplyEffect(in ColorPulseEffect effect)
		{
			if (_effectId != KrakenEffect.Pulse || _colorCount != 1 || _colors[0] != effect.Color || _speed != PulseSpeeds[2] || _flags != 0x00 && _parameter2 != 0x08 || _size != 0x03)
			{
				_effectId = KrakenEffect.Pulse;
				_colors[0] = effect.Color;
				if (_colorCount > 1)
				{
					_colors.AsSpan(1, _colorCount - 1).Clear();
				}
				_colorCount = 1;
				_speed = PulseSpeeds[2];
				_flags = 0x00;
				_parameter2 = 0x08;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<VariableColorPulseEffect>.ApplyEffect(in VariableColorPulseEffect effect)
		{
			if (_effectId != KrakenEffect.Pulse || _colorCount != 1 || _colors[0] != effect.Color || _speed != PulseSpeeds[2] || _flags != 0x00 && _parameter2 != 0x08 || _size != 0x03)
			{
				_effectId = KrakenEffect.Pulse;
				_colors[0] = effect.Color;
				if (_colorCount > 1)
				{
					_colors.AsSpan(1, _colorCount - 1).Clear();
				}
				_colorCount = 1;
				_speed = PulseSpeeds[(int)effect.Speed];
				_flags = 0x00;
				_parameter2 = 0x08;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<CandleEffect>.ApplyEffect(in CandleEffect effect)
		{
			if (_effectId != KrakenEffect.Candle || _colorCount != 1 || _colors[0] != effect.Color || _speed != DefaultStaticSpeed || _flags != 0x00 && _parameter2 != 0x00 || _size != 0x03)
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
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<StarryNightEffect>.ApplyEffect(in StarryNightEffect effect)
		{
			if (_effectId != KrakenEffect.StarryNight || _colorCount != 1 || _colors[0] != effect.Color || StarryNightSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) || _flags != 0x01 && _parameter2 != 0x00 || _size != 0x03)
			{
				_effectId = KrakenEffect.StarryNight;
				_colors[0] = effect.Color;
				if (_colorCount > 1)
				{
					_colors.AsSpan(1, _colorCount - 1).Clear();
				}
				_colorCount = 1;
				_speed = StarryNightSpeeds[(int)effect.Speed];
				_flags = 0x01;
				_parameter2 = 0x00;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<TaiChiEffect>.ApplyEffect(in TaiChiEffect effect)
		{
			if (_effectId != KrakenEffect.TaiChi ||
				_colorCount != 2 ||
				_colors[0] != effect.Color1 ||
				_colors[1] != effect.Color2 ||
				TaiChiSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? (byte)0x02 : (byte)0x00) ||
				_parameter2 != 0x05 ||
				_size != 0x03)
			{
				_effectId = KrakenEffect.TaiChi;
				_colors[0] = effect.Color1;
				_colors[1] = effect.Color2;
				if (_colorCount > 2)
				{
					_colors.AsSpan(2, _colorCount - 2).Clear();
				}
				_colorCount = 2;
				_speed = TaiChiSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? (byte)0x02 : (byte)0x00;
				_parameter2 = 0x05;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<LiquidCoolerEffect>.ApplyEffect(in LiquidCoolerEffect effect)
		{
			if (_effectId != KrakenEffect.LiquidCooler ||
				_colorCount != 2 ||
				_colors[0] != effect.Color1 ||
				_colors[1] != effect.Color2 ||
				LiquidCoolerSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? (byte)0x02 : (byte)0x00) ||
				_parameter2 != 0x05 ||
				_size != 0x03)
			{
				_effectId = KrakenEffect.LiquidCooler;
				_colors[0] = effect.Color1;
				_colors[1] = effect.Color2;
				if (_colorCount > 2)
				{
					_colors.AsSpan(2, _colorCount - 2).Clear();
				}
				_colorCount = 2;
				_speed = LiquidCoolerSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? (byte)0x02 : (byte)0x00;
				_parameter2 = 0x05;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<ReversibleVariableSpectrumWaveEffect>.ApplyEffect(in ReversibleVariableSpectrumWaveEffect effect)
		{
			if (_effectId != KrakenEffect.SpectrumWave ||
				_colorCount != 0 ||
				SpectrumWaveSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? (byte)0x02 : (byte)0x00) ||
				_parameter2 != 0x00 ||
				_size != 0x03)
			{
				_effectId = KrakenEffect.SpectrumWave;
				_colors.AsSpan(0, _colorCount).Clear();
				_colorCount = 2;
				_speed = SpectrumWaveSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? (byte)0x02 : (byte)0x00;
				_parameter2 = 0x00;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<CoveringMarqueeEffect>.ApplyEffect(in CoveringMarqueeEffect effect)
		{
			if (effect.Colors.IsDefault || effect.Colors.Length is < 1 or > 8) throw new ArgumentException("The covering marquee effect requires between one to eight colors.");

			if (_effectId != KrakenEffect.CoveringMarquee ||
				_colorCount != effect.Colors.Length ||
				!_colors.AsSpan(0, _colorCount).SequenceEqual(effect.Colors.AsSpan()) ||
				CoveringMarqueeSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.Direction != EffectDirection1D.Forward ? (byte)0x06 : (byte)0x04) ||
				_parameter2 != 0x00 ||
				_size != effect.Size)
			{
				_effectId = KrakenEffect.CoveringMarquee;
				effect.Colors.AsSpan().CopyTo(_colors);
				_colorCount = (byte)effect.Colors.Length;
				_speed = CoveringMarqueeSpeeds[(int)effect.Speed];
				_flags = effect.Direction != EffectDirection1D.Forward ? (byte)0x06 : (byte)0x04;
				_parameter2 = 0x00;
				_size = effect.Size;
				_hasChanged = true;
			}
		}

		bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect)
		{
			effect = default;
			return _effectId == KrakenEffect.Static && _colorCount == 1 && _colors[0] == default && _speed == DefaultStaticSpeed && _flags == 0x00 && _parameter2 == 0x00 && _size == 0x03;
		}

		bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect)
		{
			if (_effectId == KrakenEffect.Static && _colorCount == 1 && _speed == DefaultStaticSpeed && _flags == 0x00 && _parameter2 == 0x00 && _size == 0x03)
			{
				effect = new(_colors[0]);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<ColorPulseEffect>.TryGetCurrentEffect(out ColorPulseEffect effect)
		{
			if (_effectId == KrakenEffect.Pulse && _colorCount == 1 && _speed == PulseSpeeds[2] && _flags == 0x00 && _parameter2 == 0x08 && _size == 0x03)
			{
				effect = new(_colors[0]);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<VariableColorPulseEffect>.TryGetCurrentEffect(out VariableColorPulseEffect effect)
		{
			if (_effectId == KrakenEffect.Pulse && _colorCount == 1 && PulseSpeeds.IndexOf(_speed) is int speedIndex && speedIndex >= 0 && _flags == 0x00 && _parameter2 == 0x08 && _size == 0x03)
			{
				effect = new(_colors[0], (PredeterminedEffectSpeed)speedIndex);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<CandleEffect>.TryGetCurrentEffect(out CandleEffect effect)
		{
			if (_effectId == KrakenEffect.Candle && _colorCount == 1 && _speed == DefaultStaticSpeed && _flags == 0x00 && _parameter2 == 0x00 && _size == 0x03)
			{
				effect = new(_colors[0]);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<StarryNightEffect>.TryGetCurrentEffect(out StarryNightEffect effect)
		{
			if (_effectId == KrakenEffect.Pulse && _colorCount == 1 && StarryNightSpeeds.IndexOf(_speed) is int speedIndex && speedIndex >= 0 && _flags == 0x00 && _parameter2 == 0x08 && _size == 0x03)
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
				_flags is 0x00 or 0x02 &&
				_parameter2 == 0x05 &&
				_size == 0x03)
			{
				effect = new(_colors[0], _colors[1], (PredeterminedEffectSpeed)speedIndex, (_flags & 0x02) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
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
				_flags is 0x00 or 0x02 &&
				_parameter2 == 0x05 &&
				_size == 0x03)
			{
				effect = new(_colors[0], _colors[1], (PredeterminedEffectSpeed)speedIndex, (_flags & 0x02) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
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
				_flags is 0x00 or 0x02 &&
				_parameter2 == 0x05 &&
				_size == 0x03)
			{
				effect = new((PredeterminedEffectSpeed)speedIndex, (_flags & 0x02) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward);
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
				_flags is 0x04 or 0x06 &&
				_parameter2 == 0x00 &&
				_size is >= 0x03 and <= 0x06)
			{
				effect = new
				(
					_colors.AsSpan(0, _colorCount).ToImmutableArray(),
					(PredeterminedEffectSpeed)speedIndex,
					(_flags & 0x02) != 0 ? EffectDirection1D.Backward : EffectDirection1D.Forward,
					_size
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
