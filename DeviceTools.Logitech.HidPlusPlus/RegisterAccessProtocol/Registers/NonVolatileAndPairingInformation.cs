using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Registers;

public static class NonVolatileAndPairingInformation
{
	public enum Parameter
	{
		ReceiverInformation = 0x03,

		PairingInformation1 = 0x20,
		PairingInformation2 = 0x21,
		PairingInformation3 = 0x22,
		PairingInformation4 = 0x23,
		PairingInformation5 = 0x24,
		PairingInformation6 = 0x25,

		ExtendedPairingInformation1 = 0x30,
		ExtendedPairingInformation2 = 0x31,
		ExtendedPairingInformation3 = 0x32,
		ExtendedPairingInformation4 = 0x33,
		ExtendedPairingInformation5 = 0x34,
		ExtendedPairingInformation6 = 0x35,

		DeviceName1 = 0x40,
		DeviceName2 = 0x41,
		DeviceName3 = 0x42,
		DeviceName4 = 0x43,
		DeviceName5 = 0x44,
		DeviceName6 = 0x45,

		BoltPairingInformation1 = 0x51,
		BoltPairingInformation2 = 0x52,
		BoltPairingInformation3 = 0x53,
		BoltPairingInformation4 = 0x54,
		BoltPairingInformation5 = 0x55,
		BoltPairingInformation6 = 0x56,

		BoltDeviceName1 = 0x61,
		BoltDeviceName2 = 0x62,
		BoltDeviceName3 = 0x63,
		BoltDeviceName4 = 0x64,
		BoltDeviceName5 = 0x65,
		BoltDeviceName6 = 0x66,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
	public struct Request : IMessageGetParametersWithOneExtraParameter, IShortMessageParameters
	{
		private byte _parameter;

		public Request(Parameter parameter) => _parameter = (byte)parameter;

		public Parameter Parameter
		{
			readonly get => (Parameter)_parameter;
			set => _parameter = (byte)value;
		}

		readonly byte IMessageGetParametersWithOneExtraParameter.Parameter => _parameter;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
	public struct RequestWithIndex : IMessageGetParametersWithTwoExtraParameters, IShortMessageParameters
	{
		private byte _parameter;
		public byte Index;

		public RequestWithIndex(Parameter parameter, byte index)
		{
			_parameter = (byte)parameter;
			Index = index;
		}

		public Parameter Parameter
		{
			readonly get => (Parameter)_parameter;
			set => _parameter = (byte)value;
		}

