using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class DeviceInformation
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.DeviceInformation;

	public static class GetDeviceInfo
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public byte EntityCount;

			private byte _unitId0;
			private byte _unitId1;
			private byte _unitId2;
			private byte _unitId3;
			public uint UnitId
			{
				get => BigEndian.ReadUInt32(_unitId0);
				set => BigEndian.Write(ref _unitId0, value);
			}

			private byte _transport0;
			private byte _transport1;
			public Transports Transports
			{
				get => (Transports)BigEndian.ReadUInt16(_transport0);
				set => BigEndian.Write(ref _transport0, (ushort)value);
			}

			private byte _productId00;
			private byte _productId01;
			public ushort ProductId0
			{
				get => BigEndian.ReadUInt16(_productId00);
				set => BigEndian.Write(ref _productId00, value);
			}

			private byte _productId10;
			private byte _productId11;
			public ushort ProductId1
			{
				get => BigEndian.ReadUInt16(_productId10);
				set => BigEndian.Write(ref _productId10, value);
			}

			private byte _productId20;
			private byte _productId21;
			public ushort ProductId2
			{
				get => BigEndian.ReadUInt16(_productId20);
				set => BigEndian.Write(ref _productId20, value);
			}

			public byte ExtendedModelId;

			private byte _capabilities;
			public DeviceCapabilities Capabilities
			{
				get => (DeviceCapabilities)_capabilities;
				set => _capabilities = (byte)value;
			}

			public ushort GetProductId(int index)
			{
				if ((uint)index > 2) throw new ArgumentOutOfRangeException(nameof(index));

				return BigEndian.ReadUInt16(Unsafe.Add(ref _productId00, index * 2));
			}

			public int GetProductIdCount() => BitOperations.PopCount((uint)((byte)Transports & 0xF));

			public ushort[] GetProductIds()
			{
				var productIds = new ushort[GetProductIdCount()];
				for (int i = 0; i < productIds.Length; i++)
				{
					productIds[i] = GetProductId(i);
				}
				return productIds;
			}

			public HidPlusPlusDeviceId[] GetDeviceIds()
			{
				var productIds = new HidPlusPlusDeviceId[GetProductIdCount()];
				var transports = Transports;
				var currentTransport = Transports.Bluetooth;
				for (int i = 0; i < productIds.Length; i++)
				{
					while ((transports & currentTransport) == 0)
					{
						currentTransport = (Transports)((byte)currentTransport << 1);
					}
					productIds[i] = new()
					{
						Source = currentTransport switch
						{
							Transports.Bluetooth => DeviceIdSource.Bluetooth,
							Transports.BluetoothLowEnergy => DeviceIdSource.BluetoothLowEnergy,
							Transports.EQuad => DeviceIdSource.EQuad,
							Transports.Usb => DeviceIdSource.Usb,
							_ => throw new InvalidOperationException($"The transport {currentTransport} is not supported."),
						},
						ProductId = GetProductId(i)
					};
				}
				return productIds;
			}
		}
	}

	public static class GetFirmwareInformation
	{
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte EntityIndex;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _deviceEntityType;
			public DeviceEntityType EntityType
			{
				get => (DeviceEntityType)_deviceEntityType;
				set => _deviceEntityType = (byte)value;
			}

			public byte FirmwareNamePrefix0;
			public byte FirmwareNamePrefix1;
			public byte FirmwareNamePrefix2;

			public byte FirmwareNameNumber;

			public byte Revision;

			private byte _build0;
			private byte _build1;
			public ushort Build
			{
				get => BigEndian.ReadUInt16(_build0);
				set => BigEndian.Write(ref _build0, value);
			}

			private byte _isActive;
			public bool IsActive
			{
				get => (_isActive & 1) != 0;
				set
				{
					if (value) _isActive |= 1;
					else _isActive &= 0xFE;
				}
			}

			private byte _transportProductId0;
			private byte _transportProductId1;
			public ushort TransportProductId
			{
				get => BigEndian.ReadUInt16(_transportProductId0);
				set => BigEndian.Write(ref _transportProductId0, value);
			}

			public byte ExtraVersion0;
			public byte ExtraVersion1;
			public byte ExtraVersion2;
			public byte ExtraVersion3;
			public byte ExtraVersion4;
		}
	}

	public static class GetDeviceSerialNumber
	{
		public const byte FunctionId = 2;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _serialNumber0;
			private byte _serialNumber1;
			private byte _serialNumber2;
			private byte _serialNumber3;
			private byte _serialNumber4;
			private byte _serialNumber5;
			private byte _serialNumber6;
			private byte _serialNumber7;
			private byte _serialNumber8;
			private byte _serialNumber9;
			private byte _serialNumberA;
			private byte _serialNumberB;

			public string? SerialNumber
			{
				get
				{
					var span = MemoryMarshal.CreateSpan(ref _serialNumber0, 12);

					if (span.IndexOfAnyExcept((byte)0) < 0)
						return null;

					return Encoding.ASCII.GetString(span);
				}
				set => SetSerialNumber(value is not null ? value.AsSpan() : default);
			}

			public bool TryGetSerialNumber(Span<char> destination, out int bytesWritten)
			{
				var span = MemoryMarshal.CreateSpan(ref _serialNumber0, 12);

				if (span.IndexOfAnyExcept((byte)0) < 0 || destination.Length < 12)
				{
					bytesWritten = 0;
					return false;
				}

				Encoding.ASCII.GetChars(span, destination);
				bytesWritten = 12;
				return true;
			}

			public void SetSerialNumber(ReadOnlySpan<char> value)
			{
				var span = MemoryMarshal.CreateSpan(ref _serialNumber0, 12);

				if (value.IsEmpty)
				{
					span.Fill(0);
					return;
				}

				if (value.Length != 12)
					throw new ArgumentException("Serial number must be exactly 12 characters long.");

				for (int i = 0; i < value.Length; i++)
					ValidateChar(value[i]);

				Encoding.ASCII.GetBytes(value, span);
			}

			// The serial number is base 34 encoded in ASCII. (It excludes the letters I and O)
			private static void ValidateChar(char c)
			{
				if (c is < '0' or > 'Z' or > '9' and < 'A' or 'I' or 'O')
					throw new ArgumentException($"Invalid character in serial number: '{c}'.");
			}
		}
	}

	[Flags]
	public enum Transports : ushort
	{
		Bluetooth = 0x0001,
		BluetoothLowEnergy = 0x0002,
		EQuad = 0x0004,
		Usb = 0x0008,
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
