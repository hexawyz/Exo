using System;
using System.Collections.Generic;
using System.Text;

namespace DeviceTools;

public static class DeviceClassGuids
{
	public static readonly Guid Ieee1394 = new Guid(0x6bdd1fc1, 0x810f, 0x11d0, 0xbe, 0xc7, 0x08, 0x00, 0x2b, 0xe2, 0x09, 0x2f);
	public static readonly Guid Ieee1394Debug = new Guid(0x66f250d6, 0x7801, 0x4a64, 0xb1, 0x39, 0xee, 0xa8, 0x0a, 0x45, 0x0b, 0x24);
	public static readonly Guid Iec61883 = new Guid(0x7ebefbc0, 0x3200, 0x11d2, 0xb4, 0xc2, 0x00, 0xa0, 0xc9, 0x69, 0x7d, 0x07);
	public static readonly Guid Adapter = new Guid(0x4d36e964, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid ApmSupport = new Guid(0xd45b1c18, 0xc8fa, 0x11d1, 0x9f, 0x77, 0x00, 0x00, 0xf8, 0x05, 0xf5, 0x30);
	public static readonly Guid Avc = new Guid(0xc06ff265, 0xae09, 0x48f0, 0x81, 0x2c, 0x16, 0x75, 0x3d, 0x7c, 0xba, 0x83);
	public static readonly Guid Battery = new Guid(0x72631e54, 0x78a4, 0x11d0, 0xbc, 0xf7, 0x00, 0xaa, 0x00, 0xb7, 0xb3, 0x2a);
	public static readonly Guid Biometric = new Guid(0x53d29ef7, 0x377c, 0x4d14, 0x86, 0x4b, 0xeb, 0x3a, 0x85, 0x76, 0x93, 0x59);
	public static readonly Guid Bluetooth = new Guid(0xe0cbf06c, 0xcd8b, 0x4647, 0xbb, 0x8a, 0x26, 0x3b, 0x43, 0xf0, 0xf9, 0x74);
	public static readonly Guid Camera = new Guid(0xca3e7ab9, 0xb4c3, 0x4ae6, 0x82, 0x51, 0x57, 0x9e, 0xf9, 0x33, 0x89, 0x0f);
	public static readonly Guid CdRom = new Guid(0x4d36e965, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Computer = new Guid(0x4d36e966, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Decoder = new Guid(0x6bdd1fc2, 0x810f, 0x11d0, 0xbe, 0xc7, 0x08, 0x00, 0x2b, 0xe2, 0x09, 0x2f);
	public static readonly Guid DiskDrive = new Guid(0x4d36e967, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Display = new Guid(0x4d36e968, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Dot4 = new Guid(0x48721b56, 0x6795, 0x11d2, 0xb1, 0xa8, 0x00, 0x80, 0xc7, 0x2e, 0x74, 0xa2);
	public static readonly Guid Dot4Print = new Guid(0x49ce6ac8, 0x6f86, 0x11d2, 0xb1, 0xe5, 0x00, 0x80, 0xc7, 0x2e, 0x74, 0xa2);
	public static readonly Guid EhStorageSilo = new Guid(0x9da2b80f, 0xf89f, 0x4a49, 0xa5, 0xc2, 0x51, 0x1b, 0x08, 0x5b, 0x9e, 0x8a);
	public static readonly Guid Enum1394 = new Guid(0xc459df55, 0xdb08, 0x11d1, 0xb0, 0x09, 0x00, 0xa0, 0xc9, 0x08, 0x1f, 0xf6);
	public static readonly Guid Extension = new Guid(0xe2f84ce7, 0x8efa, 0x411c, 0xaa, 0x69, 0x97, 0x45, 0x4c, 0xa4, 0xcb, 0x57);
	public static readonly Guid Fdc = new Guid(0x4d36e969, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Firmware = new Guid(0xf2e7dd72, 0x6468, 0x4e36, 0xb6, 0xf1, 0x64, 0x88, 0xf4, 0x2c, 0x1b, 0x52);
	public static readonly Guid FloppyDisk = new Guid(0x4d36e980, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Gps = new Guid(0x6bdd1fc3, 0x810f, 0x11d0, 0xbe, 0xc7, 0x08, 0x00, 0x2b, 0xe2, 0x09, 0x2f);
	public static readonly Guid Hdc = new Guid(0x4d36e96a, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid HidClass = new Guid(0x745a17a0, 0x74d3, 0x11d0, 0xb6, 0xfe, 0x00, 0xa0, 0xc9, 0x0f, 0x57, 0xda);
	public static readonly Guid Holographic = new Guid(0xd612553d, 0x06b1, 0x49ca, 0x89, 0x38, 0xe3, 0x9e, 0xf8, 0x0e, 0xb1, 0x6f);
	public static readonly Guid Image = new Guid(0x6bdd1fc6, 0x810f, 0x11d0, 0xbe, 0xc7, 0x08, 0x00, 0x2b, 0xe2, 0x09, 0x2f);
	public static readonly Guid InfiniBand = new Guid(0x30ef7132, 0xd858, 0x4a0c, 0xac, 0x24, 0xb9, 0x02, 0x8a, 0x5c, 0xca, 0x3f);
	public static readonly Guid Infrared = new Guid(0x6bdd1fc5, 0x810f, 0x11d0, 0xbe, 0xc7, 0x08, 0x00, 0x2b, 0xe2, 0x09, 0x2f);
	public static readonly Guid Keyboard = new Guid(0x4d36e96b, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid LegacyDriver = new Guid(0x8ecc055d, 0x047f, 0x11d1, 0xa5, 0x37, 0x00, 0x00, 0xf8, 0x75, 0x3e, 0xd1);
	public static readonly Guid Media = new Guid(0x4d36e96c, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid MediumChanger = new Guid(0xce5939ae, 0xebde, 0x11d0, 0xb1, 0x81, 0x00, 0x00, 0xf8, 0x75, 0x3e, 0xc4);
	public static readonly Guid Memory = new Guid(0x5099944a, 0xf6b9, 0x4057, 0xa0, 0x56, 0x8c, 0x55, 0x02, 0x28, 0x54, 0x4c);
	public static readonly Guid Modem = new Guid(0x4d36e96d, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Monitor = new Guid(0x4d36e96e, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Mouse = new Guid(0x4d36e96f, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Mtd = new Guid(0x4d36e970, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Multifunction = new Guid(0x4d36e971, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid MultiportSerial = new Guid(0x50906cb8, 0xba12, 0x11d1, 0xbf, 0x5d, 0x00, 0x00, 0xf8, 0x05, 0xf5, 0x30);
	public static readonly Guid Net = new Guid(0x4d36e972, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid NetClient = new Guid(0x4d36e973, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid NetDriver = new Guid(0x87ef9ad1, 0x8f70, 0x49ee, 0xb2, 0x15, 0xab, 0x1f, 0xca, 0xdc, 0xbe, 0x3c);
	public static readonly Guid NetService = new Guid(0x4d36e974, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid NetTrans = new Guid(0x4d36e975, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid NoDriver = new Guid(0x4d36e976, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Pcmcia = new Guid(0x4d36e977, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid PnpPrinters = new Guid(0x4658ee7e, 0xf050, 0x11d1, 0xb6, 0xbd, 0x00, 0xc0, 0x4f, 0xa3, 0x72, 0xa7);
	public static readonly Guid Ports = new Guid(0x4d36e978, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Printer = new Guid(0x4d36e979, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid PritnerUpgrade = new Guid(0x4d36e97a, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid PrintQueue = new Guid(0x1ed2bbf9, 0x11f0, 0x4084, 0xb2, 0x1f, 0xad, 0x83, 0xa8, 0xe6, 0xdc, 0xdc);
	public static readonly Guid Processor = new Guid(0x50127dc3, 0x0f36, 0x415e, 0xa6, 0xcc, 0x4c, 0xb3, 0xbe, 0x91, 0x0B, 0x65);
	public static readonly Guid Sbp2 = new Guid(0xd48179be, 0xec20, 0x11d1, 0xb6, 0xb8, 0x00, 0xc0, 0x4f, 0xa3, 0x72, 0xa7);
	public static readonly Guid StorageClassMemoryDisk = new Guid(0x53966cb1, 0x4d46, 0x4166, 0xbf, 0x23, 0xc5, 0x22, 0x40, 0x3c, 0xd4, 0x95);
	public static readonly Guid StorageClassMemoryVolume = new Guid(0x53ccb149, 0xe543, 0x4c84, 0xb6, 0xe0, 0xbc, 0xe4, 0xf6, 0xb7, 0xe8, 0x06);
	public static readonly Guid ScsiAdapter = new Guid(0x4d36e97b, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid SecurityAccelerator = new Guid(0x268c95a1, 0xedfe, 0x11d3, 0x95, 0xc3, 0x00, 0x10, 0xdc, 0x40, 0x50, 0xa5);
	public static readonly Guid Sensor = new Guid(0x5175d334, 0xc371, 0x4806, 0xb3, 0xba, 0x71, 0xfd, 0x53, 0xc9, 0x25, 0x8d);
	public static readonly Guid SideShow = new Guid(0x997b5d8d, 0xc442, 0x4f2e, 0xba, 0xf3, 0x9c, 0x8e, 0x67, 0x1e, 0x9e, 0x21);
	public static readonly Guid SmartCardReader = new Guid(0x50dd5230, 0xba8a, 0x11d1, 0xbf, 0x5d, 0x00, 0x00, 0xf8, 0x05, 0xf5, 0x30);
	public static readonly Guid SmrDisk = new Guid(0x53487c23, 0x680f, 0x4585, 0xac, 0xc3, 0x1f, 0x10, 0xd6, 0x77, 0x7e, 0x82);
	public static readonly Guid SmrVolume = new Guid(0x53b3cf03, 0x8f5a, 0x4788, 0x91, 0xb6, 0xd1, 0x9e, 0xd9, 0xfc, 0xcc, 0xbf);
	public static readonly Guid SoftwareComponent = new Guid(0x5c4c3332, 0x344d, 0x483c, 0x87, 0x39, 0x25, 0x9e, 0x93, 0x4c, 0x9c, 0xc8);
	public static readonly Guid Sound = new Guid(0x4d36e97c, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid System = new Guid(0x4d36e97d, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid TapeDrive = new Guid(0x6d807884, 0x7d21, 0x11cf, 0x80, 0x1c, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Unknown = new Guid(0x4d36e97e, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);
	public static readonly Guid Usb = new Guid(0x36fc9e60, 0xc465, 0x11cf, 0x80, 0x56, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);
	public static readonly Guid Volume = new Guid(0x71a27cdd, 0x812a, 0x11d0, 0xbe, 0xc7, 0x08, 0x00, 0x2b, 0xe2, 0x09, 0x2f);
	public static readonly Guid VolumeSnapshot = new Guid(0x533c5b84, 0xec70, 0x11d2, 0x95, 0x05, 0x00, 0xc0, 0x4f, 0x79, 0xde, 0xaf);
	public static readonly Guid WindowsCeUsbs = new Guid(0x25dbce51, 0x6c8f, 0x4a72, 0x8a, 0x6d, 0xb5, 0x4c, 0x2b, 0x4f, 0xc8, 0x35);
	public static readonly Guid Wpd = new Guid(0xeec5ad98, 0x8080, 0x425f, 0x92, 0x2a, 0xda, 0xbf, 0x3d, 0xe3, 0xf6, 0x9a);

	public static readonly Guid FileSystemFilterTop = new Guid(0xb369baf4, 0x5568, 0x4e82, 0xa8, 0x7e, 0xa9, 0x3e, 0xb1, 0x6b, 0xca, 0x87);
	public static readonly Guid FileSystemFilterActivityMonitor = new Guid(0xb86dff51, 0xa31e, 0x4bac, 0xb3, 0xcf, 0xe8, 0xcf, 0xe7, 0x5c, 0x9f, 0xc2);
	public static readonly Guid FileSystemFilterUndelete = new Guid(0xfe8f1572, 0xc67a, 0x48c0, 0xbb, 0xac, 0x0b, 0x5c, 0x6d, 0x66, 0xca, 0xfb);
	public static readonly Guid FileSystemFilterAntivirus = new Guid(0xb1d1a169, 0xc54f, 0x4379, 0x81, 0xdb, 0xbe, 0xe7, 0xd8, 0x8d, 0x74, 0x54);
	public static readonly Guid FileSystemFilterReplication = new Guid(0x48d3ebc4, 0x4cf8, 0x48ff, 0xb8, 0x69, 0x9c, 0x68, 0xad, 0x42, 0xeb, 0x9f);
	public static readonly Guid FileSystemFilterContinuousBackup = new Guid(0x71aa14f8, 0x6fad, 0x4622, 0xad, 0x77, 0x92, 0xbb, 0x9d, 0x7e, 0x69, 0x47);
	public static readonly Guid FileSystemFilterContentScreener = new Guid(0x3e3f0674, 0xc83c, 0x4558, 0xbb, 0x26, 0x98, 0x20, 0xe1, 0xeb, 0xa5, 0xc5);
	public static readonly Guid FileSystemFilterQuotaManagement = new Guid(0x8503c911, 0xa6c7, 0x4919, 0x8f, 0x79, 0x50, 0x28, 0xf5, 0x86, 0x6b, 0x0c);
	public static readonly Guid FileSystemFilterSystemRecovery = new Guid(0x2db15374, 0x706e, 0x4131, 0xa0, 0xc7, 0xd7, 0xc7, 0x8e, 0xb0, 0x28, 0x9a);
	public static readonly Guid FileSystemFilterCfsMetadataServer = new Guid(0xcdcf0939, 0xb75b, 0x4630, 0xbf, 0x76, 0x80, 0xf7, 0xba, 0x65, 0x58, 0x84);
	public static readonly Guid FileSystemFilterHsm = new Guid(0xd546500a, 0x2aeb, 0x45f6, 0x94, 0x82, 0xf4, 0xb1, 0x79, 0x9c, 0x31, 0x77);
	public static readonly Guid FileSystemFilterCompression = new Guid(0xf3586baf, 0xb5aa, 0x49b5, 0x8d, 0x6c, 0x05, 0x69, 0x28, 0x4c, 0x63, 0x9f);
	public static readonly Guid FileSystemFilterEncryption = new Guid(0xa0a701c0, 0xa511, 0x42ff, 0xaa, 0x6c, 0x06, 0xdc, 0x03, 0x95, 0x57, 0x6f);
	public static readonly Guid FileSystemFilterVirtualization = new Guid(0xf75a86c0, 0x10d8, 0x4c3a, 0xb2, 0x33, 0xed, 0x60, 0xe4, 0xcd, 0xfa, 0xac);
	public static readonly Guid FileSystemFilterPhysicalQuotaManagement = new Guid(0x6a0a8e78, 0xbba6, 0x4fc4, 0xa7, 0x09, 0x1e, 0x33, 0xcd, 0x09, 0xd6, 0x7e);
	public static readonly Guid FileSystemFilterOpenFileBackup = new Guid(0xf8ecafa6, 0x66d1, 0x41a5, 0x89, 0x9b, 0x66, 0x58, 0x5d, 0x72, 0x16, 0xb7);
	public static readonly Guid FileSystemFilterSecurityEnhancer = new Guid(0xd02bc3da, 0x0c8e, 0x4945, 0x9b, 0xd5, 0xf1, 0x88, 0x3c, 0x22, 0x6c, 0x8c);
	public static readonly Guid FileSystemFilterCopyProtection = new Guid(0x89786ff1, 0x9c12, 0x402f, 0x9c, 0x9e, 0x17, 0x75, 0x3c, 0x7f, 0x43, 0x75);
	public static readonly Guid FileSystemFilterBottom = new Guid(0x37765ea0, 0x5958, 0x4fc9, 0xb0, 0x4b, 0x2f, 0xdf, 0xef, 0x97, 0xe5, 0x9e);
	public static readonly Guid FileSystemFilterSystem = new Guid(0x5d1b9aaa, 0x01e2, 0x46af, 0x84, 0x9f, 0x27, 0x2b, 0x3f, 0x32, 0x4c, 0x46);
	public static readonly Guid FileSystemFilterInfrastructure = new Guid(0xe55fa6f9, 0x128c, 0x4d04, 0xab, 0xab, 0x63, 0x0c, 0x74, 0xb1, 0x45, 0x3a);
}
