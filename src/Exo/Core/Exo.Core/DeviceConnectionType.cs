using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exo;

// TODO: Better organize and add more connection types.
[Flags]
public enum DeviceConnectionTypes : ulong
{
	Other = 0x0000000000000000,

	Isa = 0x0000000000000001,
	Pci = 0x0000000000000002,
	Agp = 0x0000000000000004,
	PciExpress = 0x0000000000000008,

	Firewire = 0x0000000000000100,
	Usb = 0x0000000000000200,
	Bluetooth = 0x0000000000000400,
	BluetoothLowEnergy = 0x0000000000000800,

	Monitor = 0x0000000000010000,
	Vga = 0x0000000000020000,
	Dvi = 0x0000000000040000,
	Hdmi = 0x0000000000080000,
	DisplayPort = 0x0000000000100000,

	UsbDongle = 0x0001000000000000,
	LogitechLightspeed = 0x0002000000000000,
	LogitechUnifying = 0x0004000000000000,
	LogitechBolt = 0x0008000000000000,
}
