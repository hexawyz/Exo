using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Configuration;
using DeviceTools.DisplayDevices.Mccs;

namespace Exo.Cli
{
	internal static class Program
	{
		private enum InputName
		{
			MiniDisplayPort = 21,
			DisplayPort = InputSource.DisplayPort1,
			Hdmi1 = InputSource.Hdmi1,
			Hdmi2 = InputSource.Hdmi2,
			TypeC = 23,
		}

		private static void Main(string? monitorName = null, InputName? changeSource = null)
		{
			if (monitorName is null || changeSource is null) return;

			var parsedMonitorName = MonitorName.Parse(monitorName);

			var displayConfiguration = DisplayConfiguration.GetForActivePaths();

			foreach (var logicalMonitor in LogicalMonitor.GetAll())
			{
				DisplayConfigurationPathSourceInfo? foundSource = null;
				int currentPathIndex;

				for (currentPathIndex = 0; currentPathIndex < displayConfiguration.Paths.Count; currentPathIndex++)
				{
					var path = displayConfiguration.Paths[currentPathIndex];
					if (path.SourceInfo.GetDeviceName() == logicalMonitor.GetMonitorInformation().DeviceName)
					{
						foundSource = path.SourceInfo;

						break;
					}
				}

				if (foundSource is null) continue;

				// This code assumes that one "source" is one logical monitor, and one "target" is one physical monitor. (i.e. there are the same number and they are ordered the same)
				// It is a reasonable assumption to make since EnumDisplayMonitors is supposed to report monitors in logical, i.e. configuration, order.
				// See https://stackoverflow.com/questions/27042576/enumdisplaydevices-vs-enumdisplaymonitors answer by Hans Passant.

				// Note that this is not enough, because the two APIs are queried separately, and configuration can change in-between.
				// As such, we should handle the (hopefully rare) case where both APIs return mismatching results.

				var physicalMonitors = logicalMonitor.GetPhysicalMonitors();
				for (int i = 0; i < physicalMonitors.Length; i++)
				{
					var physicalMonitor = physicalMonitors[i];

					var path = displayConfiguration.Paths[currentPathIndex];

					var targetNameInformation = path.TargetInfo.GetDeviceNameInformation();

					if (targetNameInformation.IsEdidValid && targetNameInformation.EdidVendorId == parsedMonitorName.VendorId && targetNameInformation.EdidProductId == parsedMonitorName.ProductId)
					{
						physicalMonitor.SetVcpFeature((byte)VcpCode.InputSelect, (uint)changeSource);
						break;
					}

					// Advance in the path collection before looping.
					if (++currentPathIndex >= displayConfiguration.Paths.Count || path.SourceInfo != foundSource.GetValueOrDefault())
					{
						break;
					}
				}
			}
		}
	}
}
