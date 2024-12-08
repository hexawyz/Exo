using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private class FixedColorLightingZone : LightingZone, ILightingZoneEffect<DisabledEffect>, ILightingZoneEffect<StaticBrightnessEffect>
	{
		private byte _brightness;

		public FixedColorLightingZone(Lock @lock, int index, Guid zoneId, byte initialBrightness) : base(@lock, index, zoneId)
		{
			if (initialBrightness != 0)
			{
				_brightness = Math.Max(initialBrightness, (byte)100);
				Effect = LightingEffect.Static;
			}
		}

		protected override ILightingEffect GetCurrentEffect()
			=> Effect switch
			{
				LightingEffect.Disabled => DisabledEffect.SharedInstance,
				LightingEffect.Static => new StaticBrightnessEffect(_brightness),
				_ => DisabledEffect.SharedInstance,
			};

		void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect)
		{
			lock (Lock)
			{
				_brightness = default;
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

		void ILightingZoneEffect<StaticBrightnessEffect>.ApplyEffect(in StaticBrightnessEffect effect)
		{
			lock (Lock)
			{
				_brightness = effect.BrightnessLevel;
				Effect = LightingEffect.Static;
			}
		}

		bool ILightingZoneEffect<StaticBrightnessEffect>.TryGetCurrentEffect(out StaticBrightnessEffect effect)
		{
			lock (Lock)
			{
				if (Effect == LightingEffect.Disabled)
				{
					effect = default;
					return false;
				}

				effect = new(_brightness);
				return true;
			}
		}

		protected override void UpdateControl(ref NvApi.Gpu.Client.IlluminationZoneControl control)
		{
			control.ControlMode = NvApi.Gpu.Client.IlluminationControlMode.Manual;
			control.Data.SingleColor.Manual = new() { BrightnessPercentage = _brightness };
		}
	}
}
