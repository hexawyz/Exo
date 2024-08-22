using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

// NB: Information mostly obtained from libratbag, as logitech doesn't seem to have published an official doc ?
// See: https://github.com/libratbag/libratbag

#pragma warning disable IDE0044 // Add readonly modifier
public static class OnBoardProfiles
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.ColorLedEffects;

	public static class GetInfo
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public MemoryType MemoryType;
			public ProfileFormat ProfileFormat;
			public MacroFormat MacroFormat;
			public byte ProfileCount;
			public byte ProfileCountOutOfTheBox;
			public byte ButtonCount;
			public byte SectorCount;
			private byte _sectorSize0;
			private byte _sectorSize1;
			public byte MechanicalLayout;
			public byte DeviceConnectionMode;

			public ushort SectorSize
			{
				get => BigEndian.ReadUInt16(in _sectorSize0);
				set => BigEndian.Write(ref _sectorSize0, value);
			}

			public bool HasGShift => (MechanicalLayout & 0x03) is 0x02;
			public bool HasDpiShift => (MechanicalLayout & 0x0C) is 0x80;

			public bool IsCorded => (DeviceConnectionMode & 0x07) is 0x01 or 0x04;
			public bool IsWireless => (DeviceConnectionMode & 0x07) is 0x02 or 0x04;
		}
	}

	public static class SetDeviceMode
	{
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public DeviceMode Mode;
		}
	}

	public static class GetDeviceMode
	{
		public const byte FunctionId = 2;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public DeviceMode Mode;
		}
	}

	public enum MemoryType : byte
	{
		G402 = 1,
	}

	public enum ProfileFormat : byte
	{
		G402 = 1,
		G303 = 2,
		G900 = 3,
		G915 = 4,
	}

	public enum MacroFormat : byte
	{
		G402 = 1,
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
