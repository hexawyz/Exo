namespace Exo.Devices.Lg.Monitors;

[TypeId(0x33A7CF4B, 0x5468, 0x4C9D, 0x8E, 0x83, 0xA7, 0x30, 0x69, 0x41, 0x9B, 0x96)]
public interface ILgMonitorDeviceFeature : IDeviceFeature
{
}

public interface ILgMonitorNxpVersionFeature : ILgMonitorDeviceFeature
{
	public SimpleVersion FirmwareNxpVersion { get; }
}

public interface ILgMonitorScalerVersionFeature : ILgMonitorDeviceFeature
{
	public SimpleVersion FirmwareScalerVersion { get; }
}

public interface ILgMonitorDisplayStreamCompressionVersionFeature : ILgMonitorDeviceFeature
{
	public SimpleVersion FirmwareDisplayStreamCompressionVersion { get; }
}
