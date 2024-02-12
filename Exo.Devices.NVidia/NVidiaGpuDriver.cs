using System.Collections.Immutable;
using DeviceTools;
using Exo.Discovery;
using Exo.Features;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.NVidia;

public class NVidiaGpuDriver : Driver, IDeviceIdFeature
{
	private const ushort NVidiaVendorId = 0x10DE;

	[DiscoverySubsystem<PciDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.DisplayAdapter)]
	[DeviceInterfaceClass(DeviceInterfaceClass.DisplayDeviceArrival)]
	[ProductId(VendorIdSource.Pci, NVidiaVendorId, 0x2204)]
	public static ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		ImmutableArray<DeviceObjectInformation> devices,
		string topLevelDeviceName,
		DeviceId deviceId,
		ILogger<NVidiaGpuDriver> logger
	)
	{
		string friendlyName = deviceId.ProductId switch
		{
			0x2204 => "NVIDIA GeForce RTX 3090",
			_ => "NVIDIA GPU",
		};

		logger.NvApiVersion(NvApi.GetInterfaceVersionString());

		return new(new DriverCreationResult<SystemDevicePath>(keys, new NVidiaGpuDriver(deviceId, friendlyName, new("nv", topLevelDeviceName, $"{NVidiaVendorId:X4}:{deviceId.ProductId:X4}", null))));
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.GraphicsAdapter;
	public DeviceId DeviceId { get; }
	public override IDeviceFeatureCollection<IDeviceFeature> Features { get; }

	public NVidiaGpuDriver(DeviceId deviceId, string friendlyName, DeviceConfigurationKey configurationKey) : base(friendlyName, configurationKey)
	{
		DeviceId = deviceId;
		Features = FeatureCollection.Create<IDeviceFeature, IDeviceIdFeature>(this);
	}

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

}
