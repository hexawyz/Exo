using System.Runtime.InteropServices;
using Exo.Contracts.Ui;
using Exo.Utils;

namespace Exo.Service.Grpc;

internal sealed class GrpcServiceLifetimeService : IServiceLifetimeService
{
	private readonly TaskCompletionSource _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

	public GrpcServiceLifetimeService(IHostApplicationLifetime hostApplicationLifetime)
	{
		_taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		hostApplicationLifetime.ApplicationStopping.Register(state => _taskCompletionSource.TrySetResult(), _taskCompletionSource, false);
	}

	ValueTask<string?> IServiceLifetimeService.TryGetVersionAsync(CancellationToken cancellationToken)
		=> ValueTask.FromResult
		(
			_taskCompletionSource.Task.IsCompleted ?
				null :
				Program.GitCommitId.IsDefaultOrEmpty ? "UNKNOWN" : Convert.ToHexString(ImmutableCollectionsMarshal.AsArray(Program.GitCommitId)!)
		);

	Task IServiceLifetimeService.WaitForStopAsync(CancellationToken cancellationToken) => _taskCompletionSource.Task.WaitAsync(cancellationToken);
}
