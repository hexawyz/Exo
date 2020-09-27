using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceTools.DisplayDevices.Mccs
{
	public readonly struct VcpFeatureReply
	{
		public VcpFeatureReply(uint currentValue, uint maximumValue)
		{
			CurrentValue = currentValue;
			MaximumValue = maximumValue;
		}

		public uint CurrentValue { get; }
		public uint MaximumValue { get; }
	}
}
