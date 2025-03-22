using System.IO.Pipes;
using Exo.Rpc;

namespace Exo.Service.Rpc;

internal sealed class UiPipeServer : PipeServer<UiPipeServerConnection>
{
	internal ILogger<UiPipeServerConnection> ConnectionLogger { get; }
	internal CustomMenuService CustomMenuService { get; }
	internal SensorService SensorService { get; }
	internal IMetadataSourceProvider MetadataSourceProvider { get; }

	public UiPipeServer
	(
		string pipeName,
		PipeSecurity? pipeSecurity,
		ILogger<UiPipeServerConnection> connectionLogger,
		IMetadataSourceProvider metadataSourceProvider,
		CustomMenuService customMenuService,
		SensorService sensorService
	) : base(pipeName, 2, PipeTransmissionMode.Message, pipeSecurity)
	{
		ConnectionLogger = connectionLogger;
		MetadataSourceProvider = metadataSourceProvider;
		CustomMenuService = customMenuService;
		SensorService = sensorService;
	}
}
