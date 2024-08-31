using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using DeviceTools;
using Exo.Discovery;
using Exo.Features;
using Exo.I2C;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Intel;

public abstract class IntelGpuDriver : Driver, IDeviceIdFeature
{
	private const ushort IntelVendorId = 0x8086;

	[DiscoverySubsystem<PciDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.DisplayAdapter)]
	[DeviceInterfaceClass(DeviceInterfaceClass.DisplayDeviceArrival)]
	[VendorId(VendorIdSource.Pci, IntelVendorId)]
	public static ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILogger<IntelGpuDriver> logger,
		ImmutableArray<SystemDevicePath> keys,
		string? friendlyName,
		DeviceObjectInformation topLevelDevice,
		DeviceId deviceId,
		IDisplayAdapterI2cBusProviderFeature? fallbackI2cBusProviderFeature
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
		try
		{
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
		}
		catch when (fallbackI2cBusProviderFeature is not null)
		{
			goto Fallback;
		}

		return new
		(
			new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new ControlLibraryDriver
				(
					deviceId,
					friendlyName ?? "Intel GPU",
					new("intel_gpu", topLevelDevice.Id, $"{IntelVendorId:X4}:{deviceId.ProductId:X4}", null),
					foundGpu
				)
			)
		);

	Fallback:;
		return new
		(
			new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new FallbackIntelGpuDriver
				(
					deviceId,
					friendlyName ?? "Intel GPU",
					new("intel_gpu", topLevelDevice.Id, $"{IntelVendorId:X4}:{deviceId.ProductId:X4}", null),
					fallbackI2cBusProviderFeature
				)
			)
		);
	}

	private sealed class MonitorI2CBus : II2cBus
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

	private sealed class ControlLibraryDriver :
		IntelGpuDriver,
		IDeviceDriver<IGenericDeviceFeature>,
		IDeviceDriver<IDisplayAdapterDeviceFeature>,
		IDisplayAdapterI2cBusProviderFeature
	{
		private readonly IDeviceFeatureSet<IDisplayAdapterDeviceFeature> _displayAdapterFeatures;
		private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
		private readonly ControlLibrary.DisplayAdapter _gpu;

		public DeviceId DeviceId { get; }
		public override DeviceCategory DeviceCategory => DeviceCategory.GraphicsAdapter;

		IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
		IDeviceFeatureSet<IDisplayAdapterDeviceFeature> IDeviceDriver<IDisplayAdapterDeviceFeature>.Features => _displayAdapterFeatures;

		public ControlLibraryDriver
		(
			DeviceId deviceId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			ControlLibrary.DisplayAdapter gpu
		) : base(deviceId, friendlyName, configurationKey)
		{
			DeviceId = deviceId;
			_gpu = gpu;
			_displayAdapterFeatures = FeatureSet.Create<IDisplayAdapterDeviceFeature, ControlLibraryDriver, IDisplayAdapterI2cBusProviderFeature>(this);
			_genericFeatures = FeatureSet.Create<IGenericDeviceFeature, IntelGpuDriver, IDeviceIdFeature>(this);
		}

		public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

		string IDisplayAdapterI2cBusProviderFeature.DeviceName => ConfigurationKey.DeviceMainId;

		ValueTask<II2cBus> IDisplayAdapterI2cBusProviderFeature.GetBusForMonitorAsync(PnpVendorId vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken)
		{
			return ValueTask.FromException<II2cBus>(ExceptionDispatchInfo.SetCurrentStackTrace(new NotImplementedException()));
		}
	}

	private sealed class FallbackIntelGpuDriver :
		IntelGpuDriver,
		IDeviceDriver<IGenericDeviceFeature>,
		IDeviceDriver<IDisplayAdapterDeviceFeature>
	{

		private readonly IDeviceFeatureSet<IDisplayAdapterDeviceFeature> _displayAdapterFeatures;
		private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
		private readonly ControlLibrary.DisplayAdapter _gpu;

		IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
		IDeviceFeatureSet<IDisplayAdapterDeviceFeature> IDeviceDriver<IDisplayAdapterDeviceFeature>.Features => _displayAdapterFeatures;

		public FallbackIntelGpuDriver
		(
			DeviceId deviceId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			IDisplayAdapterI2cBusProviderFeature fallbackI2cBusProviderFeatureProvider
		) : base(deviceId, friendlyName, configurationKey)
		{
			_displayAdapterFeatures = FeatureSet.Create<IDisplayAdapterDeviceFeature, IDisplayAdapterI2cBusProviderFeature>(fallbackI2cBusProviderFeatureProvider);
			_genericFeatures = FeatureSet.Create<IGenericDeviceFeature, FallbackIntelGpuDriver, IDeviceIdFeature>(this);
		}

		public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	public DeviceId DeviceId { get; }
	public override DeviceCategory DeviceCategory => DeviceCategory.GraphicsAdapter;

	protected IntelGpuDriver(DeviceId deviceId, string friendlyName, DeviceConfigurationKey configurationKey) : base(friendlyName, configurationKey)
	{
		DeviceId = deviceId;
	}
}
