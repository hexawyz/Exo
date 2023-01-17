using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DeviceTools;
using Exo.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

public sealed class HidDeviceManager : IHostedService, IDeviceNotificationSink
{
	private enum EventKind
	{
		Arrival,
		Removal
	}

	private static readonly UnboundedChannelOptions EventChannelOptions = new() { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = true };

	private record struct DriverTypeReference(AssemblyName AssemblyName, string TypeName);

	private readonly ILogger<HidDeviceManager> _logger;
	private readonly IAssemblyLoader _assemblyLoader;
	private readonly IAssemblyParsedDataCache<HidAssembyDetails> _parsedDataCache;
	private readonly ISystemDeviceDriverRegistry _systemDeviceDriverRegistry;
	private readonly IDeviceNotificationService _deviceNotificationService;
	private readonly DriverRegistry _driverRegistry;
	private readonly Channel<(string DeviceName, EventKind Event)> _eventChannel;
	private IDisposable? _deviceNotificationRegistration;
	private Task? _eventProcessingTask;
	private readonly ConcurrentDictionary<HidVendorKey, DriverTypeReference> _vendorDrivers;
	private readonly ConcurrentDictionary<HidProductKey, DriverTypeReference> _productDrivers;
	private readonly ConcurrentDictionary<HidVersionedProductKey, DriverTypeReference> _versionedProductDrivers;
	private readonly ConditionalWeakTable<Type, Func<Task<Driver>>> _driverFactoryMethods;
	private readonly Dictionary<(ushort ProductId, ushort VendorId, ushort? VersionNumber), (AssemblyName AssemblyName, string TypeName)> _knownHidDrivers;

	public HidDeviceManager
	(
		ILogger<HidDeviceManager> logger,
		IAssemblyLoader assemblyLoader,
		IAssemblyParsedDataCache<HidAssembyDetails> parsedDataCache,
		ISystemDeviceDriverRegistry systemDeviceDriverRegistry,
		DriverRegistry driverRegistry,
		IDeviceNotificationService deviceNotificationService
	)
	{
		_logger = logger;
		_assemblyLoader = assemblyLoader;
		_parsedDataCache = parsedDataCache;
		_systemDeviceDriverRegistry = systemDeviceDriverRegistry;
		_deviceNotificationService = deviceNotificationService;
		_driverRegistry = driverRegistry;
		_eventChannel = Channel.CreateUnbounded<(string DeviceName, EventKind Event)>(EventChannelOptions);
		_vendorDrivers = new();
		_productDrivers = new();
		_versionedProductDrivers = new();
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		RefreshDriverCache();
		_deviceNotificationService.RegisterDeviceNotifications(DeviceInterfaceClassGuids.Hid, this);
		_eventProcessingTask = ProcessAsync(_eventChannel.Reader, cancellationToken);
		return Task.CompletedTask;
	}

	private void RefreshDriverCache()
	{
		foreach (var assembly in _assemblyLoader.AvailableAssemblies)
		{
			OnAssemblyAdded(assembly);
		}
	}

	private void OnAssemblyAdded(AssemblyName assembly)
	{
		HidAssembyDetails details;
		try
		{
			if (!_parsedDataCache.TryGetValue(assembly, out details))
			{
				_parsedDataCache.SetValue(assembly, details = ParseAssembly(assembly));
			}
		}
		catch (Exception ex)
		{
			_logger.HidAssemblyParsingFailure(assembly.FullName, ex);
			return;
		}

		foreach (var kvp in details.VendorDrivers)
		{
			foreach (var key in kvp.Value)
			{
				try
				{
					_vendorDrivers.TryAdd(key, new DriverTypeReference(assembly, kvp.Key));
				}
				catch (Exception ex)
				{
					var preexistingValue = _vendorDrivers[key];

					_logger.HidVendorRegisteredTwice(key.VendorIdSource, key.VendorId, kvp.Key, assembly.FullName, preexistingValue.TypeName, preexistingValue.AssemblyName.FullName, ex);
				}
			}
		}

		foreach (var kvp in details.ProductDrivers)
		{
			foreach (var key in kvp.Value)
			{
				try
				{
					_productDrivers.TryAdd(key, new DriverTypeReference(assembly, kvp.Key));
				}
				catch (Exception ex)
				{
					var preexistingValue = _productDrivers[key];

					_logger.HidProductRegisteredTwice
					(
						key.VendorIdSource,
						key.VendorId,
						key.ProductId,
						kvp.Key,
						assembly.FullName,
						preexistingValue.TypeName,
						preexistingValue.AssemblyName.FullName,
						ex
					);
				}
			}
		}

		foreach (var kvp in details.VersionedProductDrivers)
		{
			foreach (var key in kvp.Value)
			{
				try
				{
					_versionedProductDrivers.TryAdd(key, new DriverTypeReference(assembly, kvp.Key));
				}
				catch (Exception ex)
				{
					var preexistingValue = _versionedProductDrivers[key];

					_logger.HidVersionedProductRegisteredTwice
					(
						key.VendorIdSource,
						key.VendorId,
						key.ProductId,
						key.VersionNumber,
						kvp.Key,
						assembly.FullName,
						preexistingValue.TypeName,
						preexistingValue.AssemblyName.FullName,
						ex
					);
				}
			}
		}
	}

