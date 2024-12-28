using System.Runtime.CompilerServices;

namespace DeviceTools;

/// <summary>GUIDs used for device interface classes.</summary>
/// <remarks>
/// <para>
/// Use these GUIDs to refer to device interface classes, and not to devices themselves. (A device can expose multiple interfaces, basically)
/// </para>
/// <para>
/// Some of these GUIDs may have the same name as ones in <see cref="DeviceClassGuids"/>, but they should still hold a different value.
/// </para>
/// </remarks>
public static class DeviceInterfaceClassGuids
{
	public static readonly Guid Parallel = new(0x97F76EF0, 0xF883, 0x11D0, 0xAF, 0x1F, 0x00, 0x00, 0xF8, 0x00, 0x84, 0x5C);
	public static readonly Guid ParallelClass = new(0x811FC6A5, 0xF728, 0x11D0, 0xA5, 0x37, 0x00, 0x00, 0xF8, 0x75, 0x3E, 0xD1);
	public static readonly Guid UsbHub = new(0xF18A0E88, 0xC30C, 0x11D0, 0x88, 0x15, 0x00, 0xA0, 0xC9, 0x06, 0xBE, 0xD8);
	public static readonly Guid UsbBillboard = new(0x5E9ADAEF, 0xF879, 0x473F, 0xB8, 0x07, 0x4E, 0x5E, 0xA7, 0x7D, 0x1B, 0x1C);
	public static readonly Guid UsbDevice = new(0xA5DCBF10, 0x6530, 0x11D2, 0x90, 0x1F, 0x00, 0xC0, 0x4F, 0xB9, 0x51, 0xED);
	public static readonly Guid UsbHostController = new(0x3ABF6F2D, 0x71C4, 0x462A, 0x8A, 0x92, 0x1E, 0x68, 0x61, 0xE6, 0xAF, 0x27);
	public static readonly Guid Keyboard = new(0x884B96C3, 0x56EF, 0x11D1, 0xBC, 0x8C, 0x00, 0xA0, 0xC9, 0x14, 0x05, 0xDD);
	public static readonly Guid Mouse = new(0x378DE44C, 0x56EF, 0x11D1, 0xBC, 0x8C, 0x00, 0xA0, 0xC9, 0x14, 0x05, 0xDD);
	public static readonly Guid Hid = new(0x4D1E55B2, 0xF16F, 0x11CF, 0x88, 0xCB, 0x00, 0x11, 0x11, 0x00, 0x00, 0x30);
	public static readonly Guid DisplayOutputInterfaceStandard = new(0x96304D9F, 0x54b5, 0x11d1, 0x8b, 0x0f, 0x00, 0xa0, 0xc9, 0x06, 0x8f, 0xf3);
	public static readonly Guid DisplayAdapter = new(0x5B45201D, 0xF2F2, 0x4F3B, 0x85, 0xBB, 0x30, 0xFF, 0x1F, 0x95, 0x35, 0x99);
	public static readonly Guid Monitor = new(0xE6F07B5F, 0xEE97, 0x4A90, 0xB0, 0x76, 0x33, 0xF5, 0x7B, 0xF4, 0xEA, 0xA7);
	public static readonly Guid DisplayDeviceArrival = new(0x1CA05180, 0xA699, 0x450A, 0x9A, 0x0C, 0xDE, 0x4F, 0xBE, 0x3D, 0xDD, 0x89);
	public static readonly Guid VideoOutputArrival = new(0x1AD9E4F0, 0xF88D, 0x4360, 0xBA, 0xB9, 0x4C, 0x2D, 0x55, 0xE5, 0x64, 0xCD);
	public static readonly Guid Disk = new(0x53F56307, 0xB6BF, 0x11D0, 0x94, 0xF2, 0x00, 0xA0, 0xC9, 0x1E, 0xFB, 0x8B);
	public static readonly Guid CdRom = new(0x53F56308, 0xB6BF, 0x11D0, 0x94, 0xF2, 0x00, 0xA0, 0xC9, 0x1E, 0xFB, 0x8B);
	public static readonly Guid Partition = new(0x53F5630A, 0xB6BF, 0x11D0, 0x94, 0xF2, 0x00, 0xA0, 0xC9, 0x1E, 0xFB, 0x8B);
	public static readonly Guid Tape = new(0x53F5630B, 0xB6BF, 0x11D0, 0x94, 0xF2, 0x00, 0xA0, 0xC9, 0x1E, 0xFB, 0x8B);
	public static readonly Guid WriteOnceDisk = new(0x53F5630C, 0xB6BF, 0x11D0, 0x94, 0xF2, 0x00, 0xA0, 0xC9, 0x1E, 0xFB, 0x8B);
	public static readonly Guid Volume = new(0x53F5630D, 0xB6BF, 0x11D0, 0x94, 0xF2, 0x00, 0xA0, 0xC9, 0x1E, 0xFB, 0x8B);
	public static readonly Guid MediumChanger = new(0x53F56310, 0xB6BF, 0x11D0, 0x94, 0xF2, 0x00, 0xA0, 0xC9, 0x1E, 0xFB, 0x8B);
	public static readonly Guid Floppy = new(0x53F56311, 0xB6BF, 0x11D0, 0x94, 0xF2, 0x00, 0xA0, 0xC9, 0x1E, 0xFB, 0x8B);
	public static readonly Guid CdChanger = new(0x53F56312, 0xB6BF, 0x11D0, 0x94, 0xF2, 0x00, 0xA0, 0xC9, 0x1E, 0xFB, 0x8B);
	public static readonly Guid StoragePort = new(0x2ACCFE60, 0xC130, 0x11D2, 0xB0, 0x82, 0x00, 0xA0, 0xC9, 0x1E, 0xFB, 0x8B);
	public static readonly Guid VmLun = new(0x6F416619, 0x9F29, 0x42A5, 0xB2, 0x0B, 0x37, 0xE2, 0x19, 0xCA, 0x02, 0xB0);
	public static readonly Guid Ses = new(0x1790C9EC, 0x47D5, 0x4DF3, 0xB5, 0xAF, 0x9A, 0xDF, 0x3C, 0xF2, 0x3E, 0x48);
	public static readonly Guid ServiceVolume = new(0x6EAD3D82, 0x25EC, 0x46BC, 0xB7, 0xFD, 0xC1, 0xF0, 0xDF, 0x8F, 0x50, 0x37);
	public static readonly Guid HiddenVolume = new(0x7F108A28, 0x9833, 0x4B3B, 0xB7, 0x80, 0x2C, 0x6B, 0x5F, 0xA5, 0xC0, 0x62);
	public static readonly Guid UnifiedAccessRpmb = new(0x27447C21, 0xBCC3, 0x4D07, 0xA0, 0x5B, 0xA3, 0x39, 0x5B, 0xB4, 0xEE, 0xE7);
	public static readonly Guid ComPort = new(0x86E0D1E0, 0x8089, 0x11D0, 0x9C, 0xE4, 0x08, 0x00, 0x3E, 0x30, 0x1F, 0x73);
	public static readonly Guid SerialAndParallelPort = new(0x4D36E978, 0xE325, 0x11CE, 0xBF, 0xC1, 0x08, 0x00, 0x2B, 0xE1, 0x03, 0x18);
	public static readonly Guid Modem = new(0x2C7089AA, 0x2E0E, 0x11D1, 0xB1, 0x14, 0x00, 0xC0, 0x4F, 0xC2, 0xAA, 0xE4);
	public static readonly Guid Net = new(0xcac88484, 0x7515, 0x4c03, 0x82, 0xe6, 0x71, 0xa8, 0x7a, 0xba, 0xc3, 0x61);
	public static readonly Guid I2C = new(0x2564AA4F, 0xDDDB, 0x4495, 0xB4, 0x97, 0x6A, 0xD4, 0xA8, 0x41, 0x63, 0xD7);
	public static readonly Guid Opm = new(0xBF4672DE, 0x6B4E, 0x4BE4, 0xA3, 0x25, 0x68, 0xA9, 0x1E, 0xA4, 0x9C, 0x09);
	public static readonly Guid Opm2Jtp = new(0xE929EEA4, 0xB9F1, 0x407B, 0xAA, 0xB9, 0xAB, 0x08, 0xBB, 0x44, 0xFB, 0xF4);
	public static readonly Guid Opm2 = new(0x7F098726, 0x2EBB, 0x4FF3, 0xA2, 0x7F, 0x10, 0x46, 0xB9, 0x5D, 0xC5, 0x17);
	public static readonly Guid Opm3 = new(0x693a2cb1, 0x8c8d, 0x4ab6, 0x95, 0x55, 0x4b, 0x85, 0xef, 0x2c, 0x7c, 0x6b);
	public static readonly Guid Brightness = new(0xFDE5BBA4, 0xB3F9, 0x46FB, 0xBD, 0xAA, 0x07, 0x28, 0xCE, 0x31, 0x00, 0xB4);
	public static readonly Guid Brightness2 = new(0x148A3C98, 0x0ECD, 0x465A, 0xB6, 0x34, 0xB0, 0x5F, 0x19, 0x5F, 0x77, 0x39);
	public static readonly Guid MiracastDisplay = new(0xaf03f190, 0x22af, 0x48cb, 0x94, 0xbb, 0xb7, 0x8e, 0x76, 0xa2, 0x51, 0x7);
	public static readonly Guid Image = new(0x6bdd1fc6, 0x810f, 0x11d0, 0xbe, 0xc7, 0x08, 0x00, 0x2b, 0xe2, 0x09, 0x2f);
	public static readonly Guid SideShow = new(0x152e5811, 0xfeb9, 0x4b00, 0x90, 0xf4, 0xd3, 0x29, 0x47, 0xae, 0x16, 0x81);
	public static readonly Guid Wpd = new(0x6AC27878, 0xA6FA, 0x4155, 0xBA, 0x85, 0xF9, 0x8F, 0x49, 0x1D, 0x4F, 0x33);
	public static readonly Guid WpdPrivate = new(0xBA0C718F, 0x4DED, 0x49B7, 0xBD, 0xD3, 0xFA, 0xBE, 0x28, 0x66, 0x12, 0x11);
	public static readonly Guid WpdService = new(0x9EF44F80, 0x3D64, 0x4246, 0xA6, 0xAA, 0x20, 0x6F, 0x32, 0x8D, 0x1E, 0xDC);
	public static readonly Guid Battery = new(0x72631e54, 0x78A4, 0x11d0, 0xbc, 0xf7, 0x00, 0xaa, 0x00, 0xb7, 0xb3, 0x2a);
	public static readonly Guid PowerMeter = new(0xe849804e, 0xc719, 0x43d8, 0xac, 0x88, 0x96, 0xb8, 0x94, 0xc1, 0x91, 0xe2);
	public static readonly Guid EnergyMeter = new(0x45bd8344, 0x7ed6, 0x49cf, 0xa4, 0x40, 0xc2, 0x76, 0xc9, 0x33, 0xb0, 0x53);
	public static readonly Guid ApplicationLaunchButton = new(0x629758ee, 0x986e, 0x4d9e, 0x8e, 0x47, 0xde, 0x27, 0xf8, 0xab, 0x05, 0x4d);
	public static readonly Guid SystemButton = new(0x4AFA3D53, 0x74A7, 0x11d0, 0xbe, 0x5e, 0x00, 0xA0, 0xC9, 0x06, 0x28, 0x57);
	public static readonly Guid Lid = new(0x4AFA3D52, 0x74A7, 0x11d0, 0xbe, 0x5e, 0x00, 0xA0, 0xC9, 0x06, 0x28, 0x57);
	public static readonly Guid ThermalZone = new(0x4AFA3D51, 0x74A7, 0x11d0, 0xbe, 0x5e, 0x00, 0xA0, 0xC9, 0x06, 0x28, 0x57);
	public static readonly Guid Fan = new(0x05ecd13d, 0x81da, 0x4a2a, 0x8a, 0x4c, 0x52, 0x4f, 0x23, 0xdd, 0x4d, 0xc9);
	public static readonly Guid Processor = new(0x97fadb10, 0x4e33, 0x40ae, 0x35, 0x9c, 0x8b, 0xef, 0x02, 0x9d, 0xbd, 0xd0);
	public static readonly Guid Memory = new(0x3fd0f03d, 0x92e0, 0x45fb, 0xb7, 0x5c, 0x5e, 0xd8, 0xff, 0xb0, 0x10, 0x21);
	public static readonly Guid AcpiTime = new(0x97f99bf6, 0x4497, 0x4f18, 0xbb, 0x22, 0x4b, 0x9f, 0xb2, 0xfb, 0xef, 0x9c);
	public static readonly Guid MessageIndicator = new(0XCD48A365, 0xfa94, 0x4ce2, 0xa2, 0x32, 0xa1, 0xb7, 0x64, 0xe5, 0xd8, 0xb4);
	public static readonly Guid ThermalCooling = new(0xdbe4373d, 0x3c81, 0x40cb, 0xac, 0xe4, 0xe0, 0xe5, 0xd0, 0x5f, 0xc, 0x9f);
	public static readonly Guid VirtualAudioVideoControl = new(0x616ef4d0, 0x23ce, 0x446d, 0xa5, 0x68, 0xc3, 0x1e, 0xb0, 0x19, 0x13, 0xd0);
	public static readonly Guid AudioVideoControl = new(0x095780c3, 0x48a1, 0x4570, 0xbd, 0x95, 0x46, 0x70, 0x7f, 0x78, 0xc2, 0xdc);
	/// <summary>Bluetooth Radios</summary>
	public static readonly Guid BluetoothPort = new(0x850302a, 0xb344, 0x4fda, 0x9b, 0xe9, 0x90, 0x57, 0x6b, 0x8d, 0x46, 0xf0);
	/// <summary>Bluetooth RFCOMM.</summary>
	public static readonly Guid BluetoothRfcommService = new(0xb142fc3e, 0xfa4e, 0x460b, 0x8a, 0xbc, 0x07, 0x2b, 0x62, 0x8b, 0x3c, 0x70);
	/// <summary>Bluetooth.</summary>
	public static readonly Guid Bluetooth = new(0x00F40965, 0xE89D, 0x4487, 0x98, 0x90, 0x87, 0xC3, 0xAB, 0xB2, 0x11, 0xF4);
	/// <summary>Bluetooth LE.</summary>
	public static readonly Guid BluetoothLe = new(0x781aee18, 0x7733, 0x4ce4, 0xad, 0xd0, 0x91, 0xf4, 0x1c, 0x67, 0xb5, 0x92);
	/// <summary>Bluetooth LE Service.</summary>
	public static readonly Guid BluetoothGattService = new(0x6e3bb679, 0x4372, 0x40c8, 0x9e, 0xaa, 0x45, 0x09, 0xdf, 0x26, 0x0c, 0xd8);

