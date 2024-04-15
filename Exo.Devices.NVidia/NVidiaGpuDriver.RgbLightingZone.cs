using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private class RgbLightingZone : LightingZone, ILightingZoneEffect<DisabledEffect>, ILightingZoneEffect<StaticColorEffect>
	{
		private RgbColor _color;

		public RgbLightingZone(object @lock, int index, Guid zoneId, RgbColor initialColor) : base(@lock, index, zoneId)
		{
			if (initialColor != default)
			{
				_color = initialColor;
				Effect = LightingEffect.Static;
			}
		}

		protected override ILightingEffect GetCurrentEffect()
			=> Effect switch
			{
				LightingEffect.Disabled => DisabledEffect.SharedInstance,
				LightingEffect.Static => new StaticColorEffect(_color),
				_ => DisabledEffect.SharedInstance,
			};

		void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect)
		{
			lock (Lock)
			{
				_color = effect.Color;
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

				effect = new(_color);
				return true;
			}
		}

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

		protected override void UpdateControl(ref NvApi.Gpu.Client.IlluminationZoneControl control)
		{
			control.ControlMode = NvApi.Gpu.Client.IlluminationControlMode.Manual;
			control.Data.Rgb.Manual = new() { R = _color.R, G = _color.G, B = _color.B, BrightnessPercentage = _color == default ? (byte)0 : (byte)100 };
		}
	}
}
