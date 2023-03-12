using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
internal struct RawShortNotificationParameters
{
	private byte _data0;
	private byte _data1;
	private byte _data2;
	private byte _data3;

	public byte this[int index]
	{
		get => GetSpan(ref this)[index];
		set => GetSpan(ref this)[index] = value;
	}

	public static Span<byte> GetSpan(ref RawShortNotificationParameters message)
		=> MemoryMarshal.CreateSpan(ref message._data0, 4);
}
