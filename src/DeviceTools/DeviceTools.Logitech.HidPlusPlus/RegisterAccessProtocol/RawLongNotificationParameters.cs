using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 17)]
internal struct RawLongNotificationParameters
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

	public byte this[int index]
	{
		get => GetSpan(ref this)[index];
		set => GetSpan(ref this)[index] = value;
	}

	public static Span<byte> GetSpan(ref RawLongNotificationParameters message)
		=> MemoryMarshal.CreateSpan(ref message._data00, 17);
}
