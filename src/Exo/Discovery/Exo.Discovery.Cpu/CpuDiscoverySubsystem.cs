using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using DeviceTools.Processors;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public sealed class CpuDiscoverySubsystem :
	DiscoveryService<CpuDiscoverySubsystem, SystemCpuDeviceKey, CpuDriverFactoryDetails, CpuDiscoveryContext, CpuDriverCreationContext, Driver, DriverCreationResult<SystemCpuDeviceKey>>
{
	[DiscoverySubsystem<RootDiscoverySubsystem>]
	[RootComponent(typeof(CpuDiscoverySubsystem))]
	public static async ValueTask<RootComponentCreationResult?> CreateAsync
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator
	)
	{
		// Only support X86 CPUs, at least for now.
		if (!X86Base.IsSupported) return null;

		// For now, there should never be a case where we have heterogeneous vendors or even CPU packages, so we can just ake the CPUID from a random core on the system.
		var vendorId = X86VendorId.ForCurrentCpu();

		var processorPackages = ProcessorPackageInformation.GetAll();
		var service = new CpuDiscoverySubsystem(loggerFactory, driverRegistry, vendorId, processorPackages);
		try
		{
			await service.RegisterAsync(discoveryOrchestrator);
		}
		catch
		{
			await service.DisposeAsync();
			throw;
		}
		return new RootComponentCreationResult(typeof(CpuDiscoverySubsystem), service);
	}

	private readonly Dictionary<X86VendorId, Guid> _cpuFactories;

	private readonly ILogger<CpuDiscoverySubsystem> _logger;
	private readonly X86VendorId _vendorId;
	private readonly ImmutableArray<ProcessorPackageInformation> _processorPackages;
	internal ILoggerFactory LoggerFactory { get; }
	internal INestedDriverRegistryProvider DriverRegistry { get; }

	private readonly Lock _lock;

	private CpuDiscoverySubsystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		X86VendorId vendorId,
		ImmutableArray<ProcessorPackageInformation> processorPackages
	)
	{
		_logger = loggerFactory.CreateLogger<CpuDiscoverySubsystem>();
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		_vendorId = vendorId;
		_processorPackages = processorPackages;

		_cpuFactories = new();

		_lock = new();
	}

	public override string FriendlyName => "CPU Discovery";

	internal ImmutableArray<ProcessorPackageInformation> ProcessorPackages => _processorPackages;

	protected override ValueTask StartAsync(IDiscoverySink<SystemCpuDeviceKey, CpuDiscoveryContext, CpuDriverCreationContext> sink, CancellationToken cancellationToken)
	{
		Guid factoryId;

		lock (_lock)
		{
			if (_cpuFactories.Count == 0 || !_cpuFactories.TryGetValue(_vendorId, out factoryId)) return ValueTask.CompletedTask;
		}

		for (int i = 0; i < _processorPackages.Length; i++)
		{
			sink.HandleArrival(new(this, _vendorId, i, factoryId));
		}

		return ValueTask.CompletedTask;
	}

	public override bool TryParseFactory(ImmutableArray<CustomAttributeData> attributes, [NotNullWhen(true)] out CpuDriverFactoryDetails parsedFactoryDetails)
	{
		var keys = ImmutableArray.CreateBuilder<X86VendorId>();
		foreach (var attribute in attributes)
		{
			if (attribute.Matches<X86CpuVendorIdAttribute>() &&
				attribute.ConstructorArguments is [{ Value: string vendorId }] &&
				vendorId is { Length: 12 })
			{
				keys.Add(new(vendorId));
			}
		}
		if (keys.Count > 0)
		{
			parsedFactoryDetails = new() { SupportedVendors = keys.DrainToImmutable() };
			return true;
		}
		parsedFactoryDetails = default;
		return false;
	}

	public override bool TryRegisterFactory(Guid factoryId, CpuDriverFactoryDetails parsedFactoryDetails)
	{
		lock (_lock)
		{
			foreach (var key in parsedFactoryDetails.SupportedVendors)
			{
				if (_cpuFactories.ContainsKey(key))
				{
					return false;
				}
			}

			foreach (var key in parsedFactoryDetails.SupportedVendors)
			{
				_cpuFactories.TryAdd(key, factoryId);
			}
		}
		return true;
	}
}
