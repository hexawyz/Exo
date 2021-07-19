using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceTools.SystemControl
{
	public abstract class DdcMonitorDriver : MonitorDriver
	{
		protected DdcMonitorDriver(long adapterId, long id, string deviceName)
		{
		}
	}
}
