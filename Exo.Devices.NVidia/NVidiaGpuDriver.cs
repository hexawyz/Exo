using System.Collections.Immutable;
using DeviceTools;
using Exo.Discovery;

namespace Exo.Devices.NVidia;

public class NVidiaGpuDriver : Driver
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
		ushort productId
	)
	{
		string friendlyName = productId switch
		{
			0x2204 => "NVIDIA GeForce RTX 3090",
			_ => "NVIDIA GPU",
		};

		return new(new DriverCreationResult<SystemDevicePath>(keys, new NVidiaGpuDriver(friendlyName, new("nv", topLevelDeviceName, $"{NVidiaVendorId:X4}:{productId:X4}", null))));
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.GraphicsAdapter;
	public override IDeviceFeatureCollection<IDeviceFeature> Features { get; }

	public NVidiaGpuDriver(string friendlyName, DeviceConfigurationKey configurationKey) : base(friendlyName, configurationKey)
	{
		Features = FeatureCollection.Empty<IDeviceFeature>();
	}

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
