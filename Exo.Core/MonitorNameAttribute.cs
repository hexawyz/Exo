using DeviceTools;
using DeviceTools.DisplayDevices.Configuration;

namespace Exo;

[AttributeUsage(AttributeTargets.Class)]
public sealed class MonitorNameAttribute : Attribute
{
	public MonitorName MonitorName { get; }

	public MonitorNameAttribute(string monitorName) => MonitorName = MonitorName.Parse(monitorName);
	public MonitorNameAttribute(string vendorId, ushort productCodeId) => MonitorName = new MonitorName(PnpVendorId.Parse(vendorId), productCodeId);
}
