using System.IO.Pipes;
using Exo.Ipc;

namespace Exo.Service.Ipc;

internal sealed class UiPipeServer : PipeServer<UiPipeServerConnection>
{
	internal ILogger<UiPipeServerConnection> ConnectionLogger { get; }
	internal CustomMenuService CustomMenuService { get; }
	internal ImageStorageService ImageStorageService { get; }
	internal DeviceRegistry DeviceRegistry { get; }
	internal PowerService PowerService { get; }
	internal MouseService MouseService { get; }
	internal MonitorService MonitorService { get; }
	internal SensorService SensorService { get; }
	internal LightingEffectMetadataService LightingEffectMetadataService { get; }
	internal LightingService LightingService { get; }
	internal IAssemblyLoader AssemblyLoader { get; }

	public UiPipeServer
	(
		string pipeName,
		PipeSecurity? pipeSecurity,
		ILogger<UiPipeServerConnection> connectionLogger,
		IAssemblyLoader assemblyLoader,
		CustomMenuService customMenuService,
		ImageStorageService imageStorageService,
		DeviceRegistry deviceRegistry,
		PowerService powerService,
		MouseService mouseService,
		MonitorService monitorService,
		SensorService sensorService,
		LightingEffectMetadataService lightingEffectMetadataService,
		LightingService lightingService
	) : base(pipeName, 2, PipeTransmissionMode.Message, pipeSecurity)
	{
		ConnectionLogger = connectionLogger;
		AssemblyLoader = assemblyLoader;
		CustomMenuService = customMenuService;
		ImageStorageService = imageStorageService;
		DeviceRegistry = deviceRegistry;
		PowerService = powerService;
		MouseService = mouseService;
		MonitorService = monitorService;
		SensorService = sensorService;
		LightingEffectMetadataService = lightingEffectMetadataService;
		LightingService = lightingService;
	}
}
