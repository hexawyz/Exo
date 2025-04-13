using System.IO.Pipes;
using Exo.Ipc;

namespace Exo.Service.Ipc;

internal sealed class UiPipeServer : PipeServer<UiPipeServerConnection>
{
	internal ILogger<UiPipeServerConnection> ConnectionLogger { get; }
	internal CustomMenuService CustomMenuService { get; }
	internal DeviceRegistry DeviceRegistry { get; }
	internal MonitorService MonitorService { get; }
	internal SensorService SensorService { get; }
	internal LightingEffectMetadataService LightingEffectMetadataService { get; }
	internal IAssemblyLoader AssemblyLoader { get; }

	public UiPipeServer
	(
		string pipeName,
		PipeSecurity? pipeSecurity,
		ILogger<UiPipeServerConnection> connectionLogger,
		IAssemblyLoader assemblyLoader,
		CustomMenuService customMenuService,
		DeviceRegistry deviceRegistry,
		MonitorService monitorService,
		SensorService sensorService,
		LightingEffectMetadataService lightingEffectMetadataService
	) : base(pipeName, 2, PipeTransmissionMode.Message, pipeSecurity)
	{
		ConnectionLogger = connectionLogger;
		AssemblyLoader = assemblyLoader;
		CustomMenuService = customMenuService;
		DeviceRegistry = deviceRegistry;
		MonitorService = monitorService;
		SensorService = sensorService;
		LightingEffectMetadataService = lightingEffectMetadataService;
	}
}
