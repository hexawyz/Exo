using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DeviceTools;
using Microsoft.Win32.SafeHandles;

namespace Exo.Devices.Razer;

internal sealed class RazerProtocolTransport : IDisposable
{
	private const uint SetFeatureIoControlCode = 0x88883010;
	private const uint GetFeatureIoControlCode = 0x88883014;

	// The message length is hardcoded to 90 bytes for now. Maybe different devices use a different buffer length.
	private const int HidMessageLength = 90;

	// The message length is hardcoded to 64 bytes + report ID.
	private const int HidBufferLength = HidMessageLength + 1;

	// We don't use a HidFullDuplexStream here, as we communicate with the razer system driver through IO Control codes.
	// This gives access to non-restricted HID GetFeature/SetFeature APIs, unlike the Win32 ones which will do some validation.
	// The validation done by Win32 would not allow us to use HID Get/Set Feature because the report ID needs to be properly declared and mapped to a device interface.
	// And for these devices, this is not the case.
	private readonly SafeFileHandle _deviceHandle;

	private readonly byte[] _buffer;

	private object? _lock;

	public RazerProtocolTransport(SafeFileHandle deviceHandle)
	{
		_deviceHandle = deviceHandle;
		// Allocate 1 read buffer + 1 write buffer with extra capacity for one message.
		_buffer = GC.AllocateArray<byte>(HidBufferLength, true);
		_lock = new();
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _lock, null) is not null)
		{
			_deviceHandle.Dispose();
		}
	}

	private Span<byte> Buffer => MemoryMarshal.CreateFromPinnedArray(_buffer, 0, HidBufferLength).Span;

	private void SetFeature(ReadOnlySpan<byte> buffer) => _deviceHandle.IoControl(SetFeatureIoControlCode, buffer, default);
	private void GetFeature(Span<byte> buffer) => _deviceHandle.IoControl(GetFeatureIoControlCode, default, buffer);

	public bool Handshake()
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		lock (@lock)
		{
			var buffer = Buffer;

			try
			{
				buffer[2] = 0x08;

				buffer[6] = 0x02;
				buffer[7] = 0x00;
				buffer[8] = 0x86;

				UpdateChecksum(buffer);

				SetFeature(buffer);

				return TryReadResponse(buffer, 0x08, 0x00, 0x86, 4);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Clear();
			}
		}
	}

	public void SetBrightness(byte value)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		lock (@lock)
		{
			var buffer = Buffer;

			try
			{
				buffer[2] = 0x1f;
				buffer[6] = 0x03;
				buffer[7] = 0x0f;
				buffer[8] = 0x04;
				buffer[9] = 0x01;
				buffer[11] = value;

				UpdateChecksum(buffer);

				SetFeature(buffer);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Clear();
			}
		}
	}

	public void SetStaticColor(RgbColor color)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		lock (@lock)
		{
			var buffer = Buffer;

			try
			{
				buffer[2] = 0x1f;
				buffer[6] = 0x08;
				buffer[7] = 0x0f;
				buffer[8] = 0x03;

				buffer[14] = color.R;
				buffer[15] = color.G;
				buffer[16] = color.B;

				UpdateChecksum(buffer);

				SetFeature(buffer);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Clear();
			}
		}
	}

	public string GetSerialNumber()
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		lock (@lock)
		{
			var buffer = Buffer;

			try
			{
				buffer[2] = 0x08;

				buffer[6] = 0x16;
				buffer[7] = 0x00;
				buffer[8] = 0x82;

				UpdateChecksum(buffer);

				SetFeature(buffer);

				ReadResponse(buffer, 0x08, 0x00, 0x82, 0);

				var value = buffer[9..^2];

				int length = value.IndexOf((byte)0);

				if (length < 0) length = value.Length;

				return Encoding.ASCII.GetString(value[..length]);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Clear();
			}
		}
	}

	public PairedDeviceInformation[] GetDevicePairingInformation()
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		lock (@lock)
		{
			var buffer = Buffer;

			try
			{
				buffer[2] = 0x08;

				buffer[6] = 0x31;
				buffer[7] = 0x00;
				buffer[8] = 0xbf;

				UpdateChecksum(buffer);

				SetFeature(buffer);

				ReadResponse(buffer, 0x08, 0x00, 0xbf, 0);

				byte deviceCount = buffer[9];

				if (deviceCount < 0) return Array.Empty<PairedDeviceInformation>();
				// We couldn't hold information for more than 26 devices in the 90 bytes-long buffer.
				if (deviceCount > 26) throw new InvalidDataException("The returned number of paired devices appears to be too large.");

				var devices = new PairedDeviceInformation[deviceCount];

				for (int i = 0; i < devices.Length; i++)
				{
					int j = 10 + 3 * i;

					devices[i] = new(buffer[j] == 1, BigEndian.ReadUInt16(buffer[j + 1]));
				}

				return devices;
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Clear();
			}
		}
	}

	private void ReadResponse(Span<byte> buffer, byte commandByte1, byte commandByte2, byte commandByte3, int errorResponseRetryCount)
	{
		if (!TryReadResponse(buffer, commandByte1, commandByte2, commandByte3, errorResponseRetryCount))
		{
			throw new InvalidOperationException("The device did not return a valid response.");
		}
	}

	private bool TryReadResponse(Span<byte> buffer, byte commandByte1, byte commandByte2, byte commandByte3, int errorResponseRetryCount)
	{
		while (true)
		{
			buffer.Clear();

			GetFeature(buffer);
			ValidateChecksum(buffer);

			if (buffer[2] == commandByte1 && buffer[7] == commandByte2 && buffer[8] == commandByte3)
			{
				switch (buffer[1])
				{
				case 0x01:
					continue;
				case 0x02:
					return true;
				case 0x04:
					if (errorResponseRetryCount == 0) return false;
					continue;
				default:
					throw new InvalidDataException("The response could not be decoded properly.");
				}
			}
			else
			{
				throw new InvalidDataException("The response was invalid.");
			}
		}
	}

	// TODO: Improve computations to take into account the written length.
	private static void UpdateChecksum(Span<byte> buffer) => buffer[^2] = Checksum.Xor(buffer[3..^2], 0);

	private static void ValidateChecksum(ReadOnlySpan<byte> buffer)
	{
		if (buffer[^2] != Checksum.Xor(buffer[3..^2], 0)) throw new InvalidDataException("Invalid checksum.");
	}
}
