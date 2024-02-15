using System.Collections.Immutable;
using System.Reflection;
using DeviceTools;
using DeviceTools.DisplayDevices.Configuration;
using Exo.Services;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

// TODO: This does need some code to reprocess device arrivals when new factories are registered.
// For now, if the service is started before all factories are registered, some devices will be missed, which is less than ideal.
public sealed class MonitorDiscoverySubsystem : Component, IDiscoveryService<SystemDevicePath, MonitorDiscoveryContext, MonitorDriverCreationContext, Driver, DriverCreationResult<SystemDevicePath>>, IDeviceNotificationSink
{
	[DiscoverySubsystem<RootDiscoverySubsystem>]
	[RootComponent(typeof(MonitorDiscoverySubsystem))]
	public static ValueTask<RootComponentCreationResult> CreateAsync
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService
	)
	{
		return new(new RootComponentCreationResult(typeof(MonitorDiscoverySubsystem), new MonitorDiscoverySubsystem(loggerFactory, driverRegistry, discoveryOrchestrator, deviceNotificationService), null));
	}

	// We allow a unique "catch-all" factory for monitors, as many monitors can be handled with generic DDC code and some text-based configuration files per monitor model.
	private Guid _defaultFactory;
	private readonly Dictionary<ProductKey, Guid> _productFactories;
	private readonly Dictionary<VendorKey, Guid> _vendorFactories;

	private readonly ILogger<MonitorDiscoverySubsystem> _logger;
	internal ILoggerFactory LoggerFactory { get; }
	internal INestedDriverRegistryProvider DriverRegistry { get; }
	private readonly IDiscoveryOrchestrator _discoveryOrchestrator;
	private readonly IDeviceNotificationService _deviceNotificationService;

	private IDisposable? _monitorNotificationRegistration;
	private IDiscoverySink<SystemDevicePath, MonitorDiscoveryContext, MonitorDriverCreationContext>? _sink;
	private readonly object _lock;

	public MonitorDiscoverySubsystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService
	)
	{
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		_logger = loggerFactory.CreateLogger<MonitorDiscoverySubsystem>();
		_discoveryOrchestrator = discoveryOrchestrator;
		_deviceNotificationService = deviceNotificationService;

		_productFactories = new();
		_vendorFactories = new();

		_lock = new();

		_sink = _discoveryOrchestrator.RegisterDiscoveryService<MonitorDiscoverySubsystem, SystemDevicePath, MonitorDiscoveryContext, MonitorDriverCreationContext, Driver, DriverCreationResult<SystemDevicePath>>(this);
	}

	public override string FriendlyName => "Monitor Discovery";

	public override ValueTask DisposeAsync()
	{
		lock (_lock)
		{
			Interlocked.Exchange(ref _monitorNotificationRegistration, null)?.Dispose();
			Interlocked.Exchange(ref _sink, null)?.Dispose();
		}
		return ValueTask.CompletedTask;
	}

	public ValueTask StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			lock (_lock)
			{
				if (_monitorNotificationRegistration is not null)
				{
					throw new InvalidOperationException("The service has already been started.");
				}

				_logger.MonitorDiscoveryStarting();

				_monitorNotificationRegistration = _deviceNotificationService.RegisterDeviceNotifications(DeviceInterfaceClassGuids.Monitor, this);

				foreach (string deviceName in Device.EnumerateAllInterfaces(DeviceInterfaceClassGuids.Monitor))
				{
					_logger.MonitorDeviceArrival(deviceName);
					_sink?.HandleArrival(new(this, deviceName));
				}

				_logger.MonitorDiscoveryStarted();
			}
		}
		catch (Exception ex)
		{
			return ValueTask.FromException(ex);
		}
		return ValueTask.CompletedTask;
	}

	public bool RegisterFactory(Guid factoryId, ImmutableArray<CustomAttributeData> attributes)
	{
		var productKeys = new HashSet<ProductKey>();
		var vendorKeys = new HashSet<VendorKey>();
		bool registeredForMonitorDeviceInterfaceClass = false;

		foreach (var attribute in attributes)
		{
			if (attribute.Matches<DeviceInterfaceClassAttribute>())
			{
				registeredForMonitorDeviceInterfaceClass |= (DeviceInterfaceClass)(int)attribute.ConstructorArguments[0].Value! == DeviceInterfaceClass.Monitor;
			}
			else if (attribute.Matches<MonitorNameAttribute>())
			{
				var arguments = attribute.ConstructorArguments;

				if (arguments.Count == 1)
				{
					string? rawMonitorName = (string?)arguments[0].Value;

					if (!MonitorName.TryParse(rawMonitorName, out var monitorName))
					{
						_logger.MonitorNameParsingFailure(factoryId, rawMonitorName);
						continue;
					}

					productKeys.Add(new ProductKey(VendorIdSource.PlugAndPlay, monitorName.VendorId.Value, monitorName.ProductId));
				}
				else if (arguments.Count == 2)
				{
					ushort vendorId = (ushort)arguments[0].Value!;
					ushort productId = (ushort)arguments[0].Value!;

					if (!PnpVendorId.IsRawValueValid(vendorId))
					{
						_logger.MonitorInvalidVendorId(factoryId, vendorId);
						continue;
					}

					productKeys.Add(new ProductKey(VendorIdSource.PlugAndPlay, vendorId, productId));
				}
			}
			else if (attribute.Matches<PnpVendorIdAttribute>())
			{
				var arguments = attribute.ConstructorArguments;

				if (arguments[0].Value is string vendorIdText)
				{
					if (!PnpVendorId.TryParse(vendorIdText, out var vendorId))
					{
						_logger.MonitorVendorIdParsingFailure(factoryId, vendorIdText);
						continue;
					}

					vendorKeys.Add(new VendorKey(VendorIdSource.PlugAndPlay, vendorId.Value));
				}
				else if (arguments[0].Value is ushort rawVendorId)
				{
					if (!PnpVendorId.IsRawValueValid(rawVendorId))
					{
						_logger.MonitorInvalidVendorId(factoryId, rawVendorId);
						continue;
					}

					vendorKeys.Add(new VendorKey(VendorIdSource.PlugAndPlay, rawVendorId));
				}
			}
		}

		bool hasKeys = productKeys.Count != 0 || vendorKeys.Count != 0;
		if (!(registeredForMonitorDeviceInterfaceClass || hasKeys))
		{
			_logger.MonitorFactoryMissingKeys(factoryId);
			return false;
		}

		// Prevent factories from being registered in parallel, so that key conflicts between factories can be avoided mostly deterministically.
		// Obviously, key conflicts will still depend on the order of discovery of the factories, but they should not exist at all anyway.
		lock (_lock)
		{
			// Allow the first matching factory to be registered as a catch-all factory for all monitors.
			// It will always be the fallback after more specific factories.
			if (!hasKeys && registeredForMonitorDeviceInterfaceClass)
			{
				if (_defaultFactory != default)
				{
					return false;
				}
				_defaultFactory = factoryId;
			}

			// Sadly, the best way to ensure coherence is to do all of this in two steps, which will be a little bit costlier.
			// We mostly need coherence for the sake of providing complete factory registrations upon device arrivals.

			// First, check that all the keys will avoid a conflict
			foreach (var key in productKeys)
			{
				if (_productFactories.ContainsKey(key))
				{
					_logger.MonitorProductRegistrationConflict(PnpVendorId.FromRaw(key.VendorId).ToString(), key.ProductId);
					return false;
				}
			}
			foreach (var key in vendorKeys)
			{
				if (_vendorFactories.ContainsKey(key))
				{
					_logger.MonitorVendorRegisteredTwice(PnpVendorId.FromRaw(key.VendorId).ToString());
					return false;
				}
			}

			// Once we are guaranteed to be conflict-free, the keys are added.
			foreach (var key in productKeys)
			{
				_productFactories.Add(key, factoryId);
			}
			foreach (var key in vendorKeys)
			{
				_vendorFactories.Add(key, factoryId);
			}
		}

		return true;
	}

	void IDeviceNotificationSink.OnDeviceArrival(Guid deviceInterfaceClassGuid, string deviceName)
	{
		lock (_lock)
		{
			_logger.MonitorDeviceArrival(deviceName);
			_sink?.HandleArrival(new(this, deviceName));
		}
	}

	void IDeviceNotificationSink.OnDeviceRemovePending(Guid deviceInterfaceClassGuid, string deviceName)
	{
		lock (_lock)
		{
			_sink?.HandleRemoval(deviceName);
		}
	}

	void IDeviceNotificationSink.OnDeviceRemoveComplete(Guid deviceInterfaceClassGuid, string deviceName)
	{
		lock (_lock)
		{
			_logger.MonitorDeviceRemoval(deviceName);
			_sink?.HandleRemoval(deviceName);
		}
	}

	internal ImmutableArray<Guid> ResolveFactories(DeviceId deviceId)
	{
		Span<Guid> factories = stackalloc Guid[3];
		int count = 0;
		Guid guid;

		lock (_lock)
		{
			if (_productFactories.TryGetValue(new(deviceId.VendorIdSource, deviceId.VendorId, deviceId.ProductId), out guid))
			{
				factories[count++] = guid;
			}
			if (_vendorFactories.TryGetValue(new(deviceId.VendorIdSource, deviceId.VendorId), out guid))
			{
				factories[count++] = guid;
			}
			if (_defaultFactory != default)
			{
				factories[count++] = _defaultFactory;
			}
		}

		return factories[..count].ToImmutableArray();
	}
}
