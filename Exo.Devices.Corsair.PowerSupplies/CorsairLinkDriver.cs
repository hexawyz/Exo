using System.Collections.Immutable;
using System.Numerics;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Discovery;
using Exo.Features;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Corsair.PowerSupplies;

public sealed class CorsairLinkDriver : Driver, IDeviceDriver<ISensorDeviceFeature>, ISensorsFeature
{
	private abstract class Sensor<T> : IPolledSensor<T>
		where T : struct, INumber<T>
	{
		protected CorsairLinkDriver Driver { get; }
		private readonly Guid _sensorId;
		protected byte Command { get; }

		protected Sensor(CorsairLinkDriver driver, Guid sensorId, byte command)
		{
			Driver = driver;
			_sensorId = sensorId;
			Command = command;
		}

		public Guid SensorId => _sensorId;

		public TimeSpan MinimumPollingInterval => default;

		public virtual T? ScaleMinimumValue => null;
		public virtual T? ScaleMaximumValue => null;
		public virtual Unit Unit => Unit.Count;

		public abstract ValueTask<T> GetValueAsync(CancellationToken cancellationToken);
	}

	private abstract class Linear11Sensor : Sensor<float>
	{
		protected Linear11Sensor(CorsairLinkDriver driver, Guid sensorId, byte command) : base(driver, sensorId, command)
		{
		}

		public override async ValueTask<float> GetValueAsync(CancellationToken cancellationToken)
			=> (float)await Driver._transport.ReadLinear11Async(Command, cancellationToken).ConfigureAwait(false);
	}

	private sealed class TemperatureSensor : Linear11Sensor
	{
		public TemperatureSensor(CorsairLinkDriver driver, Guid sensorId, byte command) : base(driver, sensorId, command)
		{
		}

		public override Unit Unit => Unit.DegreesCelsius;
	}

	private const ushort CorsairVendorId = 0x1B1C;

	private static readonly Guid TemperatureSensor1 = new(0xD8D74A16, 0x020B, 0x4ADD, 0xB8, 0x61, 0x7B, 0x64, 0x04, 0x37, 0x58, 0x65);
	private static readonly Guid TemperatureSensor2 = new(0xAE5A078C, 0xE473, 0x4D0D, 0x81, 0x38, 0xCA, 0x7D, 0xD8, 0x85, 0x38, 0x4F);
	private static readonly Guid TemperatureSensor3 = new(0xF0C97F4C, 0x32A7, 0x4DBA, 0x9E, 0x5F, 0xCC, 0xEF, 0x47, 0xC7, 0xA5, 0x8C);

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
	private readonly ImmutableArray<ISensor> _sensors;
	private readonly ILogger<CorsairLinkDriver> _logger;

	public override DeviceCategory DeviceCategory => DeviceCategory.PowerSupply;

	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;
	ImmutableArray<ISensor> ISensorsFeature.Sensors => _sensors;

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
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, CorsairLinkDriver, ISensorsFeature>(this);
		_sensors =
		[
			new TemperatureSensor(this, TemperatureSensor1, 0x8D),
			new TemperatureSensor(this, TemperatureSensor2, 0x8E)
		];
	}

	public override async ValueTask DisposeAsync()
	{
		await _transport.DisposeAsync().ConfigureAwait(false);
	}
}
