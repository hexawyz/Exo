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
		DeviceObjectInformation topLevelDevice,
		DeviceId deviceId,
		ILogger<NVidiaGpuDriver> logger
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

		// Initialize the API and log the version.
		logger.NvApiVersion(NvApi.GetInterfaceVersionString());

		NvApi.PhysicalGpu foundGpu = default;

		// Enumerate all the GPUs and find the right one.
		foreach (var gpu in NvApi.GetPhysicalGpus())
		{
			if (gpu.GetBusId() != busNumber || gpu.GetBusSlotId() != pciAddress >> 16) continue;

			foundGpu = gpu;
		}

		if (!foundGpu.IsValid)
		{
			throw new InvalidOperationException($"Could not find the corresponding GPU through NVAPI for {topLevelDevice.Id}.");
		}

		string friendlyName = foundGpu.GetFullName();

		var devices = foundGpu.GetIlluminationDevices();
		var deviceControls = foundGpu.GetIlluminationDeviceControls();
		var zones = foundGpu.GetIlluminationZones();
		var zoneControls = foundGpu.GetIlluminationZoneControls();

		return new(new DriverCreationResult<SystemDevicePath>(keys, new NVidiaGpuDriver(deviceId, friendlyName, new("nv", topLevelDevice.Id, $"{NVidiaVendorId:X4}:{deviceId.ProductId:X4}", null))));
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
