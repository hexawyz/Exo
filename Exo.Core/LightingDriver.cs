using Exo.Core;
using Exo.Core.Features.LightingFeatures;

namespace DeviceTools.SystemControl
{
	/// <summary>Base class for a lighting device driver.</summary>
	public abstract class LightingDriver : IDeviceDriver<ILightingDeviceFeature>, ILightingControllerFeature
	{
		public abstract IDeviceFeatureCollection<ILightingDeviceFeature> Features { get; }

		void ILightingControllerFeature.ApplyChanges() => ApplyChanges();

		protected abstract void ApplyChanges();
	}
}
