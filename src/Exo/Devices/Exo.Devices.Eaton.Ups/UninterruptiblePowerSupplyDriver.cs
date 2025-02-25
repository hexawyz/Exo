using System.Buffers;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.PowerManagement;
using Exo.Features.Sensors;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Eaton.Ups;

public sealed partial class UninterruptiblePowerSupplyDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<IPowerManagementDeviceFeature>,
	IDeviceDriver<ISensorDeviceFeature>,
	IDeviceIdFeature,
	ISensorsFeature,
	//ISensorsGroupedQueryFeature,
	IBatteryStateDeviceFeature
{
	private const ushort EatonVendorId = 0x0463;

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
	[ProductId(VendorIdSource.Usb, EatonVendorId, 0xFFFF)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILogger<UninterruptiblePowerSupplyDriver> logger,
		ImmutableArray<SystemDevicePath> keys,
		ushort productId,
		ushort version,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		ImmutableArray<DeviceObjectInformation> devices,
		string friendlyName,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		// The core device should be composed of exactly one HID interface derived from the USB interface.
		// However, windows recognize HID UPS devices and will derive Battery and Power Meter interfaces.
		if (deviceInterfaces.Length != 4)
		{
			throw new InvalidOperationException("Expected exactly four device interfaces.");
		}

		if (devices.Length != 2)
		{
			throw new InvalidOperationException("Expected exactly two devices.");
		}

		// All features of the device can be accessed from the sole HID interface.
		// Other device interfaces may or may not be useful…
		// From my investigations, the Power Meter device sadly seems to be doing absolutely no useful work at the moment 🙁
		string? hidDeviceInterfaceName = null;
		string? batteryDeviceInterfaceName = null;
		string? powerMeterDeviceInterfaceName = null;

		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];

			if (!deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid interfaceClassGuid))
			{
				continue;
			}

			if (interfaceClassGuid == DeviceInterfaceClassGuids.Hid)
			{
				hidDeviceInterfaceName = deviceInterface.Id;
				HidDevice.FromPath(hidDeviceInterfaceName);
			}
			else if (interfaceClassGuid == DeviceInterfaceClassGuids.Battery)
			{
				batteryDeviceInterfaceName = deviceInterface.Id;
			}
			else if (interfaceClassGuid == DeviceInterfaceClassGuids.PowerMeter)
			{
				powerMeterDeviceInterfaceName = deviceInterface.Id;
			}
		}

		if (hidDeviceInterfaceName is null || batteryDeviceInterfaceName is null || powerMeterDeviceInterfaceName is null)
		{
			throw new InvalidOperationException("One of the expected device interfaces was not found.");
		}

		var hidStream = new HidDeviceStream(Device.OpenHandle(hidDeviceInterfaceName, DeviceAccess.Read, FileShare.ReadWrite), FileAccess.Read, 0, true);
		uint batteryState;
		try
		{
			// Do a sanity check on the HID collection to ensure that it is conform to what is programmed in the driver.
			// NB: For now, we don't support dynamic parsing of HID descriptors, so we need to verify that the actual reports match what is hardcoded in our implementation.
			var collectionDescriptor = await hidStream.GetCollectionDescriptorAsync(cancellationToken).ConfigureAwait(false);

			foreach (var featureReport in collectionDescriptor.FeatureReports)
			{
				if (featureReport.ReportId == 0x01 && featureReport.ReportSize != 4 ||
					featureReport.ReportId == 0x06 && featureReport.ReportSize != 6 ||
					featureReport.ReportId == 0x0B && featureReport.ReportSize != 11 ||
					featureReport.ReportId == 0x0C && featureReport.ReportSize != 8 ||
					featureReport.ReportId == 0x0D && featureReport.ReportSize != 4 ||
					featureReport.ReportId == 0x10 && featureReport.ReportSize != 9)
				{
					throw new InvalidOperationException("This device is not yet supported.");
				}
			}

			var buffer = ArrayPool<byte>.Shared.Rent(128);
			try
			{
				// According to the report descriptor, the report 0x10 should return device information, let's see how to use it.
				// NB: When it was running the Eaton service was requesting reports 1, 7, 6, 9, 2 and 14 in a loop.
				buffer[0] = 0x10;
				await hidStream.ReceiveFeatureReportAsync(buffer.AsMemory(0, 9), cancellationToken).ConfigureAwait(false);
				string? chemistry = await hidStream.GetStringAsync(buffer[1], cancellationToken).ConfigureAwait(false);
				string? manufacturer = await hidStream.GetStringAsync(buffer[2], cancellationToken).ConfigureAwait(false);
				string? capacityName = await hidStream.GetStringAsync(buffer[3], cancellationToken).ConfigureAwait(false);
				string? product = await hidStream.GetStringAsync(buffer[4], cancellationToken).ConfigureAwait(false);
				string? unknown = await hidStream.GetStringAsync(buffer[5], cancellationToken).ConfigureAwait(false);
				string? serialNumber = await hidStream.GetStringAsync(buffer[6], cancellationToken).ConfigureAwait(false);
				string? firmwareVersion = await hidStream.GetStringAsync(buffer[7], cancellationToken).ConfigureAwait(false);
				string? connectionType = await hidStream.GetStringAsync(buffer[8], cancellationToken).ConfigureAwait(false);

				if (capacityName is not null && product is not null)
				{
					friendlyName = product + " " + capacityName;
				}

				// Read device capacity information.
				buffer[0] = 0x0D;
				await hidStream.ReceiveFeatureReportAsync(buffer.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);

				ushort capacity = LittleEndian.ReadUInt16(in buffer[1]);
				byte frequency = buffer[3];

				// TODO: This should provide some information on the device (Output ID, Flow ID, etc) but no idea how to interpret it yet.
				buffer[0] = 0x0B;
				await hidStream.ReceiveFeatureReportAsync(buffer.AsMemory(0, 11), cancellationToken).ConfigureAwait(false);

				// Read device battery information.
				buffer[0] = 0x0C;
				await hidStream.ReceiveFeatureReportAsync(buffer.AsMemory(0, 8), cancellationToken).ConfigureAwait(false);

				// Not sure reading this is very useful, as the battery is only exposed in % here.
				bool isSwitchable = buffer[1] != 0;
				byte designCapacity = buffer[5];
				byte fullChargeCapacity = buffer[5];

				// Do an initial battery capacity reading.
				buffer[0] = 0x06;
				await hidStream.ReceiveFeatureReportAsync(buffer.AsMemory(0, 6), cancellationToken).ConfigureAwait(false);
				batteryState = buffer[1];

				// Do an initial battery state reading.
				buffer[0] = 0x01;
				await hidStream.ReceiveFeatureReportAsync(buffer.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
				batteryState = batteryState | (uint)buffer[1] << 8 | (uint)buffer[2] << 16 | (uint)buffer[3] << 24;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}

			// TODO: Verify that the report IDs we use are as we expect.

			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new UninterruptiblePowerSupplyDriver
				(
					logger,
					hidStream,
					batteryState,
					productId,
					version,
					friendlyName,
					new("EatonUPS", topLevelDeviceName, $"{EatonVendorId:X4}:{productId:X4}", null)
				),
				null
			);
		}
		catch
		{
			await hidStream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private readonly HidDeviceStream _hidStream;
	private readonly byte[] _buffer;
	private uint _batteryState;
	private readonly ImmutableArray<ISensor> _sensors;
	private event Action<Driver, BatteryState>? BatteryStateChanged;
	private readonly ILogger<UninterruptiblePowerSupplyDriver> _logger;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly IDeviceFeatureSet<IPowerManagementDeviceFeature> _powerManagementFeatures;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _readTask;
	private readonly ushort _productId;
	private readonly ushort _versionNumber;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => _sensors;

	public override DeviceCategory DeviceCategory => DeviceCategory.PowerSupply;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;
	IDeviceFeatureSet<IPowerManagementDeviceFeature> IDeviceDriver<IPowerManagementDeviceFeature>.Features => _powerManagementFeatures;
	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;

	event Action<Driver, BatteryState> IBatteryStateDeviceFeature.BatteryStateChanged
	{
		add => BatteryStateChanged += value;
		remove => BatteryStateChanged -= value;
	}

	BatteryState IBatteryStateDeviceFeature.BatteryState => BuildBatteryState(Volatile.Read(ref _batteryState));

	DeviceId IDeviceIdFeature.DeviceId => DeviceId.ForUsb(EatonVendorId, _productId, _versionNumber);

	private UninterruptiblePowerSupplyDriver
	(
		ILogger<UninterruptiblePowerSupplyDriver> logger,
		HidDeviceStream hidStream,
		uint batteryState,
		ushort productId,
		ushort versionNumber,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_logger = logger;
		_hidStream = hidStream;
		_buffer = GC.AllocateUninitializedArray<byte>(256, true);
		_batteryState = batteryState;
		_productId = productId;
		_versionNumber = versionNumber;
		_sensors =
		[
			new GroupedSensor<byte>(this, 0x07, 8, 10, 6, PercentLoadSensorId, SensorUnit.Percent, 0, 100),
			new SimpleSensor<ushort>(this, 0x0E, 7, OutputVoltageSensorId, SensorUnit.Volts, 0, null),
		];
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, UninterruptiblePowerSupplyDriver, ISensorsFeature>(this);
		_powerManagementFeatures = FeatureSet.Create<IPowerManagementDeviceFeature, UninterruptiblePowerSupplyDriver, IBatteryStateDeviceFeature>(this);
		_genericFeatures = FeatureSet.Create<IGenericDeviceFeature, UninterruptiblePowerSupplyDriver, IDeviceIdFeature>(this);
		_cancellationTokenSource = new();
		_readTask = ReadAsync(_cancellationTokenSource.Token);
	}

	public override async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;
		await _hidStream.DisposeAsync().ConfigureAwait(false);
	}

	private async Task ReadAsync(CancellationToken cancellationToken)
	{
		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffer, 0, 6);
		try
		{
			// Apparently, those statuses are not sent as notifications (stream reads throw an exception), so we need to do polling ☹️
			while (true)
			{
				buffer.Span[0] = 6;
				await _hidStream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
				(byte capacity, uint remainingTime) = ProcessCapacityReport(buffer.Span.Slice(1, 5));
				buffer.Span[0] = 1;
				await _hidStream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
				ProcessBatteryStatusReport(buffer.Span.Slice(1, 3), capacity);

				await Task.Delay(60, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception)
		{
		}
	}

	private void ProcessBatteryStatusReport(ReadOnlySpan<byte> span, byte capacity)
	{
		uint oldBatteryState = _batteryState;
		UpdateBatteryState(oldBatteryState, capacity | (uint)span[0] << 8 | (uint)span[1] << 16 | (uint)span[2] << 24);
	}

	private (byte Capacity, uint RemainingTime) ProcessCapacityReport(ReadOnlySpan<byte> span) => (span[0], LittleEndian.ReadUInt32(in span[1]));

	private void UpdateBatteryState(uint oldValue, uint newValue)
	{
		if (oldValue != newValue)
		{
			Volatile.Write(ref _batteryState, newValue);
			OnBatteryStateChanged(newValue);
		}
	}

	private void OnBatteryStateChanged(uint batteryState)
	{
		if (BatteryStateChanged is { } batteryStateChanged)
		{
			_ = Task.Run
			(
				() =>
				{
					try
					{
						batteryStateChanged.Invoke(this, BuildBatteryState(batteryState));
					}
					catch (Exception ex)
					{
						// TODO: Log
					}
				}
			);
		}
	}

	private static BatteryState BuildBatteryState(uint batteryState)
	{
		byte level = (byte)batteryState;
		bool isExternalPowerConnected = (batteryState & 0x100) != 0;
		BatteryStatus batteryStatus = BatteryStatus.Unknown;

		if ((batteryState & 0x4000) != 0)
		{
			batteryStatus = BatteryStatus.Error;
		}
		else if ((batteryState & 0x1000) != 0)
		{
			batteryStatus = BatteryStatus.Discharging;
		}
		else if ((batteryState & 0x400) != 0)
		{
			batteryStatus = BatteryStatus.Charging;
		}
		else
		{
			batteryStatus = level < 100 ? BatteryStatus.Idle : BatteryStatus.ChargingComplete;
		}

		return new()
		{
			Level = (byte)batteryState * 0.01f,
			BatteryStatus = batteryStatus,
			ExternalPowerStatus = isExternalPowerConnected ? ExternalPowerStatus.IsConnected : ExternalPowerStatus.IsDisconnected,
		};
	}

	//void ISensorsGroupedQueryFeature.AddSensor(IPolledSensor sensor)
	//{
	//}

	//void ISensorsGroupedQueryFeature.RemoveSensor(IPolledSensor sensor)
	//{
	//}

	//ValueTask ISensorsGroupedQueryFeature.QueryValuesAsync(CancellationToken cancellationToken)
	//{
	//}

	private sealed class SimpleSensor<T> : IPolledSensor<T>
		where T : struct, INumber<T>
	{
		private readonly UninterruptiblePowerSupplyDriver _driver;
		private readonly byte _reportId;
		private readonly byte _bufferOffset;
		private readonly Guid _sensorId;
		private readonly SensorUnit _sensorUnit;
		private readonly T? _minimumValue;
		private readonly T? _maximumValue;

		public SimpleSensor(UninterruptiblePowerSupplyDriver driver, byte reportId, byte bufferOffset, Guid sensorId, SensorUnit sensorUnit, T? minimumValue, T? maximumValue)
		{
			_driver = driver;
			_reportId = reportId;
			_bufferOffset = bufferOffset;
			_sensorId = sensorId;
			_sensorUnit = sensorUnit;
			_minimumValue = minimumValue;
			_maximumValue = maximumValue;
		}

		public Guid SensorId => _sensorId;
		public SensorUnit Unit => _sensorUnit;

		public T? ScaleMinimumValue => _minimumValue;
		public T? ScaleMaximumValue => _maximumValue;

		public async ValueTask<T> GetValueAsync(CancellationToken cancellationToken)
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_driver._buffer, _bufferOffset, 1 + Unsafe.SizeOf<T>());
			buffer.Span[0] = _reportId;
			await _driver._hidStream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
			return Unsafe.ReadUnaligned<T>(ref buffer.Span[1]);
		}
	}

	private sealed class GroupedSensor<T> : IPolledSensor<T>
		where T : struct, INumber<T>
	{
		private readonly UninterruptiblePowerSupplyDriver _driver;
		private readonly byte _reportId;
		private readonly byte _reportLength;
		private readonly byte _bufferOffset;
		private readonly byte _dataOffset;
		private readonly Guid _sensorId;
		private readonly SensorUnit _sensorUnit;
		private readonly T? _minimumValue;
		private readonly T? _maximumValue;

		public GroupedSensor(UninterruptiblePowerSupplyDriver driver, byte reportId, byte reportLength, byte bufferOffset, byte dataOffset, Guid sensorId, SensorUnit sensorUnit, T? minimumValue, T? maximumValue)
		{
			_driver = driver;
			_reportId = reportId;
			_reportLength = reportLength;
			_bufferOffset = bufferOffset;
			_dataOffset = dataOffset;
			_sensorId = sensorId;
			_sensorUnit = sensorUnit;
			_minimumValue = minimumValue;
			_maximumValue = maximumValue;
		}

		public Guid SensorId => _sensorId;
		public SensorUnit Unit => _sensorUnit;
		public byte ReportId => _reportId;

		public T? ScaleMinimumValue => _minimumValue;
		public T? ScaleMaximumValue => _maximumValue;

		public async ValueTask<T> GetValueAsync(CancellationToken cancellationToken)
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_driver._buffer, _bufferOffset, _reportLength);
			buffer.Span[0] = _reportId;
			await _driver._hidStream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
			return Unsafe.ReadUnaligned<T>(ref buffer.Span[_dataOffset]);
		}
	}
}
