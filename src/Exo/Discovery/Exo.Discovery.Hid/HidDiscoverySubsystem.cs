using System.Collections.Immutable;
using System.Reflection;
using DeviceTools;
using Exo.Configuration;
using Exo.I2C;
using Exo.Services;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

// TODO: This does need some code to reprocess device arrivals when new factories are registered.
// For now, if the service is started before all factories are registered, some devices will be missed, which is less than ideal.
[TypeId(0xEC7784B8, 0x4CB6, 0x48B5, 0x9E, 0xD5, 0x6C, 0x0F, 0xD7, 0xD8, 0x7B, 0x27)]
public sealed class HidDiscoverySubsystem :
	DiscoveryService<HidDiscoverySubsystem, SystemDevicePath, HidFactoryDetails, HidDiscoveryContext, HidDriverCreationContext, Driver, DriverCreationResult<SystemDevicePath>>,
	IDeviceNotificationSink
{
	[DiscoverySubsystem<RootDiscoverySubsystem>]
	[RootComponent(typeof(HidDiscoverySubsystem))]
	public static async ValueTask<RootComponentCreationResult> CreateAsync
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService
	)
	{
		var service = new HidDiscoverySubsystem(loggerFactory, driverRegistry, deviceNotificationService);
		try
		{
			await service.RegisterAsync(discoveryOrchestrator);
		}
		catch
		{
			await service.DisposeAsync();
			throw;
		}
		return new RootComponentCreationResult(typeof(HidDiscoverySubsystem), service);
	}

	private readonly Dictionary<ProductVersionKey, Guid> _productVersionFactories;
	private readonly Dictionary<ProductKey, Guid> _productFactories;
	private readonly Dictionary<VendorKey, Guid> _vendorFactories;

	private readonly ILogger<HidDiscoverySubsystem> _logger;
	internal ILoggerFactory LoggerFactory { get; }
	internal INestedDriverRegistryProvider DriverRegistry { get; }
	private readonly IDeviceNotificationService _deviceNotificationService;

	private IDisposable? _deviceNotificationRegistration;
	private readonly Lock _lock;

	public HidDiscoverySubsystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDeviceNotificationService deviceNotificationService
	)
	{
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		_logger = loggerFactory.CreateLogger<HidDiscoverySubsystem>();
		_deviceNotificationService = deviceNotificationService;

		_productVersionFactories = new();
		_productFactories = new();
		_vendorFactories = new();

		_lock = new();
	}

	public override string FriendlyName => "HID Discovery";

	public override ValueTask DisposeAsync()
	{
		lock (_lock)
		{
			Interlocked.Exchange(ref _deviceNotificationRegistration, null)?.Dispose();
			DisposeSink();
		}
		return ValueTask.CompletedTask;
	}

	protected override ValueTask StartAsync(IDiscoverySink<SystemDevicePath, HidDiscoveryContext, HidDriverCreationContext> sink, CancellationToken cancellationToken)
	{
		try
		{
			lock (_lock)
			{
				if (_deviceNotificationRegistration is not null) throw new InvalidOperationException("The service has already been started.");

				_logger.HidDiscoveryStarting();

				_deviceNotificationRegistration = _deviceNotificationService.RegisterDeviceNotifications(DeviceInterfaceClassGuids.Hid, this);

				foreach (string deviceName in Device.EnumerateAllInterfaces(DeviceInterfaceClassGuids.Hid))
				{
					_logger.HidDeviceArrival(deviceName);
					sink?.HandleArrival(new(this, deviceName));
				}

				_logger.HidDiscoveryStarted();
			}
		}
		catch (Exception ex)
		{
			return ValueTask.FromException(ex);
		}
		return ValueTask.CompletedTask;
	}

	public override bool TryParseFactory(ImmutableArray<CustomAttributeData> attributes, out HidFactoryDetails details)
	{
		var productVersionKeys = new HashSet<ProductVersionKey>();
		var productKeys = new HashSet<ProductKey>();
		var vendorKeys = new HashSet<VendorKey>();

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
							var key = new ProductVersionKey(vendorIdSource, vendorId, productId.GetValueOrDefault(), versionNumber.GetValueOrDefault());

							if (!productVersionKeys.Add(key))
							{
								_logger.HidVersionedProductDuplicateKey(key.VendorIdSource, key.VendorId, key.ProductId, key.VersionNumber);
								goto Failed;
							}
						}
						else
						{
							var key = new ProductKey(vendorIdSource, vendorId, productId.GetValueOrDefault());
							if (!productKeys.Add(key))
							{
								_logger.HidProductDuplicateKey(key.VendorIdSource, key.VendorId, key.ProductId);
								goto Failed;
							}
						}
					}
					else
					{
						var key = new VendorKey(vendorIdSource, vendorId);
						if (!vendorKeys.Add(key))
						{
							_logger.HidVendorDuplicateKey(key.VendorIdSource, key.VendorId);
							goto Failed;
						}
					}
				}
			}
		}

		if (productVersionKeys.Count == 0 && productKeys.Count == 0 && vendorKeys.Count == 0)
		{
			_logger.HidFactoryMissingKeys();
			goto Failed;
		}

		details = new()
		{
			ProductVersions = [.. productVersionKeys],
			Products = [.. productKeys],
			Vendors = [.. vendorKeys],
		};

		return true;
	Failed:;
		details = default;
		return false;
	}

	public override bool TryRegisterFactory(Guid factoryId, HidFactoryDetails parsedFactoryDetails)
	{
		// Prevent factories from being registered in parallel, so that key conflicts between factories can be avoided mostly deterministically.
		// Obviously, key conflicts will still depend on the order of discovery of the factories, but they should not exist at all anyway.
		lock (_lock)
		{
			// First, check that all the keys will avoid a conflict
			foreach (var key in parsedFactoryDetails.ProductVersions)
			{
				if (_productVersionFactories.ContainsKey(key))
				{
					_logger.HidVersionedProductRegistrationConflict(key.VendorIdSource, key.VendorId, key.ProductId, key.VersionNumber);
					goto Failed;
				}
			}
			foreach (var key in parsedFactoryDetails.Products)
			{
				if (_productFactories.ContainsKey(key))
				{
					_logger.HidProductRegistrationConflict(key.VendorIdSource, key.VendorId, key.ProductId);
					goto Failed;
				}
			}
			foreach (var key in parsedFactoryDetails.Vendors)
			{
				if (_vendorFactories.ContainsKey(key))
				{
					_logger.HidVendorRegisteredTwice(key.VendorIdSource, key.VendorId);
					goto Failed;
				}
			}

			// Once we are guaranteed to be conflict-free, the keys are added.
			foreach (var key in parsedFactoryDetails.ProductVersions)
			{
				_productVersionFactories.Add(key, factoryId);
			}
			foreach (var key in parsedFactoryDetails.Products)
			{
				_productFactories.Add(key, factoryId);
			}
			foreach (var key in parsedFactoryDetails.Vendors)
			{
				_vendorFactories.Add(key, factoryId);
			}
		}
		return true;
	Failed:;
		return false;
	}

	void IDeviceNotificationSink.OnDeviceArrival(Guid deviceInterfaceClassGuid, string deviceName)
	{
		lock (_lock)
		{
			_logger.HidDeviceArrival(deviceName);
			TryGetSink()?.HandleArrival(new(this, deviceName));
		}
	}

	void IDeviceNotificationSink.OnDeviceRemovePending(Guid deviceInterfaceClassGuid, string deviceName)
	{
		lock (_lock)
		{
			TryGetSink()?.HandleRemoval(deviceName);
		}
	}

	void IDeviceNotificationSink.OnDeviceRemoveComplete(Guid deviceInterfaceClassGuid, string deviceName)
	{
		lock (_lock)
		{
			_logger.HidDeviceRemoval(deviceName);
			TryGetSink()?.HandleRemoval(deviceName);
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
