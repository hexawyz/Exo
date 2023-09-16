using System.ServiceModel;
using Exo.Contracts;

namespace Exo.Ui.Contracts;

[ServiceContract(Name = "Lighting")]
public interface ILightingService
{
	/// <summary>Watches information on all lighting devices, including the lighting zones and their supported effects.</summary>
	/// <remarks>The availability status of devices is returned by <see cref="IDeviceService.WatchDevicesAsync(CancellationToken)"/>.</remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	IAsyncEnumerable<LightingDeviceInformation> WatchLightingDevicesAsync(CancellationToken cancellationToken);

	/// <summary>Apply lighting effects to a device.</summary>
	/// <remarks>
	/// Lighting controllers are likely to have a synchronized update mechanism, so updating multiple effects at once (when necessary) should be more efficient that updating each effect individually.
	/// </remarks>
	/// <param name="effects"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	ValueTask ApplyDeviceLightingChangesAsync(DeviceLightingUpdate effects, CancellationToken cancellationToken);

	/// <summary>Apply lighting effects to multiple devices.</summary>
	/// <remarks>
	/// This is essentially a grouped version of <see cref="ApplyDeviceLightingChangesAsync(DeviceLightingUpdate, CancellationToken)"/>.
	/// While different devices will not share a common update mechanism, this method should still provide better latency by removing message round-trips between updates of different devices.
	/// </remarks>
	/// <param name="effects"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	ValueTask ApplyMultiDeviceLightingChangesAsync(MultiDeviceLightingUpdates effects, CancellationToken cancellationToken);

	/// <summary>Gets informations about a specific effect.</summary>
	/// <remarks>Effect information could be returned from <see cref="WatchLightingDevicesAsync"/> for convenience, but effect types are likely to be reused many times.</remarks>
	/// <param name="typeReference"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	ValueTask<LightingEffectInformation> GetEffectInformationAsync(EffectTypeReference typeReference, CancellationToken cancellationToken);

	/// <summary>Watches for effect values and changes.</summary>
	/// <remarks>
	/// This will reflect changes done using <see cref="ApplyDeviceLightingChangesAsync(DeviceLightingUpdate, CancellationToken)"/>
	/// and <see cref="ApplyMultiDeviceLightingChangesAsync(MultiDeviceLightingUpdates, CancellationToken)"/>.
	/// </remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	IAsyncEnumerable<DeviceZoneLightingEffect> WatchEffectsAsync(CancellationToken cancellationToken);

	/// <summary>Watches for brightness values and changes.</summary>
	/// <remarks>
	/// This will reflect changes done using <see cref="ApplyDeviceLightingChangesAsync(DeviceLightingUpdate, CancellationToken)"/>
	/// and <see cref="ApplyMultiDeviceLightingChangesAsync(MultiDeviceLightingUpdates, CancellationToken)"/>.
	/// </remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	IAsyncEnumerable<DeviceBrightnessLevel> WatchBrightnessAsync(CancellationToken cancellationToken);
}
