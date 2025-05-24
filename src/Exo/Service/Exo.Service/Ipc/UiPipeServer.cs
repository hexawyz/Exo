using System.IO.Pipes;
using Exo.Ipc;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Ipc;

internal sealed class UiPipeServer : PipeServer<UiPipeServerConnection>
{
	internal IAssemblyLoader AssemblyLoader { get; }
	internal CustomMenuService CustomMenuService { get; }
	internal ProgrammingService ProgrammingService { get; }
	internal ImageStorageService ImageStorageService { get; }
	internal DeviceRegistry DeviceRegistry { get; }
	internal PowerService PowerService { get; }
	internal MouseService MouseService { get; }
	internal MonitorService MonitorService { get; }
	internal SensorService SensorService { get; }
	internal CoolingService CoolingService { get; }
	internal LightingEffectMetadataService LightingEffectMetadataService { get; }
	internal LightingService LightingService { get; }
	internal EmbeddedMonitorService EmbeddedMonitorService { get; }
	internal LightService LightService { get; }
	private readonly ILogger<UiPipeServerConnection> _connectionLogger;

	public UiPipeServer
	(
		string pipeName,
		PipeSecurity? pipeSecurity,
		ILogger<UiPipeServerConnection> connectionLogger,
		IAssemblyLoader assemblyLoader,
		CustomMenuService customMenuService,
		ProgrammingService programmingService,
		ImageStorageService imageStorageService,
		DeviceRegistry deviceRegistry,
		PowerService powerService,
		MouseService mouseService,
		MonitorService monitorService,
		SensorService sensorService,
		CoolingService coolingService,
		LightingEffectMetadataService lightingEffectMetadataService,
		LightingService lightingService,
		EmbeddedMonitorService embeddedMonitorService,
		LightService lightService
	) : base(pipeName, 2, PipeTransmissionMode.Message, pipeSecurity)
	{
		_connectionLogger = connectionLogger;
		AssemblyLoader = assemblyLoader;
		CustomMenuService = customMenuService;
		ProgrammingService = programmingService;
		ImageStorageService = imageStorageService;
		DeviceRegistry = deviceRegistry;
		PowerService = powerService;
		MouseService = mouseService;
		MonitorService = monitorService;
		SensorService = sensorService;
		CoolingService = coolingService;
		LightingEffectMetadataService = lightingEffectMetadataService;
		LightingService = lightingService;
		EmbeddedMonitorService = embeddedMonitorService;
		LightService = lightService;
	}

	protected override UiPipeServerConnection CreateConnection(NamedPipeServerStream stream)
		=> new(_connectionLogger, this, stream);
}
