using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private abstract class LightingZone : ILightingZone
	{
		protected Lock Lock { get; }
		private readonly int _index;
		protected LightingEffect Effect;

		public Guid ZoneId { get; }

		protected LightingZone(Lock @lock, int index, Guid zoneId)
		{
			Lock = @lock;
			_index = index;
			ZoneId = zoneId;
		}

		internal void UpdateControl(NvApi.Gpu.Client.IlluminationZoneControl[] controls)
			=> UpdateControl(ref controls[_index]);

		protected abstract void UpdateControl(ref NvApi.Gpu.Client.IlluminationZoneControl control);

		ILightingEffect ILightingZone.GetCurrentEffect() => GetCurrentEffect();

		protected abstract ILightingEffect GetCurrentEffect();
	}
}
