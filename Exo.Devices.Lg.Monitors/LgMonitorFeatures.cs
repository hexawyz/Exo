using Exo.Features.MonitorFeatures;

namespace Exo.Devices.Lg.Monitors;

public interface ILgMonitorDeviceFeature : IMonitorDeviceFeature
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
