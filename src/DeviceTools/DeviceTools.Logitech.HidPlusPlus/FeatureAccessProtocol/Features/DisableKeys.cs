using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;
#pragma warning restore IDE0044 // Add readonly modifier

#pragma warning disable IDE0044 // Add readonly modifier
public static class DisableKeys
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.DisableKeys;

	public static class GetCapabilities
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public Keys AvailableKeys;
		}
	}

	public static class GetDisabledKeys
	{
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public Keys DisabledKeys;
		}
	}

	public static class SetDisabledKeys
	{
		public const byte FunctionId = 2;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public Keys KeysToDisable;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public Keys DisabledKeys;
		}
	}

	[Flags]
	public enum Keys : byte
	{
		CapsLock = 0b00000001,
		NumLock = 0b00000010,
		ScrollLock = 0b00000100,
		Insert = 0b00001000,
		Windows = 0b00010000,
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
