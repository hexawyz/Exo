using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Sensors")]
public interface ISensorService
{
	/// <summary>Watches information on all sensor devices, including the available sensors.</summary>
	/// <remarks>The availability status of devices is returned by <see cref="IDeviceService.WatchDevicesAsync(CancellationToken)"/>.</remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "WatchSensorDevices")]
	IAsyncEnumerable<SensorDeviceInformation> WatchSensorDevicesAsync(CancellationToken cancellationToken);

	// NB: In fact, maybe we DON'T want to have any kind in monitoring in there, and it should instead be managed (indirectly) from the programmation layer.

	///// <summary>Sets the default monitoring window settings.</summary>
	///// <param name="cancellationToken"></param>
	///// <returns></returns>
	//ValueTask SetDefaultMonitoringWindowSettingsAsync(MonitoringWindowSettings settings, CancellationToken cancellationToken);

	///// <summary>Enables monitoring of the specified sensor.</summary>
	///// <remarks>
	///// <para>When enabled, values changes of the sensor will be watched in the background, and the recent history will be preserved according to the monitoring window size.</para>
	///// <para>
	///// It is important to keep in mind that monitoring sensors comes with a cost, especially for polled sensors, for which watching value updates is an active I/O operation.
	///// </para>
	///// </remarks>
	///// <param name="sensor"></param>
	///// <param name="cancellationToken"></param>
	///// <returns></returns>
	//[OperationContract(Name = "EnableMonitoring")]
	//ValueTask EnableMonitoringAsync(SensorReference sensor, CancellationToken cancellationToken);

	///// <summary>Disables monitoring of the specified sensor.</summary>
	///// <remarks>
	///// <para>When disabled, values changes of the sensor will not we watched in the background, and any recent value history will be cleared.</para>
	///// </remarks>
	///// <param name="sensor"></param>
	///// <param name="cancellationToken"></param>
	///// <returns></returns>
	//[OperationContract(Name = "DisableMonitoring")]
	//ValueTask DisableMonitoringAsync(SensorReference sensor, CancellationToken cancellationToken);

	/// <summary>Gets the last value of the specified sensor.</summary>
	/// <param name="sensor"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "GetLastSensorValue")]
	ValueTask<SensorDataPoint> GetLastSensorValueAsync(SensorReference sensor, CancellationToken cancellationToken);

	/// <summary>Watches the live value updates of a sensor.</summary>
	/// <remarks>
	/// <para>This calls adds a dependency on the sensor, which will temporarily enable monitoring of the sensor if necessary.</para>
	/// <para>Monitored sensors have an history window of a preconfigured size</para>
	/// </remarks>
	/// <param name="sensor"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "WatchSensorValue")]
	IAsyncEnumerable<SensorDataPoint> WatchSensorValueAsync(SensorReference sensor, CancellationToken cancellationToken);
}
