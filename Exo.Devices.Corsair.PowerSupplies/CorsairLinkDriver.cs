using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Discovery;
using Exo.Features;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Corsair.PowerSupplies;

public sealed class CorsairLinkDriver : Driver, IDeviceDriver<ISensorDeviceFeature>, ISensorsFeature, ISensorsGroupedQueryFeature
{
	private abstract class Sensor
	{
		public CorsairLinkDriver Driver { get; }
		private readonly Guid _sensorId;
		public byte Command { get; }
		public sbyte Page { get; }

		protected Sensor(CorsairLinkDriver driver, Guid sensorId, byte command, sbyte page)
		{
			Driver = driver;
			_sensorId = sensorId;
			Command = command;
			Page = page;
		}

		public Guid SensorId => _sensorId;

		public bool IsGroupQueryEnabled { get; set; }

		public GroupedQueryMode GroupedQueryMode => IsGroupQueryEnabled ? GroupedQueryMode.Enabled : GroupedQueryMode.Supported;

		public virtual Unit Unit => Unit.Count;

		public abstract ValueTask GroupedQueryValueAsync(CancellationToken cancellationToken);
	}

	private abstract class Sensor<T> : Sensor, IPolledSensor<T>
		where T : struct, INumber<T>
	{
		private T _lastValue;

		protected Sensor(CorsairLinkDriver driver, Guid sensorId, byte command, sbyte page)
			: base(driver, sensorId, command, page)
		{
		}

		public virtual T? ScaleMinimumValue => null;
		public virtual T? ScaleMaximumValue => null;

		public ValueTask<T> GetValueAsync(CancellationToken cancellationToken)
			=> GroupedQueryMode == GroupedQueryMode.Enabled ?
				ValueTask.FromResult(_lastValue) :
				QueryValueAsync(cancellationToken);

		private async ValueTask<T> QueryValueAsync(CancellationToken cancellationToken)
		{
			await using (await Driver._corsairLinkGuardMutex.AcquireAsync().ConfigureAwait(false))
			{
				if (Page >= 0)
				{
					await Driver._transport.WriteByteAsync(0x00, (byte)Page, cancellationToken);
				}
				return await QueryValueWithinPageAsync(cancellationToken);
			}
		}

		public bool TryGetLastValue(out T lastValue)
		{
			lastValue = _lastValue;
			return true;
		}

		public sealed override async ValueTask GroupedQueryValueAsync(CancellationToken cancellationToken)
			=> _lastValue = await QueryValueWithinPageAsync(cancellationToken);

		protected abstract ValueTask<T> QueryValueWithinPageAsync(CancellationToken cancellationToken);
	}

	private abstract class Linear11Sensor : Sensor<float>
	{
		protected Linear11Sensor(CorsairLinkDriver driver, Guid sensorId, byte command, sbyte page) : base(driver, sensorId, command, page)
		{
		}

		protected override async ValueTask<float> QueryValueWithinPageAsync(CancellationToken cancellationToken)
			=> (float)await Driver._transport.ReadLinear11Async(Command, cancellationToken);
	}

	private sealed class TemperatureSensor : Linear11Sensor
	{
		public TemperatureSensor(CorsairLinkDriver driver, Guid sensorId, byte command, sbyte page, byte queryOrder) : base(driver, sensorId, command, page)
		{
		}

		public override Unit Unit => Unit.DegreesCelsius;
	}

	private sealed class VoltageSensor : Linear11Sensor
	{
		public VoltageSensor(CorsairLinkDriver driver, Guid sensorId, byte command, sbyte page, byte queryOrder) : base(driver, sensorId, command, page)
		{
		}

		public override Unit Unit => Unit.Volt;
	}

	private sealed class CurrentSensor : Linear11Sensor
	{
		public CurrentSensor(CorsairLinkDriver driver, Guid sensorId, byte command, sbyte page, byte queryOrder) : base(driver, sensorId, command, page)
		{
		}

		public override Unit Unit => Unit.Ampere;
	}

