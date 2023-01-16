using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Core;
using Exo.Core.Services;
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
	//private readonly ConditionalWeakTable<Type, Func<Driver>> _driverFactoryMethods;
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
		_deviceNotificationService.RegisterDeviceNotifications(DeviceInterfaceClassGuids.Hid, this);
		_eventProcessingTask = ProcessAsync(_eventChannel.Reader, cancellationToken);
		return Task.CompletedTask;
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
}
