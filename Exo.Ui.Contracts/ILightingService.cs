using System.ServiceModel;

namespace Exo.Ui.Contracts;

[ServiceContract(Name = "Lighting")]
public interface ILightingService
{
	/// <summary>Gets information on all lighting devices, including the lighting zones and their supported effects.</summary>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	IAsyncEnumerable<WatchNotification<LightingDeviceInformation>> WatchLightingDevicesAsync(CancellationToken cancellationToken);

	/// <summary>Apply lighting effects to a device.</summary>
	/// <remarks>
	/// Lighting controllers are likely to have a synchronized update mechanism, so updating multiple effects at once (when necessary) should be more efficient that updating each effect individually.
	/// </remarks>
	/// <param name="effects"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	ValueTask ApplyDeviceLightingEffectsAsync(DeviceLightingEffects effects, CancellationToken cancellationToken);

	/// <summary>Apply lighting effects to multiple devices.</summary>
	/// <remarks>
	/// This is essentially a grouped version of <see cref="ApplyDeviceLightingEffectsAsync(DeviceLightingEffects, CancellationToken)"/>.
	/// While different devices will not share a common update mechanism, this method should still provide better latency by removing message round-trips between updates of different devices.
	/// </remarks>
	/// <param name="effects"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	ValueTask ApplyMultipleDeviceLightingEffectsAsync(MultipleDeviceLightingEffects effects, CancellationToken cancellationToken);

	/// <summary>Gets informations about a specific effect.</summary>
	/// <remarks>Effect information could be returned from <see cref="WatchLightingDevicesAsync"/> for convenience, but effect types are likely to be reused many times.</remarks>
	/// <param name="typeReference"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	ValueTask<LightingEffectInformation> GetEffectInformationAsync(EffectTypeReference typeReference, CancellationToken cancellationToken);

	/// <summary>Watches for effect values and changes.</summary>
	/// <remarks>
	/// This will reflect changes done using <see cref="ApplyDeviceLightingEffectsAsync(DeviceLightingEffects, CancellationToken)"/>
	/// and <see cref="ApplyMultipleDeviceLightingEffectsAsync(MultipleDeviceLightingEffects, CancellationToken)"/>.
	/// </remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	IAsyncEnumerable<DeviceLightingEffects> WatchEffectsAsync(CancellationToken cancellationToken);
}
