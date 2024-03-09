using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using DeviceTools;
using Exo.Discovery;
using Exo.Features;
using Exo.I2C;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Intel;

public sealed class IntelGpuDriver :
	Driver,
	IDeviceIdFeature,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<IDisplayAdapterDeviceFeature>,
	IDisplayAdapterI2CBusProviderFeature
{
	private const ushort IntelVendorId = 0x8086;

	[DiscoverySubsystem<PciDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.DisplayAdapter)]
	[DeviceInterfaceClass(DeviceInterfaceClass.DisplayDeviceArrival)]
	[VendorId(VendorIdSource.Pci, IntelVendorId)]
	public static ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		DeviceObjectInformation topLevelDevice,
		DeviceId deviceId,
		ILogger<IntelGpuDriver> logger
	)
	{
		// First, we need identify the expected bus number and address.
		// This will be used to match the device through the NVIDIA API.
		if (!topLevelDevice.Properties.TryGetValue(Properties.System.Devices.BusNumber.Key, out uint busNumber))
		{
			throw new InvalidOperationException($"Could not retrieve the bus number for the device {topLevelDevice.Id}.");
		}
		if (!topLevelDevice.Properties.TryGetValue(Properties.System.Devices.Address.Key, out uint pciAddress))
		{
			throw new InvalidOperationException($"Could not retrieve the bus address for the device {topLevelDevice.Id}.");
		}

		ControlLibrary.DisplayAdapter foundGpu = default;
		foreach (var gpu in ControlLibrary.GetDisplayAdapters())
		{
			var infos = gpu.GetInformations();

			if (infos.PciVendorId == deviceId.VendorId && infos.PciDeviceId == deviceId.ProductId && infos.BusNumber == busNumber && infos.DeviceIndex == (pciAddress >> 16) && infos.FunctionId == (ushort)pciAddress)
			{
				foundGpu = gpu;
				break;
			}
		}
		if (!foundGpu.IsValid)
		{
			throw new InvalidOperationException($"Could not find the corresponding GPU through Intel Control Library for {topLevelDevice.Id}.");
		}

		return new
		(
			new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new IntelGpuDriver
				(
					deviceId,
					"",
					new("ig", topLevelDevice.Id, $"{IntelVendorId:X4}:{deviceId.ProductId:X4}", null),
					foundGpu
				)
			)
		);
	}

	private sealed class MonitorI2CBus : II2CBus
	{
		private readonly nint _gpu;

		public MonitorI2CBus(nint gpu)
		{
			_gpu = gpu;
		}

		public ValueTask WriteAsync(byte address, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
		{
			return ValueTask.CompletedTask;
		}

		public ValueTask WriteAsync(byte address, byte register, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
		{
			return ValueTask.CompletedTask;
		}

		public ValueTask ReadAsync(byte address, Memory<byte> bytes, CancellationToken cancellationToken)
		{
			return ValueTask.CompletedTask;
		}

		public ValueTask ReadAsync(byte address, byte register, Memory<byte> bytes, CancellationToken cancellationToken)
		{
			return ValueTask.CompletedTask;
		}

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private readonly IDeviceFeatureCollection<IDisplayAdapterDeviceFeature> _displayAdapterFeatures;
	private readonly IDeviceFeatureCollection<IGenericDeviceFeature> _genericFeatures;
	private readonly ControlLibrary.DisplayAdapter _gpu;

	public DeviceId DeviceId { get; }
	public override DeviceCategory DeviceCategory => DeviceCategory.GraphicsAdapter;

	IDeviceFeatureCollection<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureCollection<IDisplayAdapterDeviceFeature> IDeviceDriver<IDisplayAdapterDeviceFeature>.Features => _displayAdapterFeatures;

	private IntelGpuDriver
	(
		DeviceId deviceId,
		string friendlyName,
		DeviceConfigurationKey configurationKey,
		ControlLibrary.DisplayAdapter gpu
	) : base(friendlyName, configurationKey)
	{
		DeviceId = deviceId;
		_gpu = gpu;
		_displayAdapterFeatures = FeatureCollection.Create<IDisplayAdapterDeviceFeature, IntelGpuDriver, IDisplayAdapterI2CBusProviderFeature>(this);
		_genericFeatures = FeatureCollection.Create<IGenericDeviceFeature, IntelGpuDriver, IDeviceIdFeature>(this);
	}

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

	string IDisplayAdapterI2CBusProviderFeature.DeviceName => ConfigurationKey.DeviceMainId;

	ValueTask<II2CBus> IDisplayAdapterI2CBusProviderFeature.GetBusForMonitorAsync(PnpVendorId vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken)
	{
		return ValueTask.FromException<II2CBus>(ExceptionDispatchInfo.SetCurrentStackTrace(new NotImplementedException()));
	}
}
