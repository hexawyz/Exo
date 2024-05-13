using Exo.Configuration;

namespace Exo.Service;

internal sealed partial class SensorService
{
	private sealed class DeviceState
	{
		public AsyncLock Lock { get; }
		public IConfigurationContainer DeviceConfigurationContainer { get; }
		public IConfigurationContainer<Guid> SensorsConfigurationContainer { get; }
		public bool IsConnected { get; set; }
		public SensorDeviceInformation Information { get; set; }
		public GroupedQueryState? GroupedQueryState { get; set; }
		public Dictionary<Guid, SensorState>? SensorStates { get; set; }

		public DeviceState
		(
			IConfigurationContainer deviceConfigurationContainer,
			IConfigurationContainer<Guid> sensorsConfigurationContainer,
			bool isConnected,
			SensorDeviceInformation information,
			GroupedQueryState? groupedQueryState,
			Dictionary<Guid, SensorState>? sensorStates
		)
		{
			Lock = new();
			DeviceConfigurationContainer = deviceConfigurationContainer;
			SensorsConfigurationContainer = sensorsConfigurationContainer;
			IsConnected = isConnected;
			Information = information;
			GroupedQueryState = groupedQueryState;
			SensorStates = sensorStates;
		}
	}
}
