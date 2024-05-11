using System.Buffers;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Sensors;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Eaton.Ups;

public sealed class UninterruptiblePowerSupplyDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
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

		var hidStream = new HidFullDuplexStream(hidDeviceInterfaceName);
		uint batteryState;
		try
		{
			// Do a sanity check on the HID collection to ensure that it is conform to what is programmed in the driver.
			var collectionDescriptor = await hidStream.GetCollectionDescriptorAsync(cancellationToken).ConfigureAwait(false);

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

	public static readonly Guid PercentLoadSensorId = new(0xD9CA6694, 0x7514, 0x429C, 0x86, 0x53, 0x66, 0x55, 0xC4, 0x30, 0x73, 0xB2);
	public static readonly Guid OutputVoltageSensorId = new(0xD8C1E0F2, 0x1712, 0x4709, 0x8B, 0x81, 0x3C, 0x2D, 0x2F, 0x77, 0xD6, 0x34);

	private readonly HidFullDuplexStream _stream;
	private readonly byte[] _buffer;
	private uint _batteryState;
	private readonly ImmutableArray<ISensor> _sensors;
	private event Action<Driver, BatteryState>? BatteryStateChanged;
	private readonly ILogger<UninterruptiblePowerSupplyDriver> _logger;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _readTask;
	private readonly ushort _productId;
	private readonly ushort _versionNumber;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => _sensors;

	public override DeviceCategory DeviceCategory => DeviceCategory.PowerSupply;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;
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
		HidFullDuplexStream stream,
		uint batteryState,
		ushort productId,
		ushort versionNumber,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_logger = logger;
		_stream = stream;
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
		_genericFeatures = FeatureSet.Create<IGenericDeviceFeature, UninterruptiblePowerSupplyDriver, IDeviceIdFeature, IBatteryStateDeviceFeature>(this);
		_cancellationTokenSource = new();
		_readTask = ReadAsync(_cancellationTokenSource.Token);
	}

	public override async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;
		await _stream.DisposeAsync().ConfigureAwait(false);
	}

	private async Task ReadAsync(CancellationToken cancellationToken)
	{
		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffer, 0, 6);
		try
		{
			while (true)
			{
				await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

				ProcessReport(buffer.Span);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private void ProcessReport(Span<byte> span)
	{
		switch (span[0])
		{
		case 0x01: ProcessBatteryStatusReport(span.Slice(1, 3)); break;
		case 0x02: break;
		case 0x03: break;
		case 0x06: ProcessCapacityReport(span.Slice(1, 5)); break;
		}
	}

	private void ProcessBatteryStatusReport(Span<byte> span)
	{
		uint oldBatteryState = _batteryState;
		UpdateBatteryState(oldBatteryState, oldBatteryState & 0xFF | (uint)span[0] << 8 | (uint)span[1] << 16 | (uint)span[2] << 24);
	}

	private void ProcessCapacityReport(Span<byte> span)
	{
		uint oldBatteryState = _batteryState;
		UpdateBatteryState(oldBatteryState, oldBatteryState & ~(uint)0xFF | span[0]);
	}

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
			await _driver._stream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
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
			await _driver._stream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
			return Unsafe.ReadUnaligned<T>(ref buffer.Span[_dataOffset]);
		}
	}
}
