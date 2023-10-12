using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using DeviceTools;
using Exo.Devices.Razer.LightingEffects;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer;

internal sealed class RazerProtocolTransport : IDisposable
{
	private const int SetFeatureIoControlCode = unchecked((int)0x88883010);
	private const int GetFeatureIoControlCode = unchecked((int)0x88883014);

	// The message length is hardcoded to 90 bytes for now. Maybe different devices use a different buffer length.
	private const int HidMessageLength = 90;

	// The message length is hardcoded to 64 bytes + report ID.
	private const int HidBufferLength = HidMessageLength + 1;

	// We don't use a HidFullDuplexStream here, as we communicate with the razer system driver through IO Control codes.
	// This gives access to non-restricted HID GetFeature/SetFeature APIs, unlike the Win32 ones which will do some validation.
	// The validation done by Win32 would not allow us to use HID Get/Set Feature because the report ID needs to be properly declared and mapped to a device interface.
	// And for these devices, this is not the case.
	private readonly DeviceStream _stream;

	private readonly byte[] _buffer;

	private AsyncLock? _lock;

	public RazerProtocolTransport(DeviceStream stream)
	{
		_stream = stream;
		// Allocate 1 read buffer + 1 write buffer with extra capacity for one message.
		_buffer = GC.AllocateArray<byte>(HidBufferLength, true);
		_lock = new();
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _lock, null) is not null)
		{
			_stream.Dispose();
		}
	}

	private Memory<byte> Buffer => MemoryMarshal.CreateFromPinnedArray(_buffer, 0, HidBufferLength);
	private Span<byte> BufferSpan => Buffer.Span;

	private void SetFeature(ReadOnlySpan<byte> buffer) => _stream.IoControl(SetFeatureIoControlCode, buffer, default);
	private void GetFeature(Span<byte> buffer) => _stream.IoControl(GetFeatureIoControlCode, default, buffer);

	private ValueTask SetFeatureAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
		=> _stream.IoControlAsync(SetFeatureIoControlCode, buffer, cancellationToken);

	private async ValueTask GetFeatureAsync(Memory<byte> buffer, CancellationToken cancellationToken)
		=> await _stream.IoControlAsync(GetFeatureIoControlCode, buffer, cancellationToken).ConfigureAwait(false);

	public async ValueTask<bool> HandshakeAsync(CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer)
			{
				buffer[2] = 0x08;

				buffer[6] = 0x02;
				buffer[7] = (byte)RazerDeviceFeature.General;
				buffer[8] = 0x86;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				return await TryReadResponseAsync(buffer, 0x08, RazerDeviceFeature.General, 0x86, 4, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask SetBrightnessAsync(bool persist, byte value, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, bool persist, byte value)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x03;
				buffer[7] = (byte)RazerDeviceFeature.Lighting;
				buffer[8] = 0x04;

				buffer[9] = persist ? (byte)0x01 : (byte)0x00;

				buffer[11] = value;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, persist, value);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Lighting, 0x04, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<byte> GetBrightnessAsync(CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x01;
				buffer[7] = (byte)RazerDeviceFeature.Lighting;
				buffer[8] = 0x84;

				//buffer[9] = 0x01;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Lighting, 0x84, 0, cancellationToken).ConfigureAwait(false);

				return buffer.Span[11];
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	// NB: I'm really unsure about this one. It could be used entirely wrong, but it seems to return an info we need?
	internal async ValueTask<byte> GetDeviceInformationXxxxxAsync(CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x32;
				buffer[7] = (byte)RazerDeviceFeature.Lighting;
				buffer[8] = 0x80;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Lighting, 0x80, 0, cancellationToken).ConfigureAwait(false);

				return buffer.Span[9];
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<ILightingEffect?> GetSavedEffectAsync(byte flag, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, byte flag)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x0c;
				buffer[7] = (byte)RazerDeviceFeature.Lighting;
				buffer[8] = 0x82;

				buffer[9] = 0x01;
				buffer[10] = flag;

				UpdateChecksum(buffer);
			}

			static ILightingEffect? ParseResponse(ReadOnlySpan<byte> buffer)
			{
				byte colorCount = buffer[14];
				RgbColor color1 = colorCount > 0 ? new(buffer[15], buffer[16], buffer[17]) : default;
				RgbColor color2 = colorCount > 1 ? new(buffer[18], buffer[19], buffer[20]) : default;

				return (RazerLightingEffect)buffer[11] switch
				{
					RazerLightingEffect.Disabled => DisabledEffect.SharedInstance,
					RazerLightingEffect.Static => new StaticColorEffect(color1),
					RazerLightingEffect.Breathing => colorCount switch
					{
						0 => RandomColorPulseEffect.SharedInstance,
						1 => new ColorPulseEffect(color1),
						_ => new TwoColorPulseEffect(color1, color2),
					},
					RazerLightingEffect.SpectrumCycle => ColorCycleEffect.SharedInstance,
					RazerLightingEffect.Wave => ColorWaveEffect.SharedInstance,
					RazerLightingEffect.Reactive => new ReactiveEffect(color1),
					_ => null,
				};
			}

			try
			{
				FillBuffer(buffer.Span, flag);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Lighting, 0x82, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask SetEffectAsync(bool persist, RazerLightingEffect effect, byte colorCount, RgbColor color1, RgbColor color2, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, bool persist, RazerLightingEffect effect, byte colorCount, RgbColor color1, RgbColor color2)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x0c;
				buffer[7] = (byte)RazerDeviceFeature.Lighting;
				buffer[8] = 0x02;

				buffer[9] = persist ? (byte)0x01 : (byte)0x00;
				buffer[10] = 0x00;
				buffer[11] = (byte)effect;
				buffer[12] = 0x01;
				buffer[13] = 0x28;

				if (colorCount > 0)
				{
					buffer[14] = colorCount;

					buffer[15] = color1.R;
					buffer[16] = color1.G;
					buffer[17] = color1.B;
					if (colorCount > 1)
					{
						buffer[18] = color2.R;
						buffer[19] = color2.G;
						buffer[20] = color2.B;
					}
				}

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, persist, effect, colorCount, color1, color2);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Lighting, 0x02, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask SetDynamicColorAsync(RgbColor color, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RgbColor color)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x08;
				buffer[7] = (byte)RazerDeviceFeature.Lighting;
				buffer[8] = 0x03;

				buffer[14] = color.R;
				buffer[15] = color.G;
				buffer[16] = color.B;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, color);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<string> GetSerialNumberAsync(CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer)
			{
				buffer[2] = 0x08;

				buffer[6] = 0x16;
				buffer[7] = (byte)RazerDeviceFeature.General;
				buffer[8] = 0x82;

				UpdateChecksum(buffer);
			}

			static string ParseResponse(ReadOnlySpan<byte> buffer)
			{
				var value = buffer[9..^2];

				int length = value.IndexOf((byte)0);

				if (length < 0) length = value.Length;

				return Encoding.ASCII.GetString(value[..length]);
			}

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x08, (byte)RazerDeviceFeature.General, 0x82, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<DotsPerInch> GetDpiAsync(CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x07;
				buffer[7] = (byte)RazerDeviceFeature.Mouse;
				buffer[8] = 0x85;

				UpdateChecksum(buffer);
			}

			static DotsPerInch ParseResponse(ReadOnlySpan<byte> buffer)
			{
				return new(BigEndian.ReadUInt16(buffer[10]), BigEndian.ReadUInt16(buffer[12]));
			}

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Mouse, 0x85, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask SetDpiAsync(DotsPerInch dpi, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, DotsPerInch dpi)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x07;
				buffer[7] = (byte)RazerDeviceFeature.Mouse;
				buffer[8] = 0x05;

				BigEndian.Write(ref buffer[10], dpi.Horizontal);
				BigEndian.Write(ref buffer[12], dpi.Vertical);

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, dpi);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Mouse, 0x85, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<bool> IsConnectedToExternalPowerAsync(CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x02;
				buffer[7] = (byte)RazerDeviceFeature.Power;
				buffer[8] = 0x84;

				UpdateChecksum(buffer);
			}

			static bool ParseResponse(ReadOnlySpan<byte> buffer) => (buffer[10] & 1) != 0;

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Power, 0x84, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<byte> GetBatteryLevelAsync(CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x02;
				buffer[7] = (byte)RazerDeviceFeature.Power;
				buffer[8] = 0x80;

				UpdateChecksum(buffer);
			}

			static byte ParseResponse(ReadOnlySpan<byte> buffer) => buffer[10];

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Power, 0x80, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<PairedDeviceInformation> GetDeviceInformationAsync(CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x31;
				buffer[7] = (byte)RazerDeviceFeature.General;
				buffer[8] = 0xc5;

				UpdateChecksum(buffer);
			}

			static PairedDeviceInformation ParseResponse(ReadOnlySpan<byte> buffer)
			{
				return new(buffer[9] == 1, BigEndian.ReadUInt16(buffer[10]));
			}

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.General, 0xc5, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<PairedDeviceInformation[]> GetDevicePairingInformationAsync(CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer)
			{
				buffer[2] = 0x08;

				buffer[6] = 0x31;
				buffer[7] = (byte)RazerDeviceFeature.General;
				buffer[8] = 0xbf;

				buffer[9] = 0x10;

				UpdateChecksum(buffer);
			}

			static PairedDeviceInformation[] ParseResponse(ReadOnlySpan<byte> buffer)
			{
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

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x08, RazerDeviceFeature.General, 0xbf, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	private async ValueTask ReadResponseAsync(Memory<byte> buffer, byte commandByte1, RazerDeviceFeature feature, byte commandByte3, int errorResponseRetryCount, CancellationToken cancellationToken)
	{
		if (!await TryReadResponseAsync(buffer, commandByte1, feature, commandByte3, errorResponseRetryCount, cancellationToken).ConfigureAwait(false))
		{
			throw new InvalidOperationException("The device did not return a valid response.");
		}
	}

	private enum ResponseState : byte
	{
		Success = 0,
		MustRetry = 1,
		Failure = 2,
	}

	private async ValueTask<bool> TryReadResponseAsync(Memory<byte> buffer, byte commandByte1, RazerDeviceFeature feature, byte commandByte3, int errorResponseRetryCount, CancellationToken cancellationToken)
	{
		static ResponseState ValidateResponse(Span<byte> buffer, byte commandByte1, RazerDeviceFeature feature, byte commandByte3)
		{
			if (buffer[2] == commandByte1 && buffer[7] == (byte)feature && buffer[8] == commandByte3)
			{
				switch (buffer[1])
				{
				case 0x01:
					return ResponseState.MustRetry;
				case 0x02:
					ValidateChecksum(buffer);
					return ResponseState.Success;
				case 0x04:
					return ResponseState.Failure;
				case 0x05:
					// TODO: Try to analyze the meaning further.
					throw new InvalidOperationException("Unsupported parameter.");
				default:
					throw new InvalidDataException("The response could not be decoded properly.");
				}
			}
			else
			{
				throw new InvalidDataException("The response was invalid.");
			}
		}

		while (true)
		{
			buffer.Span.Clear();

			await GetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

			switch (ValidateResponse(buffer.Span, commandByte1, feature, commandByte3))
			{
			case ResponseState.Success:
				return true;
			case ResponseState.MustRetry:
				break;
			case ResponseState.Failure:
				if (errorResponseRetryCount == 0) return false;
				errorResponseRetryCount--;
				break;
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
