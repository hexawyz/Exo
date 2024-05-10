using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.MonitorFeatures;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Nzxt.Kraken;

public class KrakenDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceIdFeature,
	IDeviceSerialNumberFeature,
	IDeviceDriver<ISensorDeviceFeature>,
	ISensorsFeature,
	ISensorsGroupedQueryFeature,
	IDeviceDriver<IMonitorDeviceFeature>,
	IMonitorBrightnessFeature
{
	private static readonly Guid LiquidTemperatureSensorId = new(0x8E880DE1, 0x2A45, 0x400D, 0xA9, 0x0F, 0x42, 0xE8, 0x9B, 0xF9, 0x50, 0xDB);
	private static readonly Guid PumpSpeedSensorId = new(0x3A2F0F14, 0x3957, 0x400E, 0x8B, 0x6C, 0xCB, 0x02, 0x5B, 0x89, 0x15, 0x06);
	private static readonly Guid FanSpeedSensorId = new(0xFDC93D5B, 0xEDE3, 0x4774, 0x96, 0xEC, 0xC4, 0xFD, 0xB1, 0xC1, 0xDE, 0xBC);

	private const int NzxtVendorId = 0x1E71;

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
	[ProductId(VendorIdSource.Usb, NzxtVendorId, 0x3008)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILogger<KrakenDriver> logger,
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
		if (deviceInterfaces.Length != 4)
		{
			throw new InvalidOperationException("Expected exactly four device interfaces.");
		}

		if (devices.Length != 4)
		{
			throw new InvalidOperationException("Expected exactly four devices.");
		}

		string? hidDeviceInterfaceName = null;
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
		}

		if (hidDeviceInterfaceName is null)
		{
			throw new InvalidOperationException("One of the expected device interfaces was not found.");
		}

		var hidStream = new HidFullDuplexStream(hidDeviceInterfaceName);
		try
		{
			string? serialNumber = await hidStream.GetSerialNumberAsync(cancellationToken).ConfigureAwait(false);
			var transport = new KrakenHidTransport(hidStream);
			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new KrakenDriver
				(
					logger,
					transport,
					productId,
					version,
					friendlyName,
					new("Kraken", topLevelDeviceName, $"{NzxtVendorId:X4}:{productId:X4}", serialNumber)
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

	private readonly KrakenHidTransport _transport;
	private readonly ISensor[] _sensors;
	private readonly ILogger<KrakenDriver> _logger;

	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly IDeviceFeatureSet<IMonitorDeviceFeature> _monitorFeatures;

	private int _groupQueriedSensorCount;
	private readonly ushort _productId;
	private readonly ushort _versionNumber;

	public override DeviceCategory DeviceCategory => DeviceCategory.Other;
	DeviceId IDeviceIdFeature.DeviceId => DeviceId.ForUsb(NzxtVendorId, _productId, _versionNumber);
	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => ImmutableCollectionsMarshal.AsImmutableArray(_sensors);

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;
	IDeviceFeatureSet<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;

	private KrakenDriver
	(
		ILogger<KrakenDriver> logger,
		KrakenHidTransport transport,
		ushort productId,
		ushort versionNumber,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	)
		: base(friendlyName, configurationKey)
	{
		_logger = logger;
		_transport = transport;
		_productId = productId;
		_versionNumber = versionNumber;
		_sensors = [new LiquidTemperatureSensor(this), new PumpSpeedSensor(this), new FanSpeedSensor(this)];
		_genericFeatures = ConfigurationKey.UniqueId is not null ?
			FeatureSet.Create<IGenericDeviceFeature, KrakenDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Create<IGenericDeviceFeature, KrakenDriver, IDeviceIdFeature>(this);
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, KrakenDriver, ISensorsFeature, ISensorsGroupedQueryFeature>(this);
		_monitorFeatures = FeatureSet.Create<IMonitorDeviceFeature, KrakenDriver, IMonitorBrightnessFeature>(this);
	}

	public override ValueTask DisposeAsync() => _transport.DisposeAsync();

	async ValueTask<ContinuousValue> IMonitorBrightnessFeature.GetBrightnessAsync(CancellationToken cancellationToken)
	{
		byte brightness = await _transport.GetBrightnessAsync(cancellationToken).ConfigureAwait(false);
		return new ContinuousValue(brightness, 0, 100);
	}

	async ValueTask IMonitorBrightnessFeature.SetBrightnessAsync(ushort value, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
		await _transport.SetBrightnessAsync((byte)value, cancellationToken).ConfigureAwait(false);
	}

	void ISensorsGroupedQueryFeature.AddSensor(IPolledSensor sensor)
	{
		if (sensor is not Sensor s || s.Driver != this) throw new InvalidOperationException();

		if (!s.IsGroupQueryEnabled)
		{
			s.IsGroupQueryEnabled = true;
			_groupQueriedSensorCount++;
		}
	}

	void ISensorsGroupedQueryFeature.RemoveSensor(IPolledSensor sensor)
	{
		if (sensor is not Sensor s || s.Driver != this) throw new InvalidOperationException();

		if (s.IsGroupQueryEnabled)
		{
			s.IsGroupQueryEnabled = false;
			_groupQueriedSensorCount--;
		}
	}

	ValueTask ISensorsGroupedQueryFeature.QueryValuesAsync(CancellationToken cancellationToken)
	{
		var readings = _transport.GetLastReadings();

		foreach (var sensor in _sensors)
		{
			Unsafe.As<Sensor>(sensor).RefreshValue(readings);
		}

		return ValueTask.CompletedTask;
	}

	private abstract class Sensor
	{
		private readonly KrakenDriver _driver;

		protected Sensor(KrakenDriver driver) => _driver = driver;

		public KrakenDriver Driver => _driver;

		public abstract bool IsGroupQueryEnabled { get; set; }

		public abstract void RefreshValue(KrakenReadings readings);
	}

	private abstract class Sensor<T> : Sensor, IPolledSensor<T>
		where T : struct, INumber<T>
	{
		private T _lastValue;
		private bool _isGroupQueryEnabled;

		protected Sensor(KrakenDriver driver) : base(driver) { }

		public T? ScaleMinimumValue => default(T);
		public virtual T? ScaleMaximumValue => null;
		public abstract Guid SensorId { get; }
		public abstract SensorUnit Unit { get; }
		public sealed override bool IsGroupQueryEnabled { get => _isGroupQueryEnabled; set => _isGroupQueryEnabled = value; }
		public GroupedQueryMode GroupedQueryMode => _isGroupQueryEnabled ? GroupedQueryMode.Enabled : GroupedQueryMode.Supported;

		public sealed override void RefreshValue(KrakenReadings readings)
			=> _lastValue = ReadValue(readings);

		protected abstract T ReadValue(KrakenReadings readings);

		public ValueTask<T> GetValueAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_lastValue);

		public bool TryGetLastValue(out T lastValue)
		{
			lastValue = _lastValue;
			return true;
		}
	}

	private sealed class LiquidTemperatureSensor : Sensor<byte>
	{
		public override Guid SensorId => LiquidTemperatureSensorId;
		public override SensorUnit Unit => SensorUnit.Celsius;
		public override byte? ScaleMaximumValue => 100;

		public LiquidTemperatureSensor(KrakenDriver driver) : base(driver) { }

		protected override byte ReadValue(KrakenReadings readings) => readings.LiquidTemperature;
	}

	private sealed class PumpSpeedSensor : Sensor<ushort>
	{
		public override Guid SensorId => PumpSpeedSensorId;
		public override SensorUnit Unit => SensorUnit.RotationsPerMinute;

		public PumpSpeedSensor(KrakenDriver driver) : base(driver) { }

		protected override ushort ReadValue(KrakenReadings readings) => readings.PumpSpeed;
	}

	private sealed class FanSpeedSensor : Sensor<ushort>
	{
		public override Guid SensorId => FanSpeedSensorId;
		public override SensorUnit Unit => SensorUnit.RotationsPerMinute;

		public FanSpeedSensor(KrakenDriver driver) : base(driver) { }

		protected override ushort ReadValue(KrakenReadings readings) => readings.FanSpeed;
	}
}

internal readonly struct KrakenReadings
{
	private readonly ulong _rawValue;

	public byte LiquidTemperature => (byte)(_rawValue >> 48);
	public byte FanPower => (byte)(_rawValue >> 40);
	public byte PumpPower => (byte)(_rawValue >> 32);
	public ushort FanSpeed => (ushort)(_rawValue >> 16);
	public ushort PumpSpeed => (ushort)_rawValue;

	internal KrakenReadings(ushort pumpSpeed, ushort fanSpeed, byte pumpPower, byte fanPower, byte liquidTemperature)
	{
		_rawValue = pumpSpeed | (uint)fanSpeed << 16 | (ulong)pumpPower << 32 | (ulong)fanPower << 40 | (ulong)liquidTemperature << 48;
	}
}

internal sealed class KrakenHidTransport : IAsyncDisposable
{
	// The message length is 64 bytes including the report ID, which indicates a specific command.
	private const int MessageLength = 64;

	private const byte ScreenSettingsRequestMessageId = 0x30;
	private const byte ScreenSettingsResponseMessageId = 0x31;
	private const byte DisplayChangeRequestMessageId = 0x38;
	private const byte CurrentDeviceStatusResponseMessageId = 0x75;

	private readonly HidFullDuplexStream _stream;
	private readonly byte[] _buffers;
	private ulong _lastReadings;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _task;
	private TaskCompletionSource<byte>? _screenInfoRetrievalTaskCompletionSource;

	public KrakenHidTransport(HidFullDuplexStream stream)
	{
		_stream = stream;
		_buffers = GC.AllocateUninitializedArray<byte>(2 * MessageLength, true);
		_cancellationTokenSource = new();
		_task = ReadAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;
		cts.Cancel();
		await _stream.DisposeAsync().ConfigureAwait(false);
		await _task.ConfigureAwait(false);
		cts.Dispose();
	}

	private async Task ReadAsync(CancellationToken cancellationToken)
	{
		// NB: In this initial version, we passively receive readings, because we let the external software handle everything.
		// As far as readings are concerned, we may want to keep a decorrelation between request and response anyway. This would likely allow to work more gracefully with other software.
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, 0, MessageLength);
			while (true)
			{
				try
				{
					// Data is received in fixed length packets, so we expect to always receive exactly the number of bytes that the buffer can hold.
					int count = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
					if (count == 0) return;
					if (count != buffer.Length) throw new InvalidOperationException();
				}
				catch (OperationCanceledException)
				{
					return;
				}

				ProcessReadMessage(buffer.Span);
			}
		}
		catch
		{
			// TODO: Log
		}
	}

	public async ValueTask<byte> GetBrightnessAsync(CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (Interlocked.CompareExchange(ref _screenInfoRetrievalTaskCompletionSource, tcs, null) is not null) throw new InvalidOperationException();

		static void PrepareRequest(Span<byte> buffer)
		{
			buffer.Clear();
			buffer[0] = ScreenSettingsRequestMessageId;
			buffer[1] = 0x01;
		}

		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
		PrepareRequest(buffer.Span);
		await _stream.WriteAsync(buffer, default).ConfigureAwait(false);
		try
		{
			return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			Volatile.Write(ref _screenInfoRetrievalTaskCompletionSource, null);
		}
	}

	public async ValueTask SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
	{
		static void PrepareRequest(Span<byte> buffer, byte brightness)
		{
			buffer.Clear();
			buffer[0] = ScreenSettingsRequestMessageId;
			buffer[1] = 0x02;
			buffer[2] = 0x01;
			buffer[3] = brightness;
			buffer[7] = 0x03;
		}

		ArgumentOutOfRangeException.ThrowIfGreaterThan(brightness, 100);

		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
		PrepareRequest(buffer.Span, brightness);
		await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
	}

	private void ProcessReadMessage(ReadOnlySpan<byte> message)
	{
		switch (message[0])
		{
		case CurrentDeviceStatusResponseMessageId:
			ProcessDeviceStatusResponse(message[1], message[14..]);
			break;
		case ScreenSettingsResponseMessageId:
			ProcessScreenInformationResponse(message[1], message[14..]);
			break;
		}
	}

	private void ProcessDeviceStatusResponse(byte functionId, ReadOnlySpan<byte> response)
	{
		if (functionId == 0x01)
		{
			byte liquidTemperature = response[1];
			ushort pumpSpeed = LittleEndian.ReadUInt16(in response[3]);
			byte pumpSetPower = response[19];
			ushort fanSpeed = LittleEndian.ReadUInt16(in response[9]);
			byte fanSetPower = response[11];
			var readings = new KrakenReadings(pumpSpeed, fanSpeed, pumpSetPower, fanSetPower, liquidTemperature);
			Volatile.Write(ref _lastReadings, Unsafe.BitCast<KrakenReadings, ulong>(readings));
		}
	}

	private void ProcessScreenInformationResponse(byte functionId, ReadOnlySpan<byte> response)
	{
		if (functionId == 0x01)
		{
			byte imageCount = response[5];
			ushort imageWidth = LittleEndian.ReadUInt16(in response[6]);
			ushort imageHeight = LittleEndian.ReadUInt16(in response[8]);
			byte brightness = response[10];

			_screenInfoRetrievalTaskCompletionSource?.TrySetResult(brightness);
		}
	}

	public KrakenReadings GetLastReadings()
		=> Unsafe.BitCast<ulong, KrakenReadings>(Volatile.Read(ref _lastReadings));
}
