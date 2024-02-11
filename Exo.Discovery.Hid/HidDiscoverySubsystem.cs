using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using DeviceTools;
using Exo.Services;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

// TODO: This does need some code to reprocess device arrivals when new factories are registered.
// For now, if the service is started before all factories are registered, some devices will be missed, which is less than ideal.
public sealed class HidDiscoverySubsystem : Component, IDiscoveryService<SystemDevicePath, HidDiscoveryContext, HidDriverCreationContext, Driver, DriverCreationResult<SystemDevicePath>>, IDeviceNotificationSink
{
	[DiscoverySubsystem<RootDiscoverySubsystem>]
	[RootComponent(typeof(HidDiscoverySubsystem))]
	public static ValueTask<RootComponentCreationResult> CreateAsync
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService
	)
	{
		return new(new RootComponentCreationResult(typeof(HidDiscoverySubsystem), new HidDiscoverySubsystem(loggerFactory, driverRegistry, discoveryOrchestrator, deviceNotificationService), null));
	}

	private readonly Dictionary<HidProductVersionKey, Guid> _productVersionFactories;
	private readonly Dictionary<HidProductKey, Guid> _productFactories;
	private readonly Dictionary<HidVendorKey, Guid> _vendorFactories;

	private readonly ILogger<HidDiscoverySubsystem> _logger;
	internal ILoggerFactory LoggerFactory { get; }
	internal INestedDriverRegistryProvider DriverRegistry { get; }
	private readonly IDiscoveryOrchestrator _discoveryOrchestrator;
	private readonly IDeviceNotificationService _deviceNotificationService;

	private IDisposable? _deviceNotificationRegistration;
	private IDiscoverySink<SystemDevicePath, HidDiscoveryContext, HidDriverCreationContext>? _sink;
	private readonly object _lock;

	public HidDiscoverySubsystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService
	)
	{
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		_logger = loggerFactory.CreateLogger<HidDiscoverySubsystem>();
		_discoveryOrchestrator = discoveryOrchestrator;
		_deviceNotificationService = deviceNotificationService;

		_productVersionFactories = new();
		_productFactories = new();
		_vendorFactories = new();

		_lock = new();

		_sink = _discoveryOrchestrator.RegisterDiscoveryService<HidDiscoverySubsystem, SystemDevicePath, HidDiscoveryContext, HidDriverCreationContext, Driver, DriverCreationResult<SystemDevicePath>>(this);
	}

	public override string FriendlyName => "HID Discovery";

	public override ValueTask DisposeAsync()
	{
		lock (_lock)
		{
			Interlocked.Exchange(ref _deviceNotificationRegistration, null)?.Dispose();
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
				if (_deviceNotificationRegistration is not null) throw new InvalidOperationException("The service has already been started.");

				_deviceNotificationRegistration = _deviceNotificationService.RegisterDeviceNotifications(DeviceInterfaceClassGuids.Hid, this);

				foreach (string deviceName in Device.EnumerateAllInterfaces(DeviceInterfaceClassGuids.Hid))
				{
					_sink?.HandleArrival(new(this, deviceName));
				}
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
		var productVersionKeys = new HashSet<HidProductVersionKey>();
		var productKeys = new HashSet<HidProductKey>();
		var vendorKeys = new HashSet<HidVendorKey>();

		foreach (var attribute in attributes)
		{
			if (attribute.Matches<VendorIdAttribute>() ||
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
							var key = new HidProductVersionKey(vendorIdSource, vendorId, productId.GetValueOrDefault(), versionNumber.GetValueOrDefault());

							if (!productVersionKeys.Add(key))
							{
								// TODO: Log duplicate key
								return false;
							}
						}
						else
						{
							var key = new HidProductKey(vendorIdSource, vendorId, productId.GetValueOrDefault());
							if (!productKeys.Add(key))
							{
								// TODO: Log duplicate key
								return false;
							}
						}
					}
					else
					{
						var key = new HidVendorKey(vendorIdSource, vendorId);
						if (!vendorKeys.Add(key))
						{
							// TODO: Log duplicate key
							return false;
						}
					}
				}
			}
		}

		if (productVersionKeys.Count == 0 && productKeys.Count == 0 && vendorKeys.Count == 0)
		{
			// TODO: Log missing HID keys.
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
					// TODO: Log key conflict.
					return false;
				}
			}
			foreach (var key in productKeys)
			{
				if (_productFactories.ContainsKey(key))
				{
					// TODO: Log key conflict.
					return false;
				}
			}
			foreach (var key in vendorKeys)
			{
				if (_vendorFactories.ContainsKey(key))
				{
					// TODO: Log key conflict.
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
