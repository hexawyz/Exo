using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class AdjustableDpi
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.BatteryUnifiedLevelStatus;

	public static class GetSensorCount
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public byte SensorCount;
		}
	}

	public static class GetSensorDpiList
	{
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte SensorIndex;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _dpi00;
			private byte _dpi01;
			private byte _dpi10;
			private byte _dpi11;
			private byte _dpi20;
			private byte _dpi21;
			private byte _dpi30;
			private byte _dpi31;
			private byte _dpi40;
			private byte _dpi41;
			private byte _dpi50;
			private byte _dpi51;
			private byte _dpi60;
			private byte _dpi61;
			private byte _dpi70;
			private byte _dpi71;

			public ushort this[int index]
			{
				get => Unsafe.ReadUnaligned<ushort>(ref Unsafe.As<ushort, byte>(ref GetSpan(ref this)[index]));
				set => Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref GetSpan(ref this)[index]), value);
			}

			public ushort Dpi0
			{
				get => BigEndian.ReadUInt16(_dpi00);
				set => BigEndian.Write(ref _dpi00, value);
			}

			public ushort Dpi1
			{
				get => BigEndian.ReadUInt16(_dpi10);
				set => BigEndian.Write(ref _dpi10, value);
			}

			public ushort Dpi2
			{
				get => BigEndian.ReadUInt16(_dpi20);
				set => BigEndian.Write(ref _dpi20, value);
			}

			public ushort Dpi3
			{
				get => BigEndian.ReadUInt16(_dpi30);
				set => BigEndian.Write(ref _dpi30, value);
			}

			public ushort Dpi4
			{
				get => BigEndian.ReadUInt16(_dpi40);
				set => BigEndian.Write(ref _dpi40, value);
			}

			public ushort Dpi5
			{
				get => BigEndian.ReadUInt16(_dpi50);
				set => BigEndian.Write(ref _dpi50, value);
			}

			public ushort Dpi6
			{
				get => BigEndian.ReadUInt16(_dpi60);
				set => BigEndian.Write(ref _dpi60, value);
			}

			public ushort Dpi7
			{
				get => BigEndian.ReadUInt16(_dpi70);
				set => BigEndian.Write(ref _dpi70, value);
			}

			private static Span<ushort> GetSpan(ref Response response)
				=> MemoryMarshal.CreateSpan(ref Unsafe.As<byte, ushort>(ref response._dpi00), 16);
		}
	}

	public static class GetSensorDpi
	{
		public const byte FunctionId = 2;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte SensorIndex;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public byte SensorIndex;

			private byte _currentDpi0;
			private byte _currentDpi1;

			public ushort CurrentDpi
			{
				get => BigEndian.ReadUInt16(_currentDpi0);
				set => BigEndian.Write(ref _currentDpi0, value);
			}

			private byte _defaultDpi0;
			private byte _defaultDpi1;

			public ushort DefaultDpi
			{
				get => BigEndian.ReadUInt16(_defaultDpi0);
				set => BigEndian.Write(ref _defaultDpi0, value);
			}
		}
	}

	public static class SetSensorDpi
	{
		public const byte FunctionId = 3;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte SensorIndex;

			private byte _dpi0;
			private byte _dpi1;

			public ushort Dpi
			{
				get => BigEndian.ReadUInt16(_dpi0);
				set => BigEndian.Write(ref _dpi0, value);
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public byte SensorIndex;

			private byte _dpi0;
			private byte _dpi1;

			public ushort Dpi
			{
				get => BigEndian.ReadUInt16(_dpi0);
				set => BigEndian.Write(ref _dpi0, value);
			}
		}
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
