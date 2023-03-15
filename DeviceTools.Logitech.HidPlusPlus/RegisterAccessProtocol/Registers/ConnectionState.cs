using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Registers;

public static class ConnectionState
{
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
	public struct SetRequest : IMessageSetParameters, IShortMessageParameters
	{
		private byte _action;

		public ConnectionStateAction Action
		{
			get => (ConnectionStateAction)_action;
			set => _action = (byte)value;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
	public struct GetResponse : IShortMessageParameters
	{
#pragma warning disable IDE0044 // Add readonly modifier
		private byte _zero;
#pragma warning restore IDE0044 // Add readonly modifier

		public byte ConnectedDeviceCount;

		private byte _remainingPairingCount;

		public byte? RemainingPairingCount
		{
			get => _remainingPairingCount switch
			{
				0 => null,
				255 => 0,
				byte b => b
			};
			set => _remainingPairingCount = value switch
			{
				null => 0,
				0 => 255,
				255 => throw new ArgumentOutOfRangeException(nameof(value)),
				byte b => b
			};
		}
	}
}
