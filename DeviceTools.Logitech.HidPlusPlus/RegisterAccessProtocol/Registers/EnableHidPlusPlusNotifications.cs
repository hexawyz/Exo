using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Registers;

public static class EnableHidPlusPlusNotifications
{
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
	public struct Parameters : IMessageSetParameters, IShortMessageParameters
	{
		private byte _deviceReportingFlags0;

		private byte _receiverReportingFlags;

		public ReceiverReportingFlags ReceiverReportingFlags
		{
			get => (ReceiverReportingFlags)_receiverReportingFlags;
			set => _receiverReportingFlags = (byte)value;
		}

		private byte _deviceReportingFlags1;

		public DeviceReportingFlags DeviceReportingFlags
		{
			get => (DeviceReportingFlags)(_deviceReportingFlags0 | _deviceReportingFlags1 << 8);
			set => (_deviceReportingFlags0, _deviceReportingFlags1) = ((byte)(ushort)value, (byte)((ushort)value >> 8));
		}
	}
}
