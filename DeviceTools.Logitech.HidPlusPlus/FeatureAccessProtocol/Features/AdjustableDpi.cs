using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class AdjustableDpi
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.AdjustableDpi;

	public static class GetSensorCount
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public byte SensorCount;
		}
	}

	public static class GetSensorDpiRanges
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
			public byte SensorIndex;
			private byte _item00;
			private byte _item01;
			private byte _item10;
			private byte _item11;
			private byte _item20;
			private byte _item21;
			private byte _item30;
			private byte _item31;
			private byte _item40;
			private byte _item41;
			private byte _item50;
			private byte _item51;
			private byte _item60;
			private byte _item61;

			public ushort this[int index]
			{
				readonly get
				{
					if ((uint)index >= (uint)ItemCount) throw new ArgumentOutOfRangeException(nameof(index));
					return BigEndian.ReadUInt16(in Unsafe.AddByteOffset(ref Unsafe.AsRef(in _item00), 2 * index));
				}
				set
				{
					if ((uint)index >= (uint)ItemCount) throw new ArgumentOutOfRangeException(nameof(index));
					BigEndian.Write(ref Unsafe.AddByteOffset(ref _item00, 2 * index), value);
				}
			}

			public readonly int ItemCount => 7;
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
