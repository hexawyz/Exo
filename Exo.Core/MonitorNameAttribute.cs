using System;
using DeviceTools.DisplayDevices.Configuration;

namespace Exo.Core
{
	[AttributeUsage(AttributeTargets.Class)]
	public sealed class MonitorNameAttribute : Attribute
	{
		public MonitorName MonitorName { get; }

		public MonitorNameAttribute(string monitorName) => MonitorName = MonitorName.Parse(monitorName);
		public MonitorNameAttribute(string manucafturerNameId, ushort productCodeId) => MonitorName = new MonitorName(EdidManufacturerNameId.Parse(manucafturerNameId), productCodeId);
	}
}
