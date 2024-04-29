using System.Reflection;
using Exo.Contracts.Ui;

namespace Exo.Service.Grpc;

internal sealed class GrpcServiceLifetimeService : IServiceLifetimeService
{
	private static readonly string Version = GetSha1Version();

	private static string GetSha1Version()
	{
		if (typeof(GrpcServiceLifetimeService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() is { } informationalVersionAttribute &&
			informationalVersionAttribute.InformationalVersion is not null &&
			informationalVersionAttribute.InformationalVersion.IndexOf('+') is >= 0 and int separatorIndex)
		{
			return informationalVersionAttribute.InformationalVersion[(separatorIndex + 1)..];
		}

		return "UNKNOWN";
	}

	private readonly TaskCompletionSource _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

	public GrpcServiceLifetimeService(IHostApplicationLifetime hostApplicationLifetime)
	{
		_taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		hostApplicationLifetime.ApplicationStopping.Register(state => _taskCompletionSource.TrySetResult(), _taskCompletionSource, false);
	}

	ValueTask<string?> IServiceLifetimeService.TryGetVersionAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_taskCompletionSource.Task.IsCompleted ? null : Version);
	Task IServiceLifetimeService.WaitForStopAsync(CancellationToken cancellationToken) => _taskCompletionSource.Task.WaitAsync(cancellationToken);
}