	private HidAssembyDetails ParseAssembly(AssemblyName assemblyName)
	{
		using var context = _assemblyLoader.CreateMetadataLoadContext(assemblyName);

		var assembly = context.LoadFromAssemblyName(assemblyName);

		var vendorDrivers = new Dictionary<string, List<HidVendorKey>>();
		var productDrivers = new Dictionary<string, List<HidProductKey>>();
		var versionedProductDrivers = new Dictionary<string, List<HidVersionedProductKey>>();

		foreach (var type in assembly.DefinedTypes)
		{
			if (IsDriverType(type))
			{
				foreach (var customAttribute in type.GetCustomAttributesData())
				{
					var attributeType = customAttribute.AttributeType;

					if (attributeType.Assembly.GetName().Name == typeof(DeviceIdAttribute).Assembly.GetName().Name && attributeType.FullName == typeof(DeviceIdAttribute).FullName)
					{
						var arguments = customAttribute.ConstructorArguments;

						if (arguments.Count >= 3)
						{
							var vendorIdSource = (VendorIdSource)(byte)arguments[0].Value!;
							ushort vendorId = (ushort)arguments[1].Value!;
							ushort productId = (ushort)arguments[2].Value!;
							ushort? versionNumber = arguments.Count >= 4 ? (ushort?)arguments[3].Value : null;

							if (versionNumber is not null)
							{
								if (!versionedProductDrivers.TryGetValue(type.FullName!, out var list))
								{
									versionedProductDrivers.Add(type.FullName!, list = new());
								}
								list.Add(new(vendorIdSource, vendorId, productId, versionNumber.GetValueOrDefault()));
							}
							else
							{
								if (!productDrivers.TryGetValue(type.FullName!, out var list))
								{
									productDrivers.Add(type.FullName!, list = new());
								}
								list.Add(new(vendorIdSource, vendorId, productId));
							}
						}
					}
				}
			}
		}

		return new HidAssembyDetails
		(
			vendorDrivers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()),
			productDrivers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()),
			versionedProductDrivers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray())
		);
	}

	private bool IsDriverType(Type type)
	{
		Type? current = type;
		while (true)
		{
			if (current == null) return false;

			if (current.Assembly.GetName().Name == typeof(Driver).Assembly.GetName().Name && current.FullName == typeof(Driver).FullName)
			{
				return true;
			}

			current = current.BaseType;
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (Interlocked.Exchange(ref _eventProcessingTask, null) is Task eventProcessingTask)
		{
			Interlocked.Exchange(ref _deviceNotificationRegistration, null)?.Dispose();
			_eventChannel.Writer.TryComplete();
			await eventProcessingTask.ConfigureAwait(false);
		}
	}

	private bool TryGetDriverReference(VendorIdSource vendorIdSource, ushort vendorId, ushort productId, ushort? versionNumber, [NotNullWhen(true)] out DriverTypeReference driverTypeReference)
		=> versionNumber is ushort vn && _versionedProductDrivers.TryGetValue(new HidVersionedProductKey(vendorIdSource, vendorId, productId, vn), out driverTypeReference) ||
			_productDrivers.TryGetValue(new HidProductKey(vendorIdSource, vendorId, productId), out driverTypeReference) ||
			_vendorDrivers.TryGetValue(new HidVendorKey(vendorIdSource, vendorId), out driverTypeReference);

	private async Task ProcessAsync(ChannelReader<(string DeviceName, EventKind Event)> reader, CancellationToken cancellationToken)
	{
		try
		{
			while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false) && reader.TryRead(out var t))
			{
				switch (t.Event)
				{
				case EventKind.Arrival:
					await HandleDeviceArrivalAsync(t.DeviceName, cancellationToken).ConfigureAwait(false);
					break;
				case EventKind.Removal:
					HandleDeviceRemoval(t.DeviceName);
					break;
				}
			}
		}
		catch (OperationCanceledException) { }
	}

	private async Task HandleDeviceArrivalAsync(string deviceName, CancellationToken cancellationToken)
	{
		// Device may already have been registered by a driver covering multiple device instance IDs.
		if (_systemDeviceDriverRegistry.TryGetDriver(deviceName, out var driver))
		{
			_logger.HidDeviceDriverAlreadyAssigned(deviceName);
			return;
		}

		VendorIdSource vendorIdSource = VendorIdSource.Unknown;

		// First try to get information from the device name. It will be quicker but may not be complete.
		// It may at least help retrieving the BT VendorIdSource, which is not trivial to get.
		if (DeviceNameParser.TryParseDeviceName(deviceName, out var deviceId))
		{
			vendorIdSource = deviceId.VendorIdSource;

			if (deviceId.Version != 0xFFFF && TryGetDriverReference(deviceId.VendorIdSource, deviceId.VendorId, deviceId.ProductId, deviceId.Version, out var driverTypeReference))
			{
				_logger.HidDeviceDriverMatch(driverTypeReference.TypeName, driverTypeReference.AssemblyName.FullName, deviceName);
				return;
			}
		}

		var properties = await DeviceQuery.GetObjectPropertiesAsync(DeviceObjectKind.DeviceInterface, deviceName, cancellationToken).ConfigureAwait(false);

		if (properties.TryGetValue(Properties.System.DeviceInterface.Hid.VendorId.Key, out var vid) &&
			properties.TryGetValue(Properties.System.DeviceInterface.Hid.ProductId.Key, out var pid))
		{
			properties.TryGetValue(Properties.System.DeviceInterface.Hid.VersionNumber.Key, out var vn);

			ushort vendorId = (ushort)vid!;
			ushort productId = (ushort)pid!;
			ushort? versionNumber = (ushort?)vn;

			if (TryGetDriverReference(vendorIdSource == VendorIdSource.Unknown ? VendorIdSource.Usb : vendorIdSource, vendorId, productId, versionNumber, out var driverTypeReference))
			{
				_logger.HidDeviceDriverMatch(driverTypeReference.TypeName, driverTypeReference.AssemblyName.FullName, deviceName);
				return;
			}
		}
	}

	private void HandleDeviceRemoval(string deviceName)
	{
		if (_systemDeviceDriverRegistry.TryGetDriver(deviceName, out var systemDeviceDriver))
		{
			if (_systemDeviceDriverRegistry.TryUnregisterDriver(systemDeviceDriver))
			{
				_logger.HidDriverUnregisterSuccess(systemDeviceDriver.GetType(), deviceName);
			}
			else
			{
				_logger.HidDriverUnregisterFailure(systemDeviceDriver.GetType(), deviceName);
			}

			if (systemDeviceDriver is Driver driver)
			{
				_driverRegistry.RemoveDriver(driver);
			}
		}
	}

	void IDeviceNotificationSink.OnDeviceArrival(Guid deviceInterfaceClassGuid, string deviceName)
	{
		_logger.HidDeviceArrival(deviceName);

		_eventChannel.Writer.TryWrite((deviceName, EventKind.Arrival));
	}

	void IDeviceNotificationSink.OnDeviceRemovePending(Guid deviceInterfaceClassGuid, string deviceName)
	{
		_logger.HidDeviceRemoval(deviceName);

		_eventChannel.Writer.TryWrite((deviceName, EventKind.Removal));
	}

	void IDeviceNotificationSink.OnDeviceRemoveComplete(Guid deviceInterfaceClassGuid, string deviceName)
	{
		_logger.HidDeviceRemoval(deviceName);

		_eventChannel.Writer.TryWrite((deviceName, EventKind.Removal));
	}
}
