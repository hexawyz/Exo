using System.ServiceModel;
using Exo.Contracts.Ui.Settings.Cooling;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Cooling")]
public interface ICoolingService
{
	/// <summary>Watches information on all cooling devices, including the available coolers.</summary>
	/// <remarks>The availability status of devices is returned by <see cref="IDeviceService.WatchDevicesAsync(CancellationToken)"/>.</remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "WatchCoolingDevices")]
	IAsyncEnumerable<CoolingDeviceInformation> WatchCoolingDevicesAsync(CancellationToken cancellationToken);

	/// <summary>Watches cooling parameter changes.</summary>
	/// <param name="parameters"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "WatchCoolingChanges")]
	IAsyncEnumerable<CoolingParameters> WatchCoolingChangesAsync(CancellationToken cancellationToken);

	/// <summary>Sets automatic cooling for the specified device and cooler.</summary>
	/// <param name="parameters"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "SetAutomaticCooling")]
	ValueTask SetAutomaticCoolingAsync(AutomaticCoolingParameters parameters, CancellationToken cancellationToken);

	/// <summary>Sets fixed cooling power for the specified device and cooler.</summary>
	/// <param name="parameters"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "SetFixedCooling")]
	ValueTask SetFixedCoolingAsync(FixedCoolingParameters parameters, CancellationToken cancellationToken);

	/// <summary>Sets a software-managed control curve for the specified device and cooler.</summary>
	/// <param name="parameters"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "SetSoftwareControlCurveCooling")]
	ValueTask SetSoftwareControlCurveCoolingAsync(SoftwareCurveCoolingParameters parameters, CancellationToken cancellationToken);

	/// <summary>Sets a hardware-managed control curve for the specified device and cooler.</summary>
	/// <param name="parameters"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "SetHardwareControlCurveCooling")]
	ValueTask SetHardwareControlCurveCoolingAsync(HardwareCurveCoolingParameters parameters, CancellationToken cancellationToken);
}
