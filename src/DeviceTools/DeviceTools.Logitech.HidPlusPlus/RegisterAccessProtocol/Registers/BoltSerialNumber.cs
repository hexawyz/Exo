using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Registers;

public static class BoltSerialNumber
{
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct Response : ILongMessageParameters
	{
#pragma warning disable IDE0044 // Add readonly modifier
		private byte _serialNumber0;
		private byte _serialNumber1;
		private byte _serialNumber2;
		private byte _serialNumber3;
		private byte _serialNumber4;
		private byte _serialNumber5;
		private byte _serialNumber6;
		private byte _serialNumber7;
		private byte _serialNumber8;
		private byte _serialNumber9;
		private byte _serialNumberA;
		private byte _serialNumberB;
		private byte _serialNumberC;
		private byte _serialNumberD;
		private byte _serialNumberE;
		private byte _serialNumberF;
#pragma warning restore IDE0044 // Add readonly modifier

		public override string ToString()
			=> Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpan(ref _serialNumber0, 16));
	}
}
