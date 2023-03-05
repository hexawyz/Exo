using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
public struct RawLongMessageParameters : ILongMessageParameters
{
	public byte Byte0;
	public byte Byte1;
	public byte Byte2;
	public byte Byte3;
	public byte Byte4;
	public byte Byte5;
	public byte Byte6;
	public byte Byte7;
	public byte Byte8;
	public byte Byte9;
	public byte ByteA;
	public byte ByteB;
	public byte ByteC;
	public byte ByteD;
	public byte ByteE;
	public byte ByteF;
}
