using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

public static class LockKeyState
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.LockKeyState;

	public static class GetLockKeyStatus
	{
		public const byte EventId = 0;
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			private byte _lockedKeys;

			public LockableKeys LockedKeys
			{
				get => (LockableKeys)_lockedKeys;
				set => _lockedKeys = (byte)value;
			}
		}
	}

	[Flags]
	public enum LockableKeys : byte
	{
		NumLock = 0x01,
		CapsLock = 0x02,
		ScrollLock = 0x04,
	}
}
