using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DeviceTools.Firmware;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public sealed class SystemManagementBiosRamDiscoverySubsystem :
	DiscoveryService<SystemManagementBiosRamDiscoverySubsystem, SystemMemoryDeviceKey, RamModuleDriverFactoryDetails, RamModuleDiscoveryContext, RamModuleDriverCreationContext, Driver, DriverCreationResult<SystemMemoryDeviceKey>>
{
	[DiscoverySubsystem<RootDiscoverySubsystem>]
	[RootComponent(typeof(SystemManagementBiosRamDiscoverySubsystem))]
	public static async ValueTask<RootComponentCreationResult?> CreateAsync
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator
	)
	{
		var modules = GetInstalledModules(SmBios.GetForCurrentMachine());

		// No need to instantiate the discovery service if no memory modules were detected.
		// SMBIOS is a statis data structure, so this will never change until a restart.
		// (Don't know how things would work out for memory hotplug though, but that is not a realistic feature for consumer devices)
		if (modules.Length == 0) return null;

		var service = new SystemManagementBiosRamDiscoverySubsystem(loggerFactory, driverRegistry, modules);
		try
		{
			await service.RegisterAsync(discoveryOrchestrator);
		}
		catch
		{
			await service.DisposeAsync();
			throw;
		}
		return new RootComponentCreationResult(typeof(SystemManagementBiosRamDiscoverySubsystem), service);
	}

	private static ImmutableArray<MemoryModuleInformation> GetInstalledModules(SmBios smBios)
	{
		var memoryDevices = smBios.MemoryDevices;
		var keys = ImmutableArray.CreateBuilder<MemoryModuleInformation>(memoryDevices.Length);
		for (int i = 0; i < memoryDevices.Length; i++)
		{
			var memoryDevice = memoryDevices[i];
			if (memoryDevice.PartNumber is { Length: not 0 } partNumber && memoryDevice.ModuleManufacturerId is ushort manufacturerId)
			{
				keys.Add(new() { Index = (byte)i, ManufacturerCode = JedecManufacturerCode.FromRawValue(manufacturerId), PartNumber = partNumber.TrimEnd('\x20') });
			}
		}
		return keys.DrainToImmutable();
	}

	private readonly Dictionary<RamModuleKey, Guid> _ramModuleFactories;
	private readonly ImmutableArray<MemoryModuleInformation> _installedModules;

	private readonly ILogger<SystemManagementBiosRamDiscoverySubsystem> _logger;
	internal ILoggerFactory LoggerFactory { get; }
	internal INestedDriverRegistryProvider DriverRegistry { get; }

	private readonly object _lock;

	private SystemManagementBiosRamDiscoverySubsystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		ImmutableArray<MemoryModuleInformation> installedModules
	)
	{
		_logger = loggerFactory.CreateLogger<SystemManagementBiosRamDiscoverySubsystem>();
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		_installedModules = installedModules;

		_ramModuleFactories = new();

		_lock = new();
	}

	public override string FriendlyName => "SMBIOS RAM Discovery";

	protected override ValueTask StartAsync(IDiscoverySink<SystemMemoryDeviceKey, RamModuleDiscoveryContext, RamModuleDriverCreationContext> sink, CancellationToken cancellationToken)
	{
		// NB: Most installations would have a single type or module or two that are all tied to the same factory.
		// We could optimize for that case here, but the overhead of the dictionary is probably not significant enough.
		var modulesByFactories = new Dictionary<Guid, ImmutableArray<MemoryModuleInformation>.Builder>();
		lock (_lock)
		{
			if (_ramModuleFactories.Count == 0) return ValueTask.CompletedTask;

			foreach (var module in _installedModules)
			{
				if (_ramModuleFactories.TryGetValue(new RamModuleKey() { ManufacturerCode = module.ManufacturerCode, PartNumber = module.PartNumber }, out var factoryId))
				{
					if (!modulesByFactories.TryGetValue(factoryId, out var modules))
					{
						modulesByFactories.Add(factoryId, modules = ImmutableArray.CreateBuilder<MemoryModuleInformation>());
					}
					modules.Add(module);
				}
			}
		}

		foreach (var kvp in modulesByFactories)
		{
			var modules = kvp.Value.DrainToImmutable();
			sink.HandleArrival(new(this, modules, kvp.Key));
		}

		return ValueTask.CompletedTask;
	}

	public override bool TryParseFactory(ImmutableArray<CustomAttributeData> attributes, [NotNullWhen(true)] out RamModuleDriverFactoryDetails parsedFactoryDetails)
	{
		var keys = ImmutableArray.CreateBuilder<RamModuleKey>();
		foreach (var attribute in attributes)
		{
			if (attribute.Matches<RamModuleIdAttribute>() &&
				attribute.ConstructorArguments is [ { Value: byte bankNumber }, { Value: byte manufacturerIndex }, { Value: string partNumber }] &&
				(sbyte)(bankNumber | manufacturerIndex) >= 0)
			{
				keys.Add(new() { ManufacturerCode = new JedecManufacturerCode(bankNumber, manufacturerIndex), PartNumber = partNumber });
			}
		}
		if (keys.Count > 0)
		{
			parsedFactoryDetails = new() { SupportedModules = keys.DrainToImmutable() };
			return true;
		}
		parsedFactoryDetails = default;
		return false;
	}

	public override bool TryRegisterFactory(Guid factoryId, RamModuleDriverFactoryDetails parsedFactoryDetails)
	{
		lock (_lock)
		{
			foreach (var key in parsedFactoryDetails.SupportedModules)
			{
				if (_ramModuleFactories.ContainsKey(key))
				{
					return false;
				}
			}

			foreach (var key in parsedFactoryDetails.SupportedModules)
			{
				_ramModuleFactories.TryAdd(key, factoryId);
			}
		}
		return true;
	}
}

public readonly struct SystemMemoryDeviceKey : IEquatable<SystemMemoryDeviceKey>
{
	private readonly byte _index;

	private SystemMemoryDeviceKey(byte index) => _index = index;

	public override bool Equals(object? obj) => obj is SystemMemoryDeviceKey key && Equals(key);
	public bool Equals(SystemMemoryDeviceKey other) => _index == other._index;
	public override int GetHashCode() => HashCode.Combine(_index);

	public static implicit operator SystemMemoryDeviceKey(byte index) => new(index);
	public static explicit operator byte(SystemMemoryDeviceKey key) => key._index;

	public static bool operator ==(SystemMemoryDeviceKey left, SystemMemoryDeviceKey right) => left.Equals(right);
	public static bool operator !=(SystemMemoryDeviceKey left, SystemMemoryDeviceKey right) => !(left == right);
}
