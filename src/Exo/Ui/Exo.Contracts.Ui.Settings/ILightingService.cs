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
