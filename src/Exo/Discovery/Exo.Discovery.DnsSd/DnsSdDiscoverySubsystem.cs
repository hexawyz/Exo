using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DeviceTools;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public delegate ValueTask<DriverCreationResult<DnsSdInstanceId>?> DnsSdDriverFactory(DnsSdDeviceLifetime deviceLifetime, DnsSdDriverCreationContext context, CancellationToken cancellationToken);

// TODO: This does need some code to reprocess device arrivals when new factories are registered.
// For now, if the service is started before all factories are registered, some devices will be missed, which is less than ideal.
public sealed class DnsSdDiscoverySubsystem :
	DiscoveryService<DnsSdDiscoverySubsystem, DnsSdDriverFactory, DnsSdInstanceId, DnsSdFactoryDetails, DnsSdDiscoveryContext, DnsSdDriverCreationContext, Driver, DriverCreationResult<DnsSdInstanceId>>
{
	[DiscoverySubsystem<RootDiscoverySubsystem>]
	[RootComponent(typeof(DnsSdDiscoverySubsystem))]
	public static async ValueTask<RootComponentCreationResult> CreateAsync
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator
	)
	{
		var service = new DnsSdDiscoverySubsystem(loggerFactory, driverRegistry);
		try
		{
			await service.RegisterAsync(discoveryOrchestrator);
		}
		catch
		{
			await service.DisposeAsync();
			throw;
		}
		return new RootComponentCreationResult(typeof(DnsSdDiscoverySubsystem), service);
	}

	private readonly Dictionary<string, Guid> _serviceTypeFactories;
	private readonly Dictionary<string, DnsSdDeviceLifetime> _lifetimes;

	private readonly ILogger<DnsSdDiscoverySubsystem> _logger;
	internal ILoggerFactory LoggerFactory { get; }
	internal INestedDriverRegistryProvider DriverRegistry { get; }

	private CancellationTokenSource? _cancellationTokenSource;
	// Workaround for DNS-SD. Instead of spawning a single watcher for everything, we spawn one watcher for each known service type.
	private Dictionary<string, Task>? _serviceTypeWatchers;
	private readonly Lock _lock;

	public DnsSdDiscoverySubsystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry
	)
	{
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		_logger = loggerFactory.CreateLogger<DnsSdDiscoverySubsystem>();

		_serviceTypeFactories = new();
		_lifetimes = new();

		_cancellationTokenSource = new();
		_lock = new();
	}

	public override string FriendlyName => "DNS-SD Discovery";

	public override async ValueTask DisposeAsync()
	{
		Dictionary<string, Task>? watchers = null;
		lock (_lock)
		{
			if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
			{
				cts.Cancel();
				DisposeSink();
				watchers = _serviceTypeWatchers;
				Volatile.Write(ref _serviceTypeWatchers, null);
			}
		}
		if (watchers is not null)
		{
			foreach (var task in watchers.Values)
			{
				await task.ConfigureAwait(false);
			}
		}
	}

	protected override Task StartAsync(IDiscoverySink<DnsSdInstanceId, DnsSdDiscoveryContext, DnsSdDriverCreationContext> sink, CancellationToken cancellationToken)
	{
		try
		{
			lock (_lock)
			{
				ObjectDisposedException.ThrowIf(_cancellationTokenSource is null, typeof(DnsSdDiscoverySubsystem));

				if (_serviceTypeWatchers is not null)
				{
					throw new InvalidOperationException("The service has already been started.");
				}

				_logger.DnsSdDiscoveryStarting();

				_serviceTypeWatchers = new();

				foreach (var serviceType in _serviceTypeFactories.Keys)
				{
					_serviceTypeWatchers.Add(serviceType, WatchAsync(serviceType, _cancellationTokenSource.Token));
				}

				_logger.DnsSdDiscoveryStarted();
			}
		}
		catch (Exception ex)
		{
			return Task.FromException(ex);
		}
		return Task.CompletedTask;
	}

	// We cannot browse available services on the network using DNS-SD through DevQuery. (see https://github.com/stammen/dnssd-uwp/issues/8)
	// Judging from all the posts that can be found on internet, queries only work when a specific service type is specified in the query.
	// Obviously, the special "magic" query on "_services._dns-sd._udp" doesn't seem work here.
	// Seemingly, the magic query would actually work internally, but because the filters are used equally for building the DNS-SD query as they are for filtering out the results, we lose everything…
	// So, for now we'll just spawn a DNS-SD request for every supported service type.
	// Relying on DevQuery avoided having to add p/invoke for the native DNS-SD APIs, but I'll probably end up doing that in the long run.
	// Underlying reason being that if we can't query the active DNS services, we will have to let one active query running for every service registered, even if the service is not present on the network.
	// This won't be a huge problem while the number of drivers is low, but it might become a problem in the long run.

	private async Task WatchAsync(string serviceType, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in
				DeviceQuery.WatchAllAsync
				(
					DeviceObjectKind.AssociationEndpointService,
					Properties.System.Devices.AepService.ProtocolId == AepProtocolGuids.DnsServiceDiscovery &
					Properties.System.Devices.Dnssd.ServiceName == serviceType,
					cancellationToken
				).ConfigureAwait(false))
			{
				// Process notifications inside the lock, so that we can allow drivers to report when the device is down in a more consistent way down the road.
				lock (_lock)
				{
					switch (notification.Kind)
					{
					// From my observations, it seems that there are actually no removal notifications but update notifications are sent when a device is back up ?
					// Processing updates as add notifications can generate multiple add notifications in a row. It works because the orchestrator will deduplicate, but it is unclean.
					// TODO: Make this a bit better. Either without DevQuery if it is any better or by propagating device disconnects from the drivers back to the service discovery subsystem.
					case WatchNotificationKind.Enumeration:
					case WatchNotificationKind.Add:
						_logger.DnsSdInstanceArrival(notification.Object.Id);
						HandleArrival(notification.Object);
						break;
					case WatchNotificationKind.Update:
						_logger.DnsSdInstanceUpdate(notification.Object.Id);
						HandleUpdate(notification.Object);
						break;
					case WatchNotificationKind.Remove:
						_logger.DnsSdInstanceRemoval(notification.Object.Id);
						HandleRemoval(notification.Object.Id);
						break;
					}
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
		}
	}

	private void HandleArrival(DeviceObjectInformation device)
	{
		TryGetSink()?.HandleArrival(new(this, device));
	}

	private void HandleUpdate(DeviceObjectInformation device)
	{
		if (_lifetimes.TryGetValue(device.Id, out var lifetime))
		{
			lifetime.NotifyDeviceUpdated();
		}
		else
		{
			HandleArrival(device);
		}
	}

	private void HandleRemoval(string instanceId)
	{
		TryGetSink()?.HandleRemoval(instanceId);
		if (_lifetimes.Remove(instanceId, out var lifetime))
		{
			lifetime.MarkDisposed();
		}
	}

	internal void OnRemoval(DnsSdDeviceLifetime lifetime)
	{
		lock (_lock)
		{
			if (((IDictionary<string, DnsSdDeviceLifetime>)_lifetimes).Remove(new KeyValuePair<string, DnsSdDeviceLifetime>(lifetime.InstanceId, lifetime)))
			{
				_logger.DnsSdInstanceRemoval(lifetime.InstanceId);
				TryGetSink()?.HandleRemoval(lifetime.InstanceId);
			}
		}
	}

	internal DnsSdDeviceLifetime CreateLifetime(string instanceId)
	{
		lock (_lock)
		{
			if (_lifetimes.ContainsKey(instanceId)) throw new Exception();

			var lifetime = new DnsSdDeviceLifetime(this, instanceId);
			_lifetimes.Add(instanceId, lifetime);
			return lifetime;
		}
	}

	internal void ReleaseLifetime(DnsSdDeviceLifetime lifetime)
	{
		lock (_lock)
		{
			_lifetimes.Remove(lifetime.InstanceId, lifetime);
		}
	}

	public override bool TryParseFactory(ImmutableArray<CustomAttributeData> attributes, [NotNullWhen(true)] out DnsSdFactoryDetails parsedFactoryDetails)
	{
		var serviceTypeKeys = new HashSet<string>();

		foreach (var attribute in attributes)
		{
			if (attribute.Matches<DnsSdServiceTypeAttribute>())
			{
				string serviceName = (string)attribute.ConstructorArguments[0].Value!;
				if (!serviceTypeKeys.Add(serviceName))
				{
					_logger.DnsSdServiceTypeDuplicateKey(serviceName);
					goto Failed;
				}
			}
		}

		if (serviceTypeKeys.Count == 0)
		{
			_logger.DnsSdFactoryMissingKeys();
			goto Failed;
		}

		parsedFactoryDetails = new()
		{
			ServiceTypes = [.. serviceTypeKeys]
		};
		return true;
	Failed:;
		parsedFactoryDetails = default;
		return false;
	}

	public override bool TryRegisterFactory(Guid factoryId, DnsSdFactoryDetails parsedFactoryDetails)
	{
		// Prevent factories from being registered in parallel, so that key conflicts between factories can be avoided mostly deterministically.
		// Obviously, key conflicts will still depend on the order of discovery of the factories, but they should not exist at all anyway.
		lock (_lock)
		{
			// Sadly, the best way to ensure coherence is to do all of this in two steps, which will be a little bit costlier.
			// We mostly need coherence for the sake of providing complete factory registrations upon device arrivals.

			// First, check that all the keys will avoid a conflict
			foreach (var key in parsedFactoryDetails.ServiceTypes)
			{
				if (_serviceTypeFactories.ContainsKey(key))
				{
					_logger.DnsSdServiceTypeRegistrationConflict(key);
					return false;
				}
			}

			// Once we are guaranteed to be conflict-free, the keys are added.
			foreach (var key in parsedFactoryDetails.ServiceTypes)
			{
				_serviceTypeFactories.Add(key, factoryId);
				// Also start the corresponding watcher if necessary.
				if (_serviceTypeWatchers is { } watchers)
				{
					watchers.Add(key, WatchAsync(key, _cancellationTokenSource!.Token));
				}
			}
		}

		return true;
	}

	internal ImmutableArray<Guid> ResolveFactories(string serviceType)
	{
		lock (_lock)
		{
			if (_serviceTypeFactories.TryGetValue(serviceType, out var guid))
			{
				return [guid];
			}
		}
		return [];
	}

	public override ValueTask<DriverCreationResult<DnsSdInstanceId>?> InvokeFactoryAsync
	(
		DnsSdDriverFactory factory,
		ComponentCreationParameters<DnsSdInstanceId, DnsSdDriverCreationContext> creationParameters,
		CancellationToken cancellationToken
	)
		=> factory(creationParameters.CreationContext!.Lifetime, creationParameters.CreationContext!, cancellationToken);
}
