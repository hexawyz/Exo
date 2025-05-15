using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using DeviceTools;
using Exo.Features;
using Exo.Services;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

// TODO: This does need some code to reprocess device arrivals when new factories are registered.
// For now, if the service is started before all factories are registered, some devices will be missed, which is less than ideal.
public sealed class PciDiscoverySubsystem :
	DiscoveryService<PciDiscoverySubsystem, SystemDevicePath, PciFactoryDetails, PciDiscoveryContext, PciDriverCreationContext, Driver, DriverCreationResult<SystemDevicePath>>,
	IDeviceNotificationSink,
	IJsonTypeInfoProvider<PciFactoryDetails>
{
	static JsonTypeInfo<PciFactoryDetails> IJsonTypeInfoProvider<PciFactoryDetails>.JsonTypeInfo => SourceGenerationContext.Default.PciFactoryDetails;

	[DiscoverySubsystem<RootDiscoverySubsystem>]
	[RootComponent(typeof(PciDiscoverySubsystem))]
	public static async ValueTask<RootComponentCreationResult> CreateAsync
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService,
		Func<string, IDisplayAdapterI2cBusProviderFeature> fallbackI2cBusProviderFeatureProvider
	)
	{
		var service = new PciDiscoverySubsystem(loggerFactory, driverRegistry, deviceNotificationService, fallbackI2cBusProviderFeatureProvider);
		try
		{
			await service.RegisterAsync(discoveryOrchestrator);
		}
		catch
		{
			await service.DisposeAsync();
			throw;
		}
		return new RootComponentCreationResult(typeof(PciDiscoverySubsystem), service);
	}

	private readonly Dictionary<ProductVersionKey, Guid> _productVersionFactories;
	private readonly Dictionary<ProductKey, Guid> _productFactories;
	private readonly Dictionary<VendorKey, Guid> _vendorFactories;

	private readonly ILogger<PciDiscoverySubsystem> _logger;
	internal ILoggerFactory LoggerFactory { get; }
	internal INestedDriverRegistryProvider DriverRegistry { get; }
	private readonly IDeviceNotificationService _deviceNotificationService;
	internal Func<string, IDisplayAdapterI2cBusProviderFeature> FallbackI2cBusProviderFeatureProvider { get; }

	private IDisposable? _displayAdapterNotificationRegistration;
	private IDisposable? _displayDeviceArrivalDeviceNotificationRegistration;
	private readonly Lock _lock;

	public PciDiscoverySubsystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDeviceNotificationService deviceNotificationService,
		Func<string, IDisplayAdapterI2cBusProviderFeature> fallbackI2cBusProviderFeatureProvider
	)
	{
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		_logger = loggerFactory.CreateLogger<PciDiscoverySubsystem>();
		_deviceNotificationService = deviceNotificationService;
		FallbackI2cBusProviderFeatureProvider = fallbackI2cBusProviderFeatureProvider;

		_productVersionFactories = new();
		_productFactories = new();
		_vendorFactories = new();

		_lock = new();
	}

	public override string FriendlyName => "PCI Discovery";

	public override ValueTask DisposeAsync()
	{
		lock (_lock)
		{
			Interlocked.Exchange(ref _displayAdapterNotificationRegistration, null)?.Dispose();
			Interlocked.Exchange(ref _displayDeviceArrivalDeviceNotificationRegistration, null)?.Dispose();
			DisposeSink();
		}
		return ValueTask.CompletedTask;
	}

	protected override Task StartAsync(IDiscoverySink<SystemDevicePath, PciDiscoveryContext, PciDriverCreationContext> sink, CancellationToken cancellationToken)
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

				// NB: Not very intuitive, but GUID_DISPLAY_DEVICE_ARRIVAL will actually return all GPUs,
				// while GUID_DEVINTERFACE_DISPLAY_ADAPTER would return only those with display connections.
				// This is actually explained here, but I missed it the first time I investigated which one to use:
				// https://learn.microsoft.com/en-us/windows-hardware/drivers/install/guid-devinterface-display-adapter
				foreach (string deviceName in Device.EnumerateAllInterfaces(DeviceInterfaceClassGuids.DisplayDeviceArrival))
				{
					if (IsBasicDisplayDevice(deviceName) || IsBasicRender(deviceName)) continue;

					_logger.DisplayAdapterDeviceArrival(deviceName);
					sink?.HandleArrival(new(this, deviceName));
				}

				_logger.PciDiscoveryStarted();
			}
		}
		catch (Exception ex)
		{
			return Task.FromException(ex);
		}
		return Task.CompletedTask;
	}

	public override bool TryParseFactory(ImmutableArray<CustomAttributeData> attributes, [NotNullWhen(true)] out PciFactoryDetails parsedFactoryDetails)
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
								goto Failed;
							}
						}
						else
						{
							var key = new ProductKey(vendorIdSource, vendorId, productId.GetValueOrDefault());
							if (!productKeys.Add(key))
							{
								_logger.PciProductDuplicateKey(key.VendorIdSource, key.VendorId, key.ProductId);
								goto Failed;
							}
						}
					}
					else
					{
						var key = new VendorKey(vendorIdSource, vendorId);
						if (!vendorKeys.Add(key))
						{
							_logger.PciVendorDuplicateKey(key.VendorIdSource, key.VendorId);
							goto Failed;
						}
					}
				}
			}
		}

		if (productVersionKeys.Count == 0 && productKeys.Count == 0 && vendorKeys.Count == 0)
		{
			_logger.PciFactoryMissingKeys();
			goto Failed;
		}

		parsedFactoryDetails = new()
		{
			ProductVersions = [.. productVersionKeys],
			Products = [.. productKeys],
			Vendors = [.. vendorKeys],
		};
		return true;
	Failed:;
		parsedFactoryDetails = default;
		return false;
	}

	public override bool TryRegisterFactory(Guid factoryId, PciFactoryDetails parsedFactoryDetails)
	{
		// Prevent factories from being registered in parallel, so that key conflicts between factories can be avoided mostly deterministically.
		// Obviously, key conflicts will still depend on the order of discovery of the factories, but they should not exist at all anyway.
		lock (_lock)
		{
			// Sadly, the best way to ensure coherence is to do all of this in two steps, which will be a little bit costlier.
			// We mostly need coherence for the sake of providing complete factory registrations upon device arrivals.

			// First, check that all the keys will avoid a conflict
			foreach (var key in parsedFactoryDetails.ProductVersions)
			{
				if (_productVersionFactories.ContainsKey(key))
				{
					_logger.PciVersionedProductRegistrationConflict(key.VendorIdSource, key.VendorId, key.ProductId, key.VersionNumber);
					return false;
				}
			}
			foreach (var key in parsedFactoryDetails.Products)
			{
				if (_productFactories.ContainsKey(key))
				{
					_logger.PciProductRegistrationConflict(key.VendorIdSource, key.VendorId, key.ProductId);
					return false;
				}
			}
			foreach (var key in parsedFactoryDetails.Vendors)
			{
				if (_vendorFactories.ContainsKey(key))
				{
					_logger.PciVendorRegisteredTwice(key.VendorIdSource, key.VendorId);
					return false;
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
	}

	// This is a quick hack to avoid "BasicDisplay" and "BasicRender" devices generating errors as they are not real monitors.
	// Proper detection would require querying the device class of the parent node and eliminating all "System" devices, but this should be good enough.
	// (Although devices (interface) names are supposed to be somewhat undocumented, they are relatively stable)
	private static bool IsBasicDisplayDevice(string deviceName) => deviceName.StartsWith(@"\\?\ROOT#BasicDisplay#", StringComparison.OrdinalIgnoreCase);
	private static bool IsBasicRender(string deviceName) => deviceName.StartsWith(@"\\?\ROOT#BasicRender#", StringComparison.OrdinalIgnoreCase);

	void IDeviceNotificationSink.OnDeviceArrival(Guid deviceInterfaceClassGuid, string deviceName)
	{
		if (IsBasicDisplayDevice(deviceName) || IsBasicRender(deviceName)) return;

		lock (_lock)
		{
			_logger.DisplayAdapterDeviceArrival(deviceName);
			TryGetSink()?.HandleArrival(new(this, deviceName));
		}
	}

	void IDeviceNotificationSink.OnDeviceRemovePending(Guid deviceInterfaceClassGuid, string deviceName)
	{
		if (IsBasicDisplayDevice(deviceName) || IsBasicRender(deviceName)) return;

		lock (_lock)
		{
			TryGetSink()?.HandleRemoval(deviceName);
		}
	}

	void IDeviceNotificationSink.OnDeviceRemoveComplete(Guid deviceInterfaceClassGuid, string deviceName)
	{
		if (IsBasicDisplayDevice(deviceName) || IsBasicRender(deviceName)) return;

		lock (_lock)
		{
			_logger.DisplayAdapterDeviceRemoval(deviceName);
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