	private sealed class PowerSensor : Linear11Sensor
	{
		public PowerSensor(CorsairLinkDriver driver, Guid sensorId, byte command, sbyte page, byte queryOrder) : base(driver, sensorId, command, page)
		{
		}

		public override Unit Unit => Unit.Watt;
	}

	private const ushort CorsairVendorId = 0x1B1C;

	private static readonly Guid TemperatureSensor1 = new(0xD8D74A16, 0x020B, 0x4ADD, 0xB8, 0x61, 0x7B, 0x64, 0x04, 0x37, 0x58, 0x65);
	private static readonly Guid TemperatureSensor2 = new(0xAE5A078C, 0xE473, 0x4D0D, 0x81, 0x38, 0xCA, 0x7D, 0xD8, 0x85, 0x38, 0x4F);
	private static readonly Guid TemperatureSensor3 = new(0xF0C97F4C, 0x32A7, 0x4DBA, 0x9E, 0x5F, 0xCC, 0xEF, 0x47, 0xC7, 0xA5, 0x8C);

	private static readonly Guid PowerRail1Voltage = new(0xB9B10BBF, 0x2BF1, 0x424A, 0x8F, 0x03, 0x99, 0x32, 0xD3, 0x34, 0x60, 0x64);
	private static readonly Guid PowerRail1Current = new(0x9B6D76E4, 0xF770, 0x40AB, 0x88, 0x53, 0x6B, 0x15, 0xB9, 0xE8, 0xFE, 0xF7);
	private static readonly Guid PowerRail1Power = new(0x590FD05F, 0x29E6, 0x4E57, 0x82, 0xD3, 0xC9, 0x3A, 0x59, 0xB9, 0xCE, 0xB5);

	private static readonly Guid PowerRail2Voltage = new(0xB5E2B134, 0x1E8E, 0x464E, 0xAA, 0xA2, 0x04, 0x89, 0xDC, 0x13, 0xCF, 0xF7);
	private static readonly Guid PowerRail2Current = new(0x3FB507D5, 0xF982, 0x4CA6, 0xBD, 0xF8, 0x42, 0x61, 0xF5, 0x02, 0x8F, 0x9C);
	private static readonly Guid PowerRail2Power = new(0xD7FA6A2B, 0x0296, 0x46B5, 0x93, 0xA3, 0x21, 0xC7, 0xBE, 0x1B, 0x5C, 0x42);

	private static readonly Guid PowerRail3Voltage = new(0x7F3EF7D1, 0xA881, 0x43E4, 0x8E, 0xF5, 0xFA, 0x6E, 0x20, 0xF4, 0x91, 0x3D);
	private static readonly Guid PowerRail3Current = new(0xDBAB37C2, 0x4D8F, 0x4EE2, 0xA9, 0xDB, 0xB0, 0xDD, 0x48, 0xB0, 0x30, 0xC0);
	private static readonly Guid PowerRail3Power = new(0x582060CE, 0xE985, 0x41F0, 0x95, 0x2B, 0x36, 0x7F, 0xDD, 0x3A, 0x5B, 0x40);

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[ProductId(VendorIdSource.Usb, CorsairVendorId, 0x1C08)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILoggerFactory loggerFactory,
		ImmutableArray<SystemDevicePath> keys,
		ushort productId,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		ImmutableArray<DeviceObjectInformation> devices,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		if (devices.Length != 2) throw new InvalidOperationException("Expected exactly two devices.");
		if (deviceInterfaces.Length != 2) throw new InvalidOperationException("Expected exactly two device interfaces.");

		string? deviceName = null;
		foreach (var deviceInterface in deviceInterfaces)
		{
			if (deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid interfaceClassGuid) && interfaceClassGuid == DeviceInterfaceClassGuids.Hid)
			{
				deviceName = deviceInterface.Id;
			}
		}

		if (deviceName is null) throw new InvalidOperationException("HID device interface not found.");

