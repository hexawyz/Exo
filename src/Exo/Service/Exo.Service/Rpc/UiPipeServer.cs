using System.IO.Pipes;
using Exo.Rpc;

namespace Exo.Service.Rpc;

internal sealed class UiPipeServer : PipeServer<UiPipeServerConnection>
{
	internal ILogger<UiPipeServerConnection> ConnectionLogger { get; }
	internal CustomMenuService CustomMenuService { get; }
	internal SensorService SensorService { get; }

	public UiPipeServer
	(
		string pipeName,
		PipeSecurity? pipeSecurity,
		ILogger<UiPipeServerConnection> connectionLogger,
		CustomMenuService customMenuService,
		SensorService sensorService
	) : base(pipeName, 2, PipeTransmissionMode.Message, pipeSecurity)
	{
		ConnectionLogger = connectionLogger;
		CustomMenuService = customMenuService;
		SensorService = sensorService;
	}
}
