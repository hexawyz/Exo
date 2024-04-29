using System.ServiceModel;

namespace Exo.Contracts.Ui;

[ServiceContract(Name = "ServiceLifetime")]
public interface IServiceLifetimeService
{
	/// <summary>Tries to get the SHA1 service version.</summary>
	/// <remarks>This call is guaranteed to return a value is the service is started and not shutting down.</remarks>
	/// <param name="cancellationToken"></param>
	/// <returns>The service version as an SHA1 commit id, or <see langword="null"/> if the service is stopped.</returns>
	[OperationContract(Name = "TryGetVersion")]
	ValueTask<string?> TryGetVersionAsync(CancellationToken cancellationToken);

	/// <summary>Waits for the service to stop.</summary>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "WaitForStop")]
	Task WaitForStopAsync(CancellationToken cancellationToken);
}
