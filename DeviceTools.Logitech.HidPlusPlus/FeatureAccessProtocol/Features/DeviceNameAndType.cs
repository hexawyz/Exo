using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class DeviceNameAndType
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.DeviceNameAndType;

	public static class GetDeviceNameLength
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public byte Length;
		}
	}

	public static class GetDeviceName
	{
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte Offset;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters, ILongMessageParameters
		{
			private byte _deviceName0;
			private byte _deviceName1;
			private byte _deviceName2;
			private byte _deviceName3;
			private byte _deviceName4;
			private byte _deviceName5;
			private byte _deviceName6;
			private byte _deviceName7;
			private byte _deviceName8;
			private byte _deviceName9;
			private byte _deviceNameA;
			private byte _deviceNameB;
			private byte _deviceNameC;
			private byte _deviceNameD;
			private byte _deviceNameE;
			private byte _deviceNameF;

			public bool TryCopyTo(Span<byte> span, out int charsWritten)
			{
				var src = MemoryMarshal.CreateSpan(ref _deviceName0, 16);

				int length = src.IndexOf((byte)0);

				if (length < 0) length = 16;

				if (span.Length < length)
				{
					charsWritten = 0;
					return false;
				}
				else
				{
					src[..length].CopyTo(span);
					charsWritten = length;
					return true;
				}
			}
		}
	}

	public static class GetDeviceType
	{
		public const byte FunctionId = 2;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			private byte _deviceType;
			public DeviceType DeviceType
			{
				get => (DeviceType)_deviceType;
				set => _deviceType = (byte)value;
			}
		}
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
