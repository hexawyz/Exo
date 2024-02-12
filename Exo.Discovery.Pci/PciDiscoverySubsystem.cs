using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using DeviceTools;
using Exo.Services;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

// TODO: This does need some code to reprocess device arrivals when new factories are registered.
// For now, if the service is started before all factories are registered, some devices will be missed, which is less than ideal.
public sealed class PciDiscoverySubsystem : Component, IDiscoveryService<SystemDevicePath, PciDiscoveryContext, PciDriverCreationContext, Driver, DriverCreationResult<SystemDevicePath>>, IDeviceNotificationSink
{
	[DiscoverySubsystem<RootDiscoverySubsystem>]
	[RootComponent(typeof(PciDiscoverySubsystem))]
	public static ValueTask<RootComponentCreationResult> CreateAsync
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService
	)
	{
		return new(new RootComponentCreationResult(typeof(PciDiscoverySubsystem), new PciDiscoverySubsystem(loggerFactory, driverRegistry, discoveryOrchestrator, deviceNotificationService), null));
	}

	private readonly Dictionary<ProductVersionKey, Guid> _productVersionFactories;
	private readonly Dictionary<ProductKey, Guid> _productFactories;
	private readonly Dictionary<VendorKey, Guid> _vendorFactories;

	private readonly ILogger<PciDiscoverySubsystem> _logger;
	internal ILoggerFactory LoggerFactory { get; }
	internal INestedDriverRegistryProvider DriverRegistry { get; }
	private readonly IDiscoveryOrchestrator _discoveryOrchestrator;
	private readonly IDeviceNotificationService _deviceNotificationService;

	private IDisposable? _displayAdapterNotificationRegistration;
	private IDisposable? _displayDeviceArrivalDeviceNotificationRegistration;
	private IDiscoverySink<SystemDevicePath, PciDiscoveryContext, PciDriverCreationContext>? _sink;
	private readonly object _lock;

	public PciDiscoverySubsystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService
	)
	{
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		_logger = loggerFactory.CreateLogger<PciDiscoverySubsystem>();
		_discoveryOrchestrator = discoveryOrchestrator;
		_deviceNotificationService = deviceNotificationService;

		_productVersionFactories = new();
		_productFactories = new();
		_vendorFactories = new();

		_lock = new();

		_sink = _discoveryOrchestrator.RegisterDiscoveryService<PciDiscoverySubsystem, SystemDevicePath, PciDiscoveryContext, PciDriverCreationContext, Driver, DriverCreationResult<SystemDevicePath>>(this);
	}

	public override string FriendlyName => "PCI Discovery";

	public override ValueTask DisposeAsync()
	{
		lock (_lock)
		{
			Interlocked.Exchange(ref _displayAdapterNotificationRegistration, null)?.Dispose();
			Interlocked.Exchange(ref _displayDeviceArrivalDeviceNotificationRegistration, null)?.Dispose();
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
				if (_displayAdapterNotificationRegistration is not null || _displayDeviceArrivalDeviceNotificationRegistration is not null)
				{
					throw new InvalidOperationException("The service has already been started.");
				}

				_logger.PciDiscoveryStarting();

				_displayAdapterNotificationRegistration = _deviceNotificationService.RegisterDeviceNotifications(DeviceInterfaceClassGuids.DisplayAdapter, this);
				_displayDeviceArrivalDeviceNotificationRegistration = _deviceNotificationService.RegisterDeviceNotifications(DeviceInterfaceClassGuids.DisplayDeviceArrival, this);

				foreach (string deviceName in Device.EnumerateAllInterfaces(DeviceInterfaceClassGuids.DisplayAdapter))
				{
					_logger.DisplayAdapterDeviceArrival(deviceName);
					_sink?.HandleArrival(new(this, deviceName));
				}

				_logger.PciDiscoveryStarted();
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
		var productVersionKeys = new HashSet<ProductVersionKey>();
		var productKeys = new HashSet<ProductKey>();
		var vendorKeys = new HashSet<VendorKey>();
		var deviceInterfaceClassKeys = new HashSet<DeviceInterfaceClass>();

		foreach (var attribute in attributes)
		{
			if (attribute.Matches<DeviceInterfaceClassAttribute>())
			{
				deviceInterfaceClassKeys.Add((DeviceInterfaceClass)(int)attribute.ConstructorArguments[0].Value!);
			}
			else if (attribute.Matches<VendorIdAttribute>() ||
				attribute.Matches<ProductIdAttribute>() ||
				attribute.Matches<ProductVersionAttribute>())
			{
				var arguments = attribute.ConstructorArguments;

				if (arguments.Count >= 2)
				{
					var vendorIdSource = (VendorIdSource)(byte)arguments[0].Value!;
					ushort vendorId = (ushort)arguments[1].Value!;
					ushort? productId = arguments.Count >= 3 ? (ushort)arguments[2].Value! : null;
					ushort? versionNumber = arguments.Count >= 4 ? (ushort)arguments[3].Value! : null;

					if (productId is not null)
					{
						if (versionNumber is not null)
						{
							var key = new ProductVersionKey(vendorIdSource, vendorId, productId.GetValueOrDefault(), versionNumber.GetValueOrDefault());

							if (!productVersionKeys.Add(key))
							{
								_logger.PciVersionedProductDuplicateKey(key.VendorIdSource, key.VendorId, key.ProductId, key.VersionNumber);
								return false;
							}
						}
						else
						{
							var key = new ProductKey(vendorIdSource, vendorId, productId.GetValueOrDefault());
							if (!productKeys.Add(key))
							{
								_logger.PciProductDuplicateKey(key.VendorIdSource, key.VendorId, key.ProductId);
								return false;
							}
						}
					}
					else
					{
						var key = new VendorKey(vendorIdSource, vendorId);
						if (!vendorKeys.Add(key))
						{
							_logger.PciVendorDuplicateKey(key.VendorIdSource, key.VendorId);
							return false;
						}
					}
				}
			}
		}

		if (productVersionKeys.Count == 0 && productKeys.Count == 0 && vendorKeys.Count == 0)
		{
			_logger.PciFactoryMissingKeys(factoryId);
			return false;
		}

		// Prevent factories from being registered in parallel, so that key conflicts between factories can be avoided mostly deterministically.
		// Obviously, key conflicts will still depend on the order of discovery of the factories, but they should not exist at all anyway.
		lock (_lock)
		{
			// Sadly, the best way to ensure coherence is to do all of this in two steps, which will be a little bit costlier.
			// We mostly need coherence for the sake of providing complete factory registrations upon device arrivals.

			// First, check that all the keys will avoid a conflict
			foreach (var key in productVersionKeys)
			{
				if (_productVersionFactories.ContainsKey(key))
				{
					_logger.PciVersionedProductRegistrationConflict(key.VendorIdSource, key.VendorId, key.ProductId, key.VersionNumber);
					return false;
				}
			}
			foreach (var key in productKeys)
			{
				if (_productFactories.ContainsKey(key))
				{
					_logger.PciProductRegistrationConflict(key.VendorIdSource, key.VendorId, key.ProductId);
					return false;
				}
			}
			foreach (var key in vendorKeys)
			{
				if (_vendorFactories.ContainsKey(key))
				{
					_logger.PciVendorRegisteredTwice(key.VendorIdSource, key.VendorId);
					return false;
				}
			}

			// Once we are guaranteed to be conflict-free, the keys are added.
			foreach (var key in productVersionKeys)
			{
				_productVersionFactories.Add(key, factoryId);
			}
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
			_logger.DisplayAdapterDeviceArrival(deviceName);
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
			_logger.DisplayAdapterDeviceRemoval(deviceName);
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
			if (_productVersionFactories.TryGetValue(new(deviceId.VendorIdSource, deviceId.VendorId, deviceId.ProductId, deviceId.Version), out guid))
			{
				factories[count++] = guid;
			}
			if (_productFactories.TryGetValue(new(deviceId.VendorIdSource, deviceId.VendorId, deviceId.ProductId), out guid))
			{
				factories[count++] = guid;
			}
			if (_vendorFactories.TryGetValue(new(deviceId.VendorIdSource, deviceId.VendorId), out guid))
			{
				factories[count++] = guid;
			}
		}

		return factories[..count].ToImmutableArray();
	}
}
