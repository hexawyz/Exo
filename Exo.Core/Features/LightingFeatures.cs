using System.Collections.Generic;
using Exo.Core.Lighting;

namespace Exo.Core.Features.LightingFeatures
{
	public interface ILightingDeviceFeature : IDeviceFeature
	{
	}

	public interface ILightingControllerFeature : ILightingDeviceFeature
	{
		IReadOnlyCollection<ILightZone> GetLightZones();
		void ApplyChanges();
	}

	public interface ISynchronizedLightFeature : ILightingDeviceFeature, ILightZone
	{
	}

	public interface IPersitableLightingFeature : ILightingDeviceFeature
	{
		void PersistCurrentConfiguration();
	}
}
