using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Exo.Discovery;

/// <summary>A base class to inherit for providing a discovery service.</summary>
/// <remarks>
/// <para>
/// This provides convenience methods to ensure that the sink is initialized and disposed properly.
/// Discovery services do not strictly need to inherit from this class, but it should generally be the case.
/// </para>
/// </remarks>
/// <typeparam name="TFactory"></typeparam>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TParsedFactoryDetails"></typeparam>
/// <typeparam name="TDiscoveryContext"></typeparam>
/// <typeparam name="TCreationContext"></typeparam>
/// <typeparam name="TComponent"></typeparam>
/// <typeparam name="TResult"></typeparam>
public abstract class DiscoveryService<TSelf, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult> :
	DiscoveryService<TSelf, SimpleComponentFactory<TCreationContext, TResult>, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>,
	IDiscoveryService<TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>,
	IAsyncDisposable
	where TSelf : class, IDiscoveryService<TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>
	where TKey : notnull, IEquatable<TKey>
	where TParsedFactoryDetails : notnull
	where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
	where TCreationContext : class, IComponentCreationContext
	where TComponent : class, IAsyncDisposable
	where TResult : ComponentCreationResult<TKey, TComponent>
{
	protected DiscoveryService() : base() { }

	public override ValueTask<TResult?> InvokeFactoryAsync
	(
		SimpleComponentFactory<TCreationContext, TResult> factory,
		ComponentCreationParameters<TKey, TCreationContext> creationParameters,
		CancellationToken cancellationToken
	)
		=> factory(creationParameters.CreationContext!, cancellationToken);
}

