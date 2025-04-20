using Exo.Cooling.Configuration;

namespace Exo.Service.Ipc;

internal interface ICoolingService
{
	/// <summary>Sets automatic cooling for the specified device and cooler.</summary>
	/// <param name="parameters"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	Task SetAutomaticCoolingAsync(Guid deviceId, Guid coolerId, CancellationToken cancellationToken);

	/// <summary>Sets fixed cooling power for the specified device and cooler.</summary>
	/// <param name="parameters"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	Task SetFixedCoolingAsync(Guid deviceId, Guid coolerId, byte power, CancellationToken cancellationToken);

	/// <summary>Sets a software-managed control curve for the specified device and cooler.</summary>
	/// <param name="parameters"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	Task SetSoftwareControlCurveCoolingAsync(Guid coolingDeviceId, Guid coolerId, Guid sensorDeviceId, Guid sensorId, byte defaultPower, CoolingControlCurveConfiguration controlCurve, CancellationToken cancellationToken);

	/// <summary>Sets a hardware-managed control curve for the specified device and cooler.</summary>
	/// <param name="parameters"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	Task SetHardwareControlCurveCoolingAsync(Guid deviceId, Guid coolerId, Guid sensorId, CoolingControlCurveConfiguration controlCurve, CancellationToken cancellationToken);
}
