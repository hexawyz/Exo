using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 61)]
internal struct RawVeryLongNotificationParameters
{
	private byte _data00;
	private byte _data01;
	private byte _data02;
	private byte _data03;
	private byte _data04;
	private byte _data05;
	private byte _data06;
	private byte _data07;
	private byte _data08;
	private byte _data09;
	private byte _data0A;
	private byte _data0B;
	private byte _data0C;
	private byte _data0D;
	private byte _data0E;
	private byte _data0F;
	private byte _data10;
	private byte _data11;
	private byte _data12;
	private byte _data13;
	private byte _data14;
	private byte _data15;
	private byte _data16;
	private byte _data17;
	private byte _data18;
	private byte _data19;
	private byte _data1A;
	private byte _data1B;
	private byte _data1C;
	private byte _data1D;
	private byte _data1E;
	private byte _data1F;
	private byte _data20;
	private byte _data21;
	private byte _data22;
	private byte _data23;
	private byte _data24;
	private byte _data25;
	private byte _data26;
	private byte _data27;
	private byte _data28;
	private byte _data29;
	private byte _data2A;
	private byte _data2B;
	private byte _data2C;
	private byte _data2D;
	private byte _data2E;
	private byte _data2F;
	private byte _data30;
	private byte _data31;
	private byte _data32;
	private byte _data33;
	private byte _data34;
	private byte _data35;
	private byte _data36;
	private byte _data37;
	private byte _data38;
	private byte _data39;
	private byte _data3A;
	private byte _data3B;
	private byte _data3C;

	public byte this[int index]
	{
		get => GetSpan(ref this)[index];
		set => GetSpan(ref this)[index] = value;
	}

	public static Span<byte> GetSpan(ref RawVeryLongNotificationParameters message)
		=> MemoryMarshal.CreateSpan(ref message._data00, 61);
}
