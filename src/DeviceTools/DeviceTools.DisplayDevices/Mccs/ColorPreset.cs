using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceTools.DisplayDevices.Mccs
{
	public enum ColorPreset : byte
	{
		Reserved = 0x00,
		Srgb = 0x01,
		Native = 0x02,
		Warmer = 0x03,
		Temperature4000 = 0x03,
		Temperature5000 = 0x04,
		Temperature6500 = 0x05,
		Temperature7500 = 0x06,
		Temperature8200 = 0x07,
		Temperature9300 = 0x08,
		Temperature10000 = 0x09,
		Cooler = 0x0A,
		Temperature11500 = 0x0A,
		User1 = 0x0B,
		User2 = 0x0C,
		User3 = 0x0D,
	}
}
