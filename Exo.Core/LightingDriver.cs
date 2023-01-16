using System.Collections.Generic;
using Exo;
using Exo.Features.LightingFeatures;
using Exo.Lighting;

namespace DeviceTools.SystemControl
{
	/// <summary>Base class for a lighting device driver.</summary>
	public abstract class LightingDriver : IDeviceDriver<ILightingDeviceFeature>, ILightingControllerFeature
	{
		public abstract IDeviceFeatureCollection<ILightingDeviceFeature> Features { get; }

		void ILightingControllerFeature.ApplyChanges() => ApplyChanges();

		protected abstract void ApplyChanges();
		public IReadOnlyCollection<ILightZone> GetLightZones() => throw new System.NotImplementedException();
	}
}
