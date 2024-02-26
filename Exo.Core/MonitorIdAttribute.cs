using DeviceTools;
using DeviceTools.DisplayDevices.Configuration;

namespace Exo;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MonitorIdAttribute : Attribute
{
	public MonitorId MonitorId { get; }

	public MonitorIdAttribute(string monitorName) => MonitorId = MonitorId.Parse(monitorName);
	public MonitorIdAttribute(string vendorId, ushort productCodeId) => MonitorId = new MonitorId(PnpVendorId.Parse(vendorId), productCodeId);
}