/// <summary>A base class to inherit for providing a discovery service.</summary>
/// <remarks>
/// <para>
/// This provides convenience methods to ensure that the sink is initialized and disposed properly.
/// Discovery services do not strictly need to inherit from this class, but it should generally be the case.
/// </para>
/// </remarks>
/// <typeparam name="TFactory"></typeparam>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TParsedFactoryDetails"></typeparam>
/// <typeparam name="TDiscoveryContext"></typeparam>
/// <typeparam name="TCreationContext"></typeparam>
/// <typeparam name="TComponent"></typeparam>
/// <typeparam name="TResult"></typeparam>
public abstract class DiscoveryService<TSelf, TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult> :
	Component,
	IDiscoveryService<TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>,
	IAsyncDisposable
	where TSelf : class, IDiscoveryService<TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>
	where TFactory : Delegate
	where TKey : notnull, IEquatable<TKey>
	where TParsedFactoryDetails : notnull
	where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
	where TCreationContext : class, IComponentCreationContext
	where TComponent : class, IAsyncDisposable
	where TResult : ComponentCreationResult<TKey, TComponent>
{
	// This contains different values depending on the initialization state of the object.
	// If uninitialized, it shall contain a TaskCompletionSource that will be completed with the sink.
	// If initialized, it contains the sink itself.
	// If disposed, it contains the null value.
	private object? _sinkOrTaskCompletionSource;

	protected DiscoveryService()
	{
		if (GetType() != typeof(TSelf)) throw new InvalidOperationException("The discovery service type does not exactly match what is declared.");
		_sinkOrTaskCompletionSource = new TaskCompletionSource<IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>>();
	}

	/// <summary></summary>
	/// <remarks>This method can be entirely substituted, as long as implementors call <see cref="DisposeSink"/>, which is the only thing this implementation does.</remarks>
	/// <returns></returns>
	public override ValueTask DisposeAsync()
	{
		DisposeSink();
		return ValueTask.CompletedTask;
	}

	protected void DisposeSink()
	{
		if (Interlocked.Exchange(ref _sinkOrTaskCompletionSource, null) is { } obj)
		{
			IDiscoverySink<TKey, TDiscoveryContext, TCreationContext> sink;
			if (obj is TaskCompletionSource<IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>> tcs)
			{
				if (!tcs.TrySetCanceled() || !tcs.Task.IsCompletedSuccessfully)
				{
					return;
				}
				sink = tcs.Task.Result;
			}
			else
			{
				sink = (IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>)obj;
			}
			sink.Dispose();
		}
	}

	protected async ValueTask RegisterAsync(IDiscoveryOrchestrator discoveryOrchestrator)
	{
		var obj = Volatile.Read(ref _sinkOrTaskCompletionSource);
		ObjectDisposedException.ThrowIf(obj is null, typeof(TSelf));

		if (obj is not TaskCompletionSource<IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>> tcs)
		{
			throw new InvalidOperationException("Was already initialized.");
		}

		var sink = await discoveryOrchestrator.RegisterDiscoveryServiceAsync
		<
			TSelf,
			TFactory,
			TKey,
			TParsedFactoryDetails,
			TDiscoveryContext,
			TCreationContext,
			TComponent,
			TResult
		>(Unsafe.As<TSelf>(this)).ConfigureAwait(false);

		if (!tcs.TrySetResult(sink) || Interlocked.CompareExchange(ref _sinkOrTaskCompletionSource, sink, tcs) != tcs)
		{
			sink.Dispose();
		}
	}

	private ValueTask<IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>> WaitForSinkAsync()
	{
		if (Volatile.Read(ref _sinkOrTaskCompletionSource) is { } obj)
		{
			if (obj is TaskCompletionSource<IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>> tcs)
			{
				return new(tcs.Task);
			}
			else
			{
				return new((IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>)obj);
			}
		}
		return ValueTask.FromException<IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>>(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException()));
	}

	/// <summary>Gets the sink for this instance.</summary>
	/// <remarks>
	/// This property should only be used after the service has started.
	/// The sink is not guaranteed to be available if the <see cref="StartAsync(IDiscoverySink{TKey, TDiscoveryContext, TCreationContext}, CancellationToken)"/> method has not been called.
	/// </remarks>
	/// <exception cref="InvalidOperationException">The sink is not yet initialized.</exception>
	public IDiscoverySink<TKey, TDiscoveryContext, TCreationContext> Sink
	{
		get
		{
			var obj = Volatile.Read(ref _sinkOrTaskCompletionSource);
			ObjectDisposedException.ThrowIf(obj is null, typeof(TSelf));
			if (obj is IDiscoverySink<TKey, TDiscoveryContext, TCreationContext> sink) return sink;
			var task = ((TaskCompletionSource<IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>>)obj).Task;
			if (!task.IsCompleted) throw new InvalidOperationException("The sink is not initialized.");
			return task.Result;
		}
	}

	protected IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>? TryGetSink()
	{
		var obj = Volatile.Read(ref _sinkOrTaskCompletionSource);
		if (obj is null) goto Failed;
		if (obj is IDiscoverySink<TKey, TDiscoveryContext, TCreationContext> sink) return sink;
		var task = ((TaskCompletionSource<IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>>)obj).Task;
		if (task.IsCompletedSuccessfully) return task.Result;
	Failed:;
		return null;
	}

	async Task IDiscoveryService<TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>.StartAsync(CancellationToken cancellationToken)
		=> await StartAsync(await WaitForSinkAsync().ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

	Task IDiscoveryService<TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>.StopAsync(CancellationToken cancellationToken)
		=> StopAsync(cancellationToken);

	protected abstract Task StartAsync(IDiscoverySink<TKey, TDiscoveryContext, TCreationContext> sink, CancellationToken cancellationToken);
	protected virtual async Task StopAsync(CancellationToken cancellationToken) => await DisposeAsync().ConfigureAwait(false);

	public abstract bool TryParseFactory(ImmutableArray<CustomAttributeData> attributes, [NotNullWhen(true)] out TParsedFactoryDetails? parsedFactoryDetails);
	public abstract bool TryRegisterFactory(Guid factoryId, TParsedFactoryDetails parsedFactoryDetails);
	public abstract ValueTask<TResult?> InvokeFactoryAsync(TFactory factory, ComponentCreationParameters<TKey, TCreationContext> creationParameters, CancellationToken cancellationToken);
}
