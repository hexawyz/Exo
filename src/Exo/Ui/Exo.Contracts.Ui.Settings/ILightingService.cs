using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

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

	/// <summary>Watches for effect values and changes.</summary>
	/// <remarks>
	/// This will reflect changes done using <see cref="ApplyDeviceLightingChangesAsync(DeviceLightingUpdate, CancellationToken)"/>
	/// and <see cref="ApplyMultiDeviceLightingChangesAsync(MultiDeviceLightingUpdates, CancellationToken)"/>.
	/// </remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	IAsyncEnumerable<DeviceZoneLightingEffect> WatchEffectsAsync(CancellationToken cancellationToken);

	/// <summary>Watches for lighting configuration changes.</summary>
	/// <remarks>
	/// This will reflect changes done using <see cref="ApplyDeviceLightingChangesAsync(DeviceLightingUpdate, CancellationToken)"/>
	/// and <see cref="ApplyMultiDeviceLightingChangesAsync(MultiDeviceLightingUpdates, CancellationToken)"/>.
	/// </remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract]
	IAsyncEnumerable<LightingDeviceConfigurationUpdate> WatchConfigurationUpdatesAsync(CancellationToken cancellationToken);
}
