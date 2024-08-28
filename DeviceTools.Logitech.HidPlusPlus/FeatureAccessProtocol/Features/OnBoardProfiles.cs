using System.Runtime.CompilerServices;
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
			public byte RomProfileCount;
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

	public static class SetCurrentProfile
	{
		public const byte FunctionId = 3;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			private byte _unknown;
			public byte ActiveProfileIndex;
		}
	}

	public static class GetCurrentProfile
	{
		public const byte FunctionId = 4;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			private byte _unknown;
			public byte ActiveProfileIndex;
		}
	}

	public static class ReadMemory
	{
		public const byte FunctionId = 5;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Request : IMessageRequestParameters, ILongMessageParameters
		{
			private byte _sectorIndex0;
			private byte _sectorIndex1;
			private byte _offset0;
			private byte _offset1;

			public ushort SectorIndex
			{
				get => BigEndian.ReadUInt16(in _sectorIndex0);
				set => BigEndian.Write(ref _sectorIndex0, value);
			}

			public ushort Offset
			{
				get => BigEndian.ReadUInt16(in _offset0);
				set => BigEndian.Write(ref _offset0, value);
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _data0;
			private byte _data1;
			private byte _data2;
			private byte _data3;
			private byte _data4;
			private byte _data5;
			private byte _data6;
			private byte _data7;
			private byte _data8;
			private byte _data9;
			private byte _dataA;
			private byte _dataB;
			private byte _dataC;
			private byte _dataD;
			private byte _dataE;
			private byte _dataF;

			public static ReadOnlySpan<byte> AsReadOnlySpan(in Response response)
				=> MemoryMarshal.CreateReadOnlySpan(in response._data0, 16);

			public readonly void CopyTo(Span<byte> span)
				=> AsReadOnlySpan(in this).CopyTo(span);
		}
	}

	public static class StartWrite
	{
		public const byte FunctionId = 6;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Request : IMessageRequestParameters, ILongMessageParameters
		{
			private byte _sectorIndex0;
			private byte _sectorIndex1;
			private byte _offset0;
			private byte _offset1;
			private byte _count0;
			private byte _count1;

			public ushort SectorIndex
			{
				get => BigEndian.ReadUInt16(in _sectorIndex0);
				set => BigEndian.Write(ref _sectorIndex0, value);
			}

			public ushort Offset
			{
				get => BigEndian.ReadUInt16(in _offset0);
				set => BigEndian.Write(ref _offset0, value);
			}

			public ushort Count
			{
				get => BigEndian.ReadUInt16(in _count0);
				set => BigEndian.Write(ref _count0, value);
			}
		}
	}

	public static class WriteMemory
	{
		public const byte FunctionId = 7;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Request : IMessageRequestParameters, ILongMessageParameters
		{
			private byte _data0;
			private byte _data1;
			private byte _data2;
			private byte _data3;
			private byte _data4;
			private byte _data5;
			private byte _data6;
			private byte _data7;
			private byte _data8;
			private byte _data9;
			private byte _dataA;
			private byte _dataB;
			private byte _dataC;
			private byte _dataD;
			private byte _dataE;
			private byte _dataF;

			public static Span<byte> AsSpan(ref Request request)
				=> MemoryMarshal.CreateSpan(ref request._data0, 16);

			public void Write(ReadOnlySpan<byte> span)
				=> span.CopyTo(AsSpan(ref this));
		}
	}

	public static class EndWrite
	{
		public const byte FunctionId = 8;
	}

	public static class GetCurrentDpiIndex
	{
		public const byte FunctionId = 11;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public byte ActivePresetIndex;
		}
	}

	public static class SetCurrentDpiIndex
	{
		public const byte FunctionId = 12;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte ActivePresetIndex;
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

	public readonly struct ProfileEntry
	{
		public static readonly ProfileEntry Empty = Unsafe.BitCast<uint, ProfileEntry>(0xFFFFFFFF);

		// NB: libratbag calls those 16 bytes the address, but it is correlated with the 1-based index?
		private readonly byte _reserved0;
		private readonly byte _profileIndex;
		private readonly byte _isEnabled;
		private readonly byte _reserved;

		public byte ProfileIndex => _profileIndex;
		public bool IsEnabled => _isEnabled != 0;

		public ProfileEntry(byte profileIndex, bool isEnabled)
		{
			_profileIndex = profileIndex;
			_isEnabled = isEnabled ? (byte)1 : (byte)0;
			_reserved = 0xFF;
		}
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
