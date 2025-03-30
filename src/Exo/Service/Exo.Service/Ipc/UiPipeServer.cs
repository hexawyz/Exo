using System.IO.Pipes;
using Exo.Ipc;

namespace Exo.Service.Ipc;

internal sealed class UiPipeServer : PipeServer<UiPipeServerConnection>
{
	internal ILogger<UiPipeServerConnection> ConnectionLogger { get; }
	internal CustomMenuService CustomMenuService { get; }
	public DeviceRegistry DeviceRegistry { get; }
	internal SensorService SensorService { get; }
	internal IMetadataSourceProvider MetadataSourceProvider { get; }

	public UiPipeServer
	(
		string pipeName,
		PipeSecurity? pipeSecurity,
		ILogger<UiPipeServerConnection> connectionLogger,
		IMetadataSourceProvider metadataSourceProvider,
		CustomMenuService customMenuService,
		DeviceRegistry deviceRegistry,
		SensorService sensorService
	) : base(pipeName, 2, PipeTransmissionMode.Message, pipeSecurity)
	{
		ConnectionLogger = connectionLogger;
		MetadataSourceProvider = metadataSourceProvider;
		CustomMenuService = customMenuService;
		DeviceRegistry = deviceRegistry;
		SensorService = sensorService;
	}
}
