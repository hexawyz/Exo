using System.IO.Pipes;
using Exo.Rpc;

namespace Exo.Service.Rpc;

internal sealed class UiPipeServer : PipeServer<UiPipeServerConnection>
{
	internal CustomMenuService CustomMenuService { get; }
	internal SensorService SensorService { get; }

	public UiPipeServer
	(
		string pipeName,
		PipeSecurity? pipeSecurity,
		CustomMenuService customMenuService,
		SensorService sensorService
	) : base(pipeName, 2, PipeTransmissionMode.Message, pipeSecurity)
	{
		CustomMenuService = customMenuService;
		SensorService = sensorService;
	}
}