		readonly byte IMessageGetParametersWithTwoExtraParameters.Parameter1 => _parameter;
		readonly byte IMessageGetParametersWithTwoExtraParameters.Parameter2 => Index;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct ReceiverInformationResponse : ILongMessageParameters
	{
		private byte _parameter;
		public Parameter Parameter
		{
			readonly get => (Parameter)_parameter;
			set => _parameter = (byte)value;
		}

#pragma warning disable IDE0044 // Add readonly modifier
		private byte _serialNumber0;
		private byte _serialNumber1;
		private byte _serialNumber2;
		private byte _serialNumber3;
#pragma warning restore IDE0044 // Add readonly modifier

		public uint SerialNumber
		{
			readonly get => BigEndian.ReadUInt32(_serialNumber0);
			set => BigEndian.Write(ref _serialNumber0, value);
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct PairingInformationResponse : ILongMessageParameters
	{
		private byte _parameter;
		public Parameter Parameter
		{
			readonly get => (Parameter)_parameter;
			set => _parameter = (byte)value;
		}
		public byte DestinationId;
		public byte DefaultReportInterval;

#pragma warning disable IDE0044 // Add readonly modifier
		private byte _wirelessProductId0;
		private byte _wirelessProductId1;

		public ushort WirelessProductId
		{
			readonly get => BigEndian.ReadUInt16(_wirelessProductId0);
			set => BigEndian.Write(ref _wirelessProductId0, value);
		}

		private byte _reserved0;
		private byte _reserved1;

		public byte DeviceType;

		private byte _reserved2;
		private byte _reserved3;
		private byte _reserved4;
		private byte _reserved5;
		private byte _reserved6;
		private byte _reserved7;
#pragma warning restore IDE0044 // Add readonly modifier
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct ExtendedPairingInformationResponse : ILongMessageParameters
	{
		private byte _parameter;
		public Parameter Parameter
		{
			readonly get => (Parameter)_parameter;
			set => _parameter = (byte)value;
		}

#pragma warning disable IDE0044 // Add readonly modifier
		private byte _serialNumber0;
		private byte _serialNumber1;
		private byte _serialNumber2;
		private byte _serialNumber3;
#pragma warning restore IDE0044 // Add readonly modifier

		public uint SerialNumber
		{
			readonly get => BigEndian.ReadUInt32(_serialNumber0);
			set => BigEndian.Write(ref _serialNumber0, value);
		}

#pragma warning disable IDE0044 // Add readonly modifier
		private byte _reportTypes0;
		private byte _reportTypes1;
		private byte _reportTypes2;
		private byte _reportTypes3;
#pragma warning restore IDE0044 // Add readonly modifier

		public uint ReportTypes
		{
			readonly get => BigEndian.ReadUInt32(_reportTypes0);
			set => BigEndian.Write(ref _reportTypes0, value);
		}

		private byte _usabilityInfo;

		public PowerSwitchLocation PowerSwitchLocation
		{
			readonly get => (PowerSwitchLocation)(_usabilityInfo & 0x0F);
			set
			{
				if ((byte)value >= 0xD) throw new ArgumentOutOfRangeException(nameof(value));

				_usabilityInfo = (byte)(_usabilityInfo & 0xF0 | (byte)value);
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct DeviceNameResponse : ILongMessageParameters
	{
		private byte _parameter;
		public Parameter Parameter
		{
			readonly get => (Parameter)_parameter;
			set => _parameter = (byte)value;
		}
		public byte Length;

#pragma warning disable IDE0044 // Add readonly modifier
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
#pragma warning restore IDE0044 // Add readonly modifier

		public bool TryCopyTo(Span<byte> span, out int charsWritten)
		{
			var src = MemoryMarshal.CreateSpan(ref _deviceName0, Math.Min((byte)14, Length));

			int length = src.Length;

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

		public string GetDeviceName() => Encoding.UTF8.GetString(MemoryMarshal.CreateSpan(ref _deviceName0, Math.Min((byte)14, Length)));
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct BoltPairingInformationResponse : ILongMessageParameters
	{
		private byte _parameter;
		public Parameter Parameter
		{
			readonly get => (Parameter)_parameter;
			set => _parameter = (byte)value;
		}
		public byte DeviceInformation;

#pragma warning disable IDE0044 // Add readonly modifier
		private byte _wirelessProductId0;
		private byte _wirelessProductId1;

		public ushort WirelessProductId
		{
			readonly get => LittleEndian.ReadUInt16(_wirelessProductId0);
			set => LittleEndian.Write(ref _wirelessProductId0, value);
		}
#pragma warning restore IDE0044 // Add readonly modifier
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct BoltDeviceNameResponse : ILongMessageParameters
	{
		private byte _parameter;
		public Parameter Parameter
		{
			readonly get => (Parameter)_parameter;
			set => _parameter = (byte)value;
		}

		public byte Index;

		public byte Length;

#pragma warning disable IDE0044 // Add readonly modifier
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
#pragma warning restore IDE0044 // Add readonly modifier

		public bool TryCopyTo(Span<byte> span, out int charsWritten)
		{
			var src = MemoryMarshal.CreateSpan(ref _deviceName0, Math.Min((byte)13, Length));

			int length = src.Length;

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

		public string GetDeviceName() => Encoding.UTF8.GetString(MemoryMarshal.CreateSpan(ref _deviceName0, Math.Min((byte)13, Length)));
	}
}
