using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	// TODO: Implement a proper conversion from RGB to RGBW.
	private class RgbwLightingZone : LightingZone, ILightingZoneEffect<DisabledEffect>, ILightingZoneEffect<StaticColorEffect>
	{
		private RgbwColor _color;

		public RgbwLightingZone(object @lock, int index, Guid zoneId, RgbwColor color) : base(@lock, index, zoneId)
		{
			if (color != default)
			{
				_color = color;
				Effect = LightingEffect.Static;
			}
		}

		protected override ILightingEffect GetCurrentEffect()
			=> Effect switch
			{
				LightingEffect.Disabled => DisabledEffect.SharedInstance,
				LightingEffect.Static => new StaticColorEffect(_color.ToRgb()),
				_ => DisabledEffect.SharedInstance,
			};

		void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect)
		{
			lock (Lock)
			{
				_color = default;
				Effect = LightingEffect.Disabled;
			}
		}

		bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect)
		{
			lock (Lock)
			{
				effect = default;

				if (Effect == LightingEffect.Disabled)
				{
					return true;
				}

				return false;
			}
		}

		void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect)
		{
			lock (Lock)
			{
				_color = RgbwColor.FromRgb(effect.Color);
				Effect = LightingEffect.Static;
			}
		}

		bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect)
		{
			lock (Lock)
			{
				if (Effect == LightingEffect.Disabled)
				{
					effect = default;
					return false;
				}

				effect = new(_color.ToRgb());
				return true;
			}
		}

		protected override void UpdateControl(ref NvApi.Gpu.Client.IlluminationZoneControl control)
		{
			switch (Effect)
			{
			case LightingEffect.Disabled:
				control.ControlMode = NvApi.Gpu.Client.IlluminationControlMode.Manual;
				control.Data.Rgbw.Manual = default;
				break;
			case LightingEffect.Static:
				byte brightness = _color == default ? (byte)0 : (byte)100;
				control.ControlMode = NvApi.Gpu.Client.IlluminationControlMode.Manual;
				control.Data.Rgbw.Manual = new() { R = _color.R, G = _color.G, B = _color.B, W = _color.W, BrightnessPercentage = brightness };
				break;
			}
		}
	}
}