	public static readonly Guid Thunderbolt = new(0xb101923a, 0xe86e, 0x4f98, 0xb2, 0x2f, 0x84, 0x36, 0x0f, 0x2e, 0xa5, 0xb7);
	public static readonly Guid Printer = new(0x0ecef634, 0x6ef0, 0x472a, 0x80, 0x85, 0x5a, 0xd0, 0x23, 0xec, 0xbc, 0xcd);

	/// <summary>WinUSB Device Interface.</summary>
	public static readonly Guid WinUsb = new(0xDEE824EF, 0x729B, 0x4A0E, 0x9C, 0x14, 0xB7, 0x11, 0x7D, 0x33, 0xA8, 0x17);

	public static class NetworkDriverInterfaceSpecification
	{
		public static readonly Guid Lan = new(0xad498944, 0x762f, 0x11d0, 0x8d, 0xcb, 0x00, 0xc0, 0x4f, 0xc3, 0x35, 0x8c);
	}

	public static class KernelStreaming
	{
		public static readonly Guid Bridge = new(0x085AFF00, 0x62CE, 0x11CF, 0xA5, 0xD6, 0x28, 0xDB, 0x04, 0xC1, 0x00, 0x00);
		public static readonly Guid Capture = new(0x65E8773D, 0x8F56, 0x11D0, 0xA3, 0xB9, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid VideoCamera = new(0xe5323777, 0xf976, 0x4f5b, 0x9b, 0x55, 0xb9, 0x46, 0x99, 0xc4, 0x6e, 0x44);
		public static readonly Guid SensorCamera = new(0x24e552d7, 0x6523, 0x47f7, 0xa6, 0x47, 0xd3, 0x46, 0x5b, 0xf1, 0xf5, 0xca);
		public static readonly Guid SensorGroup = new(0x669C7214, 0x0A88, 0x4311, 0xA7, 0xF3, 0x4E, 0x79, 0x82, 0x0E, 0x33, 0xBD);
		public static readonly Guid Render = new(0x65E8773E, 0x8F56, 0x11D0, 0xA3, 0xB9, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid Mixer = new(0xAD809C00, 0x7B88, 0x11D0, 0xA5, 0xD6, 0x28, 0xDB, 0x04, 0xC1, 0x00, 0x00);
		public static readonly Guid Splitter = new(0x0A4252A0, 0x7E70, 0x11D0, 0xA5, 0xD6, 0x28, 0xDB, 0x04, 0xC1, 0x00, 0x00);
		public static readonly Guid DataCompressor = new(0x1E84C900, 0x7E70, 0x11D0, 0xA5, 0xD6, 0x28, 0xDB, 0x04, 0xC1, 0x00, 0x00);
		public static readonly Guid DataDecompressor = new(0x2721AE20, 0x7E70, 0x11D0, 0xA5, 0xD6, 0x28, 0xDB, 0x04, 0xC1, 0x00, 0x00);
		public static readonly Guid DataTransform = new(0x2EB07EA0, 0x7E70, 0x11D0, 0xA5, 0xD6, 0x28, 0xDB, 0x04, 0xC1, 0x00, 0x00);

		public static readonly Guid MediaFoundationTransformVideoDecoder = new(0xd6c02d4b, 0x6833, 0x45b4, 0x97, 0x1a, 0x05, 0xa4, 0xb0, 0x4b, 0xab, 0x91);
		public static readonly Guid MediaFoundationTransformVideoEncoder = new(0xf79eac7d, 0xe545, 0x4387, 0xbd, 0xee, 0xd6, 0x47, 0xd7, 0xbd, 0xe4, 0x2a);
		public static readonly Guid MediaFoundationTransformVideoEffect = new(0x12e17c21, 0x532c, 0x4a6e, 0x8a, 0x1c, 0x40, 0x82, 0x5a, 0x73, 0x63, 0x97);
		public static readonly Guid MediaFoundationTransformMultiplexer = new(0x059c561e, 0x05ae, 0x4b61, 0xb6, 0x9d, 0x55, 0xb6, 0x1e, 0xe5, 0x4a, 0x7b);
		public static readonly Guid MediaFoundationTransformDemultiplexer = new(0xa8700a7a, 0x939b, 0x44c5, 0x99, 0xd7, 0x76, 0x22, 0x6b, 0x23, 0xb3, 0xf1);
		public static readonly Guid MediaFoundationTransformAudioDecoder = new(0x9ea73fb4, 0xef7a, 0x4559, 0x8d, 0x5d, 0x71, 0x9d, 0x8f, 0x04, 0x26, 0xc7);
		public static readonly Guid MediaFoundationTransformAudioEncoder = new(0x91c64bd0, 0xf91e, 0x4d8c, 0x92, 0x76, 0xdb, 0x24, 0x82, 0x79, 0xd9, 0x75);
		public static readonly Guid MediaFoundationTransformAudioEffect = new(0x11064c48, 0x3648, 0x4ed0, 0x93, 0x2e, 0x05, 0xce, 0x8a, 0xc8, 0x11, 0xb7);
		public static readonly Guid MediaFoundationTransformVideoProcessor = new(0x302ea3fc, 0xaa5f, 0x47f9, 0x9f, 0x7a, 0xc2, 0x18, 0x8b, 0xb1, 0x63, 0x2);
		public static readonly Guid MediaFoundationTransformOther = new(0x90175d57, 0xb7ea, 0x4901, 0xae, 0xb3, 0x93, 0x3a, 0x87, 0x47, 0x75, 0x6f);

		public static readonly Guid CommunicationsTransform = new(0xCF1DDA2C, 0x9743, 0x11D0, 0xA3, 0xEE, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid InterfaceTransform = new(0xCF1DDA2D, 0x9743, 0x11D0, 0xA3, 0xEE, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid MediumTransform = new(0xCF1DDA2E, 0x9743, 0x11D0, 0xA3, 0xEE, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid FileSystem = new(0x760FED5E, 0x9357, 0x11D0, 0xA3, 0xCC, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid Clock = new(0x53172480, 0x4791, 0x11D0, 0xa5, 0xd6, 0x28, 0xdb, 0x04, 0xc1, 0x00, 0x00);
		public static readonly Guid Proxy = new(0x97EBAACA, 0x95BD, 0x11D0, 0xA3, 0xEA, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid Quality = new(0x97EBAACB, 0x95BD, 0x11D0, 0xA3, 0xEA, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);

		public static readonly Guid Audio = new(0x6994AD04, 0x93EF, 0x11D0, 0xA3, 0xCC, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid Video = new(0x6994AD05, 0x93EF, 0x11D0, 0xA3, 0xCC, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid RealTime = new(0xEB115FFC, 0x10C8, 0x4964, 0x83, 0x1D, 0x6D, 0xCB, 0x02, 0xE6, 0xF2, 0x3F);
		public static readonly Guid Text = new(0x6994AD06, 0x93EF, 0x11D0, 0xA3, 0xCC, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid Network = new(0x67C9CC3C, 0x69C4, 0x11D2, 0x87, 0x59, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid Topology = new(0xdda54a40, 0x1e4c, 0x11d1, 0xa0, 0x50, 0x40, 0x57, 0x05, 0xc1, 0x00, 0x00);
		public static readonly Guid Virtual = new(0x3503EAC4, 0x1F26, 0x11D1, 0x8A, 0xB0, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid AcousticEchoCancel = new(0xBF963D80, 0xC559, 0x11D0, 0x8A, 0x2B, 0x00, 0xA0, 0xC9, 0x25, 0x5A, 0xC1);

		public static readonly Guid SystemAudio = new(0xA7C7A5B1, 0x5AF3, 0x11D1, 0x9C, 0xED, 0x00, 0xA0, 0x24, 0xBF, 0x04, 0x07);
		public static readonly Guid WindowsDriverModelAudio = new(0x3E227E76, 0x690D, 0x11D2, 0x81, 0x61, 0x00, 0x00, 0xF8, 0x77, 0x5B, 0xF1);

		public static readonly Guid AudioGlobalEffects = new(0x9BAF9572, 0x340C, 0x11D3, 0xAB, 0xDC, 0x00, 0xA0, 0xC9, 0x0A, 0xB1, 0x6F);
		public static readonly Guid AudioSplitter = new(0x9EA331FA, 0xB91B, 0x45F8, 0x92, 0x85, 0xBD, 0x2B, 0xC7, 0x7A, 0xFC, 0xDE);

		public static readonly Guid Synthesizer = new(0xDFF220F3, 0xF70F, 0x11D0, 0xB9, 0x17, 0x00, 0xA0, 0xC9, 0x22, 0x31, 0x96);
		public static readonly Guid DrmDescramble = new(0xFFBB6E3F, 0xCCFE, 0x4D84, 0x90, 0xD9, 0x42, 0x14, 0x18, 0xB0, 0x3A, 0x8E);

		public static readonly Guid AudioDevice = new(0xFBF6F530, 0x07B9, 0x11D2, 0xA7, 0x1E, 0x00, 0x00, 0xF8, 0x00, 0x47, 0x88);

		public static readonly Guid PreferredWaveOutDevice = new(0xD6C5066E, 0x72C1, 0x11D2, 0x97, 0x55, 0x00, 0x00, 0xF8, 0x00, 0x47, 0x88);
		public static readonly Guid PreferredWaveInDevice = new(0xD6C50671, 0x72C1, 0x11D2, 0x97, 0x55, 0x00, 0x00, 0xF8, 0x00, 0x47, 0x88);
		public static readonly Guid PreferredMidiOutDevice = new(0xD6C50674, 0x72C1, 0x11D2, 0x97, 0x55, 0x00, 0x00, 0xF8, 0x00, 0x47, 0x88);
		public static readonly Guid WindowsDriverModelAudioUsePinName = new(0x47A4FA20, 0xA251, 0x11D1, 0xA0, 0x50, 0x00, 0x00, 0xF8, 0x00, 0x47, 0x88);
		public static readonly Guid EscalantePlatformDriver = new(0x74f3aea8, 0x9768, 0x11d1, 0x8e, 0x07, 0x00, 0xa0, 0xc9, 0x5e, 0xc2, 0x2e);
		public static readonly Guid MicrophoneArrayProcessor = new(0x830a44f2, 0xa32d, 0x476b, 0xbe, 0x97, 0x42, 0x84, 0x56, 0x73, 0xb3, 0x5a);
		public static readonly Guid TvTuner = new(0xa799a800, 0xa46d, 0x11d0, 0xa1, 0x8c, 0x00, 0xa0, 0x24, 0x01, 0xdc, 0xd4);
		public static readonly Guid CrossBar = new(0xa799a801, 0xa46d, 0x11d0, 0xa1, 0x8c, 0x00, 0xa0, 0x24, 0x01, 0xdc, 0xd4);
		public static readonly Guid TvAudio = new(0xa799a802, 0xa46d, 0x11d0, 0xa1, 0x8c, 0x00, 0xa0, 0x24, 0x01, 0xdc, 0xd4);
		public static readonly Guid VideoMultiplexing = new(0xa799a803, 0xa46d, 0x11d0, 0xa1, 0x8c, 0x00, 0xa0, 0x24, 0x01, 0xdc, 0xd4);
		public static readonly Guid VideoBlankingIntervalCodec = new(0x07dad660, 0x22f1, 0x11d1, 0xa9, 0xf4, 0x00, 0xc0, 0x4f, 0xbb, 0xde, 0x8f);
	}

	/// <summary>Gets the device interface class Guid for the specified Bluetooth UUID.</summary>
	/// <remarks>The 16-bit UUIDs are defined in the Assigned Numbers document.</remarks>
	/// <param name="uuid">The UUID value for which a GUID should be retrieved.</param>
	/// <returns>A GUID corresponding to the provided UUID.</returns>
	public static Guid GetBluetoothUuidGuid(ushort uuid) => new(uuid, 0x0000, 0x1000, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb);

	/// <summary>Gets the device interface class Guid for the specified Bluetooth UUID.</summary>
	/// <remarks>The 16-bit UUIDs are defined in the Assigned Numbers document.</remarks>
	/// <param name="uuid">The UUID value for which a GUID should be retrieved.</param>
	/// <returns>A GUID corresponding to the provided UUID.</returns>
	public static Guid GetBluetoothUuidGuid(BluetoothServiceUuid uuid) => GetBluetoothUuidGuid((ushort)uuid);

	/// <summary>Gets the device interface class Guid for the specified Bluetooth UUID.</summary>
	/// <remarks>The 16-bit UUIDs are defined in the Assigned Numbers document.</remarks>
	/// <param name="uuid">The UUID value for which a GUID should be retrieved.</param>
	/// <returns>A GUID corresponding to the provided UUID.</returns>
	public static Guid GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid uuid) => GetBluetoothUuidGuid((ushort)uuid);

	/// <summary>Returns the UUID part from the specified GUID, if it a Bluetooth GUID.</summary>
	/// <param name="guid">The GUID/</param>
	/// <param name="uuid">The UUID if found.</param>
	/// <returns><see langword="true"/> if the UUID was retrieved; otherwise <see langword="false"/>.</returns>
	public static bool TryGetBluetoothUuid(Guid guid, out ushort uuid)
	{
		uint a = Unsafe.As<Guid, uint>(ref guid);

		if ((a & 0xFFFF0000U) == 0 && guid == new Guid(a, 0x0000, 0x1000, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb))
		{
			uuid = (ushort)a;
			return true;
		}
		else
		{
			uuid = 0;
			return false;
		}
	}

	public static bool TryGetBluetoothUuid(Guid guid, out BluetoothServiceUuid uuid)
	{
		Unsafe.SkipInit(out uuid);
		return TryGetBluetoothUuid(guid, out Unsafe.As<BluetoothServiceUuid, ushort>(ref Unsafe.AsRef(in uuid)));
	}

	public static bool TryGetBluetoothUuid(Guid guid, out BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid uuid)
	{
		Unsafe.SkipInit(out uuid);
		return TryGetBluetoothUuid(guid, out Unsafe.As<BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid, ushort>(ref Unsafe.AsRef(in uuid)));
	}

	/// <summary>Exposes well-know guids for Bluetooth specific services.</summary>
	public static class BluetoothGattServiceClasses
	{
		// Commented lines below are indicated as "Profile" only in the Assigned Numbers document.

		public static Guid ServiceDiscoveryServerServiceClassId => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ServiceDiscoveryServerServiceClassId);
		public static Guid BrowseGroupDescriptorServiceClassId => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.BrowseGroupDescriptorServiceClassId);
		public static Guid SerialPort => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.SerialPort);
		public static Guid LanAccessUsingPointToPointProtocol => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.LanAccessUsingPointToPointProtocol);
		public static Guid DialupNetworking => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.DialupNetworking);
		public static Guid IrMobileCommunicationsSync => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.IrMobileCommunicationsSync);
		public static Guid ObjectExchangeObjectPush => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ObjectExchangeObjectPush);
		public static Guid ObjectExchangeFileTransfer => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ObjectExchangeFileTransfer);
		public static Guid IrMobileCommunicationsSyncCommand => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.IrMobileCommunicationsSyncCommand);
		public static Guid Headset => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.Headset);
		public static Guid CordlessTelephony => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.CordlessTelephony);
		public static Guid AudioSource => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.AudioSource);
		public static Guid AudioSink => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.AudioSink);
		public static Guid AudioVideoRemoteControlTarget => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.AudioVideoRemoteControlTarget);
		//public static Guid AdvancedAudioDistribution => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.AdvancedAudioDistribution);
		public static Guid AudioVideoRemoteControl => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.AudioVideoRemoteControl);
		public static Guid AudioVideoRemoteControlController => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.AudioVideoRemoteControlController);
		public static Guid Intercom => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.Intercom);
		public static Guid Fax => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.Fax);
		public static Guid HeadsetAudioGateway => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.HeadsetAudioGateway);
		public static Guid WirelessApplicationProtocol => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.WirelessApplicationProtocol);
		public static Guid WirelessApplicationProtocolClient => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.WirelessApplicationProtocolClient);
		public static Guid PersonalAreaNetworkUser => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.PersonalAreaNetworkUser);
		public static Guid NetworkAccessPoint => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.NetworkAccessPoint);
		public static Guid GroupAdHocNetwork => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.GroupAdHocNetwork);
		public static Guid DirectPrinting => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.DirectPrinting);
		public static Guid ReferencePrinting => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ReferencePrinting);
		//public static Guid BasicImagingProfile => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.BasicImagingProfile);
		public static Guid ImagingResponder => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ImagingResponder);
		public static Guid ImagingAutomaticArchive => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ImagingAutomaticArchive);
		public static Guid ImagingReferencedObjects => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ImagingReferencedObjects);
		public static Guid Handsfree => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.Handsfree);
		public static Guid HandsfreeAudioGateway => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.HandsfreeAudioGateway);
		public static Guid DirectPrintingReferenceObjectsService => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.DirectPrintingReferenceObjectsService);
		public static Guid ReflectedUI => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ReflectedUI);
		//public static Guid BasicPrinting => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.BasicPrinting);
		public static Guid PrintingStatus => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.PrintingStatus);
		public static Guid HumanInterfaceDeviceService => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.HumanInterfaceDeviceService);
		//public static Guid HardcopyCableReplacement => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.HardcopyCableReplacement);
		public static Guid HostControllerReadPrint => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.HostControllerReadPrint);
		public static Guid HostControllerReadScan => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.HostControllerReadScan);
		public static Guid CommonIntegratedServicesDigitalNetworkAccess => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.CommonIntegratedServicesDigitalNetworkAccess);
		public static Guid SimAccess => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.SimAccess);
		public static Guid PhonebookAccessPhonebookClientEquipment => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.PhonebookAccessPhonebookClientEquipment);
		public static Guid PhonebookAccessPhonebookServerEquipment => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.PhonebookAccessPhonebookServerEquipment);
		//public static Guid PhonebookAccess => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.PhonebookAccess);
		public static Guid HeadsetHighSpeed => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.HeadsetHighSpeed);
		public static Guid MessageAccessServer => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.MessageAccessServer);
		public static Guid MessageNotificationServer => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.MessageNotificationServer);
		//public static Guid MessageAccessProfile => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.MessageAccessProfile);
		//public static Guid GlobalNavigationSatelliteSystem => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.GlobalNavigationSatelliteSystem);
		public static Guid GlobalNavigationSatelliteSystemServer => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.GlobalNavigationSatelliteSystemServer);
		public static Guid ThreeDimensionDisplay => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ThreeDimensionDisplay);
		public static Guid ThreeDimensionGlasses => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ThreeDimensionGlasses);
		//public static Guid ThreeDimensionSynchronization => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ThreeDimensionSynchronization);
		//public static Guid MultiProfileSpecificationProfile => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.MultiProfileSpecificationProfile);
		public static Guid MultiProfileSpecificationSecureConnection => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.MultiProfileSpecificationSecureConnection);
		public static Guid CalendarTasksAndNotesAccessService => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.CalendarTasksAndNotesAccessService);
		public static Guid CalendarTasksAndNotesNotificationService => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.CalendarTasksAndNotesNotificationService);
		//public static Guid CalendarTasksAndNotesProfile => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.CalendarTasksAndNotesProfile);
		public static Guid PlugAndPlayInformation => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.PlugAndPlayInformation);
		public static Guid GenericNetworking => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.GenericNetworking);
		public static Guid GenericFileTransfer => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.GenericFileTransfer);
		public static Guid GenericAudio => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.GenericAudio);
		public static Guid GenericTelephony => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.GenericTelephony);
		public static Guid UniversalPlugAndPlayService => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.UniversalPlugAndPlayService);
		public static Guid UniversalPlugAndPlayIpService => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.UniversalPlugAndPlayIpService);
		public static Guid ExtendedServiceDiscoveryProfileUniversalPlugAndPlayIpPersonalAreaNetwork => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ExtendedServiceDiscoveryProfileUniversalPlugAndPlayIpPersonalAreaNetwork);
		public static Guid ExtendedServiceDiscoveryProfileUniversalPlugAndPlayIpLanAccessProfile => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ExtendedServiceDiscoveryProfileUniversalPlugAndPlayIpLanAccessProfile);
		public static Guid ExtendedServiceDiscoveryProfileUniversalPlugAndPlayLogicalLinkControlAndAdaptationLayerProtocol => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.ExtendedServiceDiscoveryProfileUniversalPlugAndPlayLogicalLinkControlAndAdaptationLayerProtocol);
		public static Guid VideoSource => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.VideoSource);
		public static Guid VideoSink => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.VideoSink);
		//public static Guid VideoDistribution => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.VideoDistribution);
		//public static Guid HealthDeviceProfile => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.HealthDeviceProfile);
		public static Guid HealthDeviceProfileSource => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.HealthDeviceProfileSource);
		public static Guid HealthDeviceProfileSink => GetBluetoothUuidGuid(BluetoothServiceDiscoveryProtocolServiceClassAndProfileUuid.HealthDeviceProfileSink);
	}

	public static class BluetoothGattServices
	{
		public static Guid GenericAccess => GetBluetoothUuidGuid(BluetoothServiceUuid.GenericAccess);
		public static Guid GenericAttribute => GetBluetoothUuidGuid(BluetoothServiceUuid.GenericAttribute);
		public static Guid ImmediateAlert => GetBluetoothUuidGuid(BluetoothServiceUuid.ImmediateAlert);
		public static Guid LinkLoss => GetBluetoothUuidGuid(BluetoothServiceUuid.LinkLoss);
		public static Guid TxPower => GetBluetoothUuidGuid(BluetoothServiceUuid.TxPower);
		public static Guid CurrentTime => GetBluetoothUuidGuid(BluetoothServiceUuid.CurrentTime);
		public static Guid ReferenceTimeUpdate => GetBluetoothUuidGuid(BluetoothServiceUuid.ReferenceTimeUpdate);
		public static Guid NextDaylightSavingTimeChange => GetBluetoothUuidGuid(BluetoothServiceUuid.NextDaylightSavingTimeChange);
		public static Guid Glucose => GetBluetoothUuidGuid(BluetoothServiceUuid.Glucose);
		public static Guid HealthThermometer => GetBluetoothUuidGuid(BluetoothServiceUuid.HealthThermometer);
		public static Guid DeviceInformation => GetBluetoothUuidGuid(BluetoothServiceUuid.DeviceInformation);

		public static Guid HeartRate => GetBluetoothUuidGuid(BluetoothServiceUuid.HeartRate);
		public static Guid PhoneAlertStatus => GetBluetoothUuidGuid(BluetoothServiceUuid.PhoneAlertStatus);
		public static Guid Battery => GetBluetoothUuidGuid(BluetoothServiceUuid.Battery);
		public static Guid BloodPressure => GetBluetoothUuidGuid(BluetoothServiceUuid.BloodPressure);
		public static Guid AlertNotification => GetBluetoothUuidGuid(BluetoothServiceUuid.AlertNotification);
		public static Guid HumanInterfaceDevice => GetBluetoothUuidGuid(BluetoothServiceUuid.HumanInterfaceDevice);
		public static Guid ScanParameters => GetBluetoothUuidGuid(BluetoothServiceUuid.ScanParameters);
		public static Guid RunningSpeedAndCadence => GetBluetoothUuidGuid(BluetoothServiceUuid.RunningSpeedAndCadence);
		public static Guid AutomationIo => GetBluetoothUuidGuid(BluetoothServiceUuid.AutomationIo);
		public static Guid CyclingSpeedAndCadence => GetBluetoothUuidGuid(BluetoothServiceUuid.CyclingSpeedAndCadence);

		public static Guid CyclingPower => GetBluetoothUuidGuid(BluetoothServiceUuid.CyclingPower);
		public static Guid LocationAndNavigationService => GetBluetoothUuidGuid(BluetoothServiceUuid.LocationAndNavigationService);
		public static Guid EnvironmentalSensing => GetBluetoothUuidGuid(BluetoothServiceUuid.EnvironmentalSensing);
		public static Guid BodyComposition => GetBluetoothUuidGuid(BluetoothServiceUuid.BodyComposition);
		public static Guid UserData => GetBluetoothUuidGuid(BluetoothServiceUuid.UserData);
		public static Guid WeightScale => GetBluetoothUuidGuid(BluetoothServiceUuid.WeightScale);
		public static Guid BondManagement => GetBluetoothUuidGuid(BluetoothServiceUuid.BondManagement);
		public static Guid ContinuousGlucoseMonitoring => GetBluetoothUuidGuid(BluetoothServiceUuid.ContinuousGlucoseMonitoring);
		public static Guid InternetProtocolSupport => GetBluetoothUuidGuid(BluetoothServiceUuid.InternetProtocolSupport);
		public static Guid IndoorPositioning => GetBluetoothUuidGuid(BluetoothServiceUuid.IndoorPositioning);
		public static Guid PulseOximeter => GetBluetoothUuidGuid(BluetoothServiceUuid.PulseOximeter);
		public static Guid HttpProxy => GetBluetoothUuidGuid(BluetoothServiceUuid.HttpProxy);
		public static Guid TransportDiscovery => GetBluetoothUuidGuid(BluetoothServiceUuid.TransportDiscovery);
		public static Guid ObjectTransfer => GetBluetoothUuidGuid(BluetoothServiceUuid.ObjectTransfer);
		public static Guid FitnessMachine => GetBluetoothUuidGuid(BluetoothServiceUuid.FitnessMachine);
		public static Guid MeshProvisioning => GetBluetoothUuidGuid(BluetoothServiceUuid.MeshProvisioning);
		public static Guid MeshProxy => GetBluetoothUuidGuid(BluetoothServiceUuid.MeshProxy);
		public static Guid ReconnectionConfiguration => GetBluetoothUuidGuid(BluetoothServiceUuid.ReconnectionConfiguration);
		public static Guid InsulinDelivery => GetBluetoothUuidGuid(BluetoothServiceUuid.InsulinDelivery);
		public static Guid BinarySensor => GetBluetoothUuidGuid(BluetoothServiceUuid.BinarySensor);
		public static Guid EmergencyConfiguration => GetBluetoothUuidGuid(BluetoothServiceUuid.EmergencyConfiguration);
		public static Guid AuthorizationControl => GetBluetoothUuidGuid(BluetoothServiceUuid.AuthorizationControl);
		public static Guid PhysicalActivityMonitor => GetBluetoothUuidGuid(BluetoothServiceUuid.PhysicalActivityMonitor);

		public static Guid AudioInputControl => GetBluetoothUuidGuid(BluetoothServiceUuid.AudioInputControl);
		public static Guid VolumeControl => GetBluetoothUuidGuid(BluetoothServiceUuid.VolumeControl);
		public static Guid VolumeOffsetControl => GetBluetoothUuidGuid(BluetoothServiceUuid.VolumeOffsetControl);
		public static Guid CoordinatedSetIdentification => GetBluetoothUuidGuid(BluetoothServiceUuid.CoordinatedSetIdentification);
		public static Guid DeviceTime => GetBluetoothUuidGuid(BluetoothServiceUuid.DeviceTime);
		public static Guid MediaControl => GetBluetoothUuidGuid(BluetoothServiceUuid.MediaControl);
		public static Guid GenericMediaControl => GetBluetoothUuidGuid(BluetoothServiceUuid.GenericMediaControl);
		public static Guid ConstantToneExtension => GetBluetoothUuidGuid(BluetoothServiceUuid.ConstantToneExtension);
		public static Guid TelephoneBearer => GetBluetoothUuidGuid(BluetoothServiceUuid.TelephoneBearer);
		public static Guid GenericTelephoneBearer => GetBluetoothUuidGuid(BluetoothServiceUuid.GenericTelephoneBearer);
		public static Guid MicrophoneControl => GetBluetoothUuidGuid(BluetoothServiceUuid.MicrophoneControl);
		public static Guid AudioStreamControl => GetBluetoothUuidGuid(BluetoothServiceUuid.AudioStreamControl);
		public static Guid BroadcastAudioScan => GetBluetoothUuidGuid(BluetoothServiceUuid.BroadcastAudioScan);

		public static Guid PublishedAudioCapabilities => GetBluetoothUuidGuid(BluetoothServiceUuid.PublishedAudioCapabilities);
		public static Guid BasicAudioAnnouncement => GetBluetoothUuidGuid(BluetoothServiceUuid.BasicAudioAnnouncement);
		public static Guid BroadcastAudioAnnouncement => GetBluetoothUuidGuid(BluetoothServiceUuid.BroadcastAudioAnnouncement);
		public static Guid CommonAudio => GetBluetoothUuidGuid(BluetoothServiceUuid.CommonAudio);
		public static Guid HearingAid => GetBluetoothUuidGuid(BluetoothServiceUuid.HearingAid);
		public static Guid TelephonyAndMediaAudio => GetBluetoothUuidGuid(BluetoothServiceUuid.TelephonyAndMediaAudio);
		public static Guid PublicBroadcastAnnouncement => GetBluetoothUuidGuid(BluetoothServiceUuid.PublicBroadcastAnnouncement);
	}
}