		var stream = new HidFullDuplexStream(deviceName);
		CorsairLinkHidTransport transport;
		try
		{
			transport = await CorsairLinkHidTransport.CreateAsync(loggerFactory.CreateLogger<CorsairLinkHidTransport>(), stream, cancellationToken).ConfigureAwait(false);
			string friendlyName = await transport.ReadStringAsync(0x9A, cancellationToken).ConfigureAwait(false);
			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new CorsairLinkDriver
				(
					loggerFactory.CreateLogger<CorsairLinkDriver>(),
					transport,
					friendlyName,
					new DeviceConfigurationKey("Corsair", topLevelDeviceName, $"{CorsairVendorId:X4}{productId:X4}", null)
				)
			);
		}
		catch
		{
			await stream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private readonly CorsairLinkHidTransport _transport;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly ISensor[] _sensors;
	private readonly AsyncGlobalMutex _corsairLinkGuardMutex;
	private readonly ILogger<CorsairLinkDriver> _logger;
	private int _groupQueriedSensorCount;

	public override DeviceCategory DeviceCategory => DeviceCategory.PowerSupply;

	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;
	ImmutableArray<ISensor> ISensorsFeature.Sensors => ImmutableCollectionsMarshal.AsImmutableArray(_sensors);

	private CorsairLinkDriver
	(
		ILogger<CorsairLinkDriver> logger,
		CorsairLinkHidTransport transport,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_transport = transport;
		_logger = logger;
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, CorsairLinkDriver, ISensorsFeature, ISensorsGroupedQueryFeature>(this);
		_sensors =
		[
			new TemperatureSensor(this, TemperatureSensor1, 0x8D, -1, 0),
			new TemperatureSensor(this, TemperatureSensor2, 0x8E, -1, 1),
			new VoltageSensor(this, PowerRail3Voltage, 0x8B, 2, 2),
			new CurrentSensor(this, PowerRail3Current, 0x8C, 2, 3),
			new PowerSensor(this, PowerRail3Power, 0x96, 2, 4),
			new VoltageSensor(this, PowerRail2Voltage, 0x8B, 1, 5),
			new CurrentSensor(this, PowerRail2Current, 0x8C, 1, 6),
			new PowerSensor(this, PowerRail2Power, 0x96, 1, 7),
			new VoltageSensor(this, PowerRail1Voltage, 0x8B, 0, 8),
			new CurrentSensor(this, PowerRail1Current, 0x8C, 0, 9),
			new PowerSensor(this, PowerRail1Power, 0x96, 0, 10),
		];
		_corsairLinkGuardMutex = AsyncGlobalMutex.Get("Global\\CorsairLinkReadWriteGuardMutex");
	}

	public override async ValueTask DisposeAsync()
	{
		await _transport.DisposeAsync().ConfigureAwait(false);
	}

	void ISensorsGroupedQueryFeature.AddSensor(IPolledSensor sensor)
	{
		if (sensor is not Sensor s || s.Driver != this) throw new ArgumentException();
		if (s.GroupedQueryMode != GroupedQueryMode.Enabled)
		{
			s.IsGroupQueryEnabled = true;
			_groupQueriedSensorCount++;
		}
	}

	void ISensorsGroupedQueryFeature.RemoveSensor(IPolledSensor sensor)
	{
		if (sensor is not Sensor s || s.Driver != this) throw new ArgumentException();
		if (s.GroupedQueryMode == GroupedQueryMode.Enabled)
		{
			s.IsGroupQueryEnabled = false;
			_groupQueriedSensorCount--;
		}
	}

	async ValueTask ISensorsGroupedQueryFeature.QueryValuesAsync(CancellationToken cancellationToken)
	{
		if (_groupQueriedSensorCount == 0) return;

		await using (await _corsairLinkGuardMutex.AcquireAsync().ConfigureAwait(false))
		{
			sbyte currentSetPage = -1;
			foreach (var sensor in _sensors)
			{
				var s = Unsafe.As<Sensor>(sensor);
				if (s.GroupedQueryMode != GroupedQueryMode.Enabled) continue;

				if (currentSetPage != s.Page)
				{
					currentSetPage = s.Page;
					if (currentSetPage >= 0)
					{
						await _transport.WriteByteAsync(0x00, (byte)currentSetPage, cancellationToken);
					}
				}

				await s.GroupedQueryValueAsync(cancellationToken);
			}
		}
	}
}