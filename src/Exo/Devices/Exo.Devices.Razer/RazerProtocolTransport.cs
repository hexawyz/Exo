using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.ColorFormats;
using Exo.Devices.Razer.LightingEffects;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer;

// This is an implementation based on Razer's driver.
internal sealed class RzControlRazerProtocolTransport : RazerProtocolTransport
{
	private const int SetFeatureIoControlCode = unchecked((int)0x88883010);
	private const int GetFeatureIoControlCode = unchecked((int)0x88883014);

	public RzControlRazerProtocolTransport(DeviceStream stream) : base(stream) { }

	protected override ValueTask SetFeatureAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
		=> Stream.IoControlAsync(SetFeatureIoControlCode, buffer, cancellationToken);

	protected override async ValueTask GetFeatureAsync(Memory<byte> buffer, CancellationToken cancellationToken)
		=> await Stream.IoControlAsync(GetFeatureIoControlCode, buffer, cancellationToken).ConfigureAwait(false);
}

// This is an implementation based on the native HID stack.
internal sealed class HidRazerProtocolTransport : RazerProtocolTransport
{
	public HidRazerProtocolTransport(HidDeviceStream stream) : base(stream) { }

	private new HidDeviceStream Stream => Unsafe.As<HidDeviceStream>(base.Stream);

	protected override ValueTask SetFeatureAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
		=> Stream.SendFeatureReportAsync(buffer, cancellationToken);

	protected override ValueTask GetFeatureAsync(Memory<byte> buffer, CancellationToken cancellationToken)
		=> Stream.ReceiveFeatureReportAsync(buffer, cancellationToken);
}

internal abstract class RazerProtocolTransport : IDisposable, IRazerProtocolTransport
{
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

	protected DeviceStream Stream => _stream;

	private Memory<byte> Buffer => MemoryMarshal.CreateFromPinnedArray(_buffer, 0, HidBufferLength);
	private Span<byte> BufferSpan => Buffer.Span;

	protected abstract ValueTask SetFeatureAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
	protected abstract ValueTask GetFeatureAsync(Memory<byte> buffer, CancellationToken cancellationToken);

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

	public async Task SetBrightnessV1Async(RazerLedId ledId, byte value, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId, byte value)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x03;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x03;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				buffer[11] = value;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, ledId, value);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x03, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<byte> GetBrightnessV1Async(RazerLedId ledId, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x03;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x83;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, ledId);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x83, 0, cancellationToken).ConfigureAwait(false);

				return buffer.Span[11];
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task EnableLedV1Async(RazerLedId ledId, bool enable, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId, bool enable)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x03;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x00;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				buffer[11] = enable ? (byte)0x01 : (byte)0x00;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, ledId, enable);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x00, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<bool> IsLedEnabledV1Async(RazerLedId ledId, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x03;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x80;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, ledId);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x80, 0, cancellationToken).ConfigureAwait(false);

				return buffer.Span[11] != 0;
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetStaticColorV1Async(RazerLedId ledId, RgbColor color, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId, RgbColor color)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x05;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x01;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				buffer[11] = color.R;
				buffer[12] = color.G;
				buffer[13] = color.B;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, ledId, color);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x01, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetEffectV1Async(RazerLedId ledId, RazerLightingEffectV1 effect, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId, RazerLightingEffectV1 effect)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x03;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x02;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				buffer[11] = (byte)effect;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, ledId, effect);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x02, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetBreathingEffectParametersV1Async(RazerLedId ledId, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x03;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x0E;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				buffer[11] = 0x03;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, ledId);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x0E, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetBreathingEffectParametersV1Async(RazerLedId ledId, RgbColor color, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId, RgbColor color)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x06;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x0E;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				buffer[11] = 1;

				buffer[12] = color.R;
				buffer[13] = color.G;
				buffer[14] = color.B;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, ledId, color);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x0E, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetBreathingEffectParametersV1Async(RazerLedId ledId, RgbColor color1, RgbColor color2, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId, RgbColor color1, RgbColor color2)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x09;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x0E;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				buffer[11] = 2;

				buffer[12] = color1.R;
				buffer[13] = color1.G;
				buffer[14] = color1.B;

				buffer[15] = color2.R;
				buffer[16] = color2.G;
				buffer[17] = color2.B;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, ledId, color1, color2);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x0E, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetSynchronizedLightingV1Async(RazerLedId ledId, bool enable, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId, bool enable)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x03;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x0F;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				buffer[11] = enable ? (byte)0x01 : (byte)0x00;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, ledId, enable);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x0F, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<bool> IsSynchronizedLightingEnabledV1Async(RazerLedId ledId, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x03;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x8F;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, ledId);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x8F, 0, cancellationToken).ConfigureAwait(false);

				return buffer.Span[11] != 0;
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetBrightnessV2Async(bool persist, byte value, CancellationToken cancellationToken)
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
				buffer[7] = (byte)RazerDeviceFeature.LightingV2;
				buffer[8] = 0x04;

				buffer[9] = persist ? (byte)0x01 : (byte)0x00;

				buffer[11] = value;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, persist, value);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV2, 0x04, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<byte> GetBrightnessV2Async(bool persisted, RazerLedId ledId, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, bool persisted, RazerLedId ledId)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x01;
				buffer[7] = (byte)RazerDeviceFeature.LightingV2;
				buffer[8] = 0x84;

				buffer[9] = persisted ? (byte)0x01 : (byte)0x00;
				buffer[10] = (byte)ledId;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, persisted, ledId);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV2, 0x84, 0, cancellationToken).ConfigureAwait(false);

				return buffer.Span[11];
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}


	public async Task SetCurrentDpiPresetAsync(bool persist, byte index, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, bool persist, byte index)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x02;
				buffer[7] = (byte)RazerDeviceFeature.Mouse;
				buffer[8] = 0x04;

				buffer[9] = persist ? (byte)0x01 : (byte)0x00;

				buffer[10] = index;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, persist, index);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Mouse, 0x04, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<byte> GetCurrentDpiPresetAsync(bool persisted, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, bool persisted)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x02;
				buffer[7] = (byte)RazerDeviceFeature.Mouse;
				buffer[8] = 0x84;

				buffer[9] = persisted ? (byte)0x01 : (byte)0x00;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, persisted);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Mouse, 0x84, 0, cancellationToken).ConfigureAwait(false);

				return buffer.Span[10];
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	[StructLayout(LayoutKind.Explicit, Size = 7)]
	private struct RawDpiProfileBigEndian
	{
		[FieldOffset(0)]
		public byte Index;
		[FieldOffset(1)]
		private ushort _dpiX;
		[FieldOffset(3)]
		private ushort _dpiY;
		// NB: Not used; Can be truncated in device responses, so this could read out of buffer.
		[FieldOffset(5)]
		private ushort _dpiZ;

		public ushort DpiX
		{
			readonly get => BigEndian.ReadUInt16(in Unsafe.As<ushort, byte>(ref Unsafe.AsRef(in _dpiX)));
			set => BigEndian.Write(ref Unsafe.As<ushort, byte>(ref _dpiX), value);
		}

		public ushort DpiY
		{
			readonly get => BigEndian.ReadUInt16(in Unsafe.As<ushort, byte>(ref Unsafe.AsRef(in _dpiY)));
			set => BigEndian.Write(ref Unsafe.As<ushort, byte>(ref _dpiY), value);
		}

		public ushort DpiZ
		{
			readonly get => BigEndian.ReadUInt16(in Unsafe.As<ushort, byte>(ref Unsafe.AsRef(in _dpiZ)));
			set => BigEndian.Write(ref Unsafe.As<ushort, byte>(ref _dpiZ), value);
		}
	}

	[StructLayout(LayoutKind.Explicit, Size = 7)]
	private struct RawDpiProfileLittleEndian
	{
		[FieldOffset(0)]
		public byte Index;
		[FieldOffset(1)]
		private ushort _dpiX;
		[FieldOffset(3)]
		private ushort _dpiY;
		// NB: Not used; Can be truncated in device responses, so this could read out of buffer.
		[FieldOffset(5)]
		private ushort _dpiZ;

		public ushort DpiX
		{
			readonly get => LittleEndian.ReadUInt16(in Unsafe.As<ushort, byte>(ref Unsafe.AsRef(in _dpiX)));
			set => LittleEndian.Write(ref Unsafe.As<ushort, byte>(ref _dpiX), value);
		}

		public ushort DpiY
		{
			readonly get => LittleEndian.ReadUInt16(in Unsafe.As<ushort, byte>(ref Unsafe.AsRef(in _dpiY)));
			set => LittleEndian.Write(ref Unsafe.As<ushort, byte>(ref _dpiY), value);
		}

		public ushort DpiZ
		{
			readonly get => LittleEndian.ReadUInt16(in Unsafe.As<ushort, byte>(ref Unsafe.AsRef(in _dpiZ)));
			set => LittleEndian.Write(ref Unsafe.As<ushort, byte>(ref _dpiZ), value);
		}
	}

	public static RazerMouseDpiProfileConfiguration ParseDpiPresetConfiguration(ReadOnlySpan<byte> buffer, bool bigEndian, bool validateProfileIndices)
	{
		byte profileCount = buffer[1];

		// In the 80 available bytes, we could only retrieve up to 11 profiles exactly.
		if (profileCount > 11) throw new InvalidDataException("Returned profile count is too big.");

		var profiles = new RazerMouseDpiPreset[profileCount];

		// In the Bluetooth protocol, it seems that the data can be truncated by one byte if we assume profiles to be 7 bytes.
		// Maybe the command I reverse-engineered is wrong somehow, or it is an expected behavior.
		// Anyway, we do the computation below assuming that the profiles are 5 bytes separated by two filler bytes that we don't use.
		// In that way, the "missing" bytes from the Bluetooth response don't matter as much.
		int expectedDataLength = 2 + 5 * profileCount + (profileCount > 0 ? 2 * (profileCount - 1) : 0);
		if (expectedDataLength > buffer.Length) throw new InvalidDataException("Invalid data length");

		ref readonly byte currentRawProfile = ref buffer[2];
		ref readonly byte bufferEnd = ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(buffer), buffer.Length);
		for (int i = 0; i < profileCount; i++)
		{
			// For some reason, it seems that the USB-based protocol uses big endian, while the Bluetooth protocol uses little endian values…
			// I can't really make a lot of sense of this, but it is what it is.
			if (bigEndian)
			{
				ref readonly var rawProfile = ref Unsafe.As<byte, RawDpiProfileBigEndian>(ref Unsafe.AsRef(in currentRawProfile));
				if (validateProfileIndices && rawProfile.Index != i + 1) throw new InvalidDataException("Unexpected profile index. Expected a contiguous sequence starting from 1.");
				profiles[i] = new(rawProfile.DpiX, rawProfile.DpiY, Unsafe.ByteOffset(in currentRawProfile, in bufferEnd) >= 7 ? rawProfile.DpiZ : (ushort)0);
			}
			else
			{
				ref readonly var rawProfile = ref Unsafe.As<byte, RawDpiProfileLittleEndian>(ref Unsafe.AsRef(in currentRawProfile));
				if (validateProfileIndices && rawProfile.Index != i + 1) throw new InvalidDataException("Unexpected profile index. Expected a contiguous sequence starting from 1.");
				profiles[i] = new(rawProfile.DpiX, rawProfile.DpiY, Unsafe.ByteOffset(in currentRawProfile, in bufferEnd) >= 7 ? rawProfile.DpiZ : (ushort)0);
			}
			currentRawProfile = ref Unsafe.Add(ref Unsafe.AsRef(in currentRawProfile), 7);
		}

		return new(buffer[0], ImmutableCollectionsMarshal.AsImmutableArray(profiles));
	}

	public static int WritePresetConfiguration(Span<byte> buffer, bool bigEndian, bool emitProfileIndices, RazerMouseDpiProfileConfiguration configuration)
	{
		int expectedDataLength = 2 + 7 * configuration.Presets.Length;

		if (configuration.Presets.Length > 11 || expectedDataLength > buffer.Length) throw new ArgumentException("Too many profiles specified.");
		if (configuration.ActivePresetIndex == 0 | configuration.ActivePresetIndex > configuration.Presets.Length) throw new ArgumentException("Active profile index is out of range.");

		buffer[0] = emitProfileIndices ? configuration.ActivePresetIndex : (byte)0;
		buffer[1] = (byte)configuration.Presets.Length;

		var profiles = configuration.Presets;
		ref byte currentRawProfile = ref buffer[2];
		for (int i = 0; i < profiles.Length; i++)
		{
			ref readonly var profile = ref ImmutableCollectionsMarshal.AsArray(profiles)![i];
			if (bigEndian)
			{
				ref var rawProfile = ref Unsafe.As<byte, RawDpiProfileBigEndian>(ref Unsafe.AsRef(in currentRawProfile));
				rawProfile.Index = emitProfileIndices ? (byte)(i + 1) : (byte)0;
				rawProfile.DpiX = profile.X;
				rawProfile.DpiY = profile.Y;
				rawProfile.DpiZ = profile.Z;
			}
			else
			{
				ref var rawProfile = ref Unsafe.As<byte, RawDpiProfileLittleEndian>(ref Unsafe.AsRef(in currentRawProfile));
				rawProfile.Index = emitProfileIndices ? (byte)(i + 1) : (byte)0;
				rawProfile.DpiX = profile.X;
				rawProfile.DpiY = profile.Y;
				rawProfile.DpiZ = profile.Z;
			}
			currentRawProfile = ref Unsafe.Add(ref Unsafe.AsRef(in currentRawProfile), 7);
		}
		return expectedDataLength;
	}

	private async ValueTask<RazerMouseDpiProfileConfiguration> GetDpiPresetsAsync(byte communicationId, byte functionId, bool validatePresetIndices, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, byte communicationId, byte functionId, bool persisted)
			{
				buffer[2] = communicationId;

				buffer[6] = 0x26;
				buffer[7] = (byte)RazerDeviceFeature.Mouse;
				buffer[8] = functionId;

				buffer[9] = persisted ? (byte)0x01 : (byte)0x00;

				UpdateChecksum(buffer);
			}

			static RazerMouseDpiProfileConfiguration ReadResponse(Span<byte> buffer, bool validatePresetIndices)
				=> ParseDpiPresetConfiguration(buffer[10..], true, validatePresetIndices);

			try
			{
				FillBuffer(buffer.Span, communicationId, functionId, true);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);
				await ReadResponseAsync(buffer, communicationId, RazerDeviceFeature.Mouse, functionId, 0, cancellationToken).ConfigureAwait(false);

				return ReadResponse(buffer.Span, validatePresetIndices);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	private async Task SetDpiPresetsAsync(byte communicationId, byte functionId, bool persist, bool emitProfileIndices, RazerMouseDpiProfileConfiguration configuration, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, byte communicationId, byte functionId, bool persisted, bool emitProfileIndices, RazerMouseDpiProfileConfiguration configuration)
			{
				buffer[2] = communicationId;

				buffer[6] = 0x26;
				buffer[7] = (byte)RazerDeviceFeature.Mouse;
				buffer[8] = functionId;

				buffer[9] = persisted ? (byte)0x01 : (byte)0x00;

				WritePresetConfiguration(buffer[10..^2], true, emitProfileIndices, configuration);

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, communicationId, functionId, persist, emitProfileIndices, configuration);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);
				await ReadResponseAsync(buffer, communicationId, RazerDeviceFeature.Mouse, functionId, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public ValueTask<RazerMouseDpiProfileConfiguration> GetDpiPresetsV1Async(CancellationToken cancellationToken)
		=> GetDpiPresetsAsync(0x3F, 0x83, false, cancellationToken);

	public Task SetDpiPresetsV1Async(bool persist, RazerMouseDpiProfileConfiguration configuration, CancellationToken cancellationToken)
		=> SetDpiPresetsAsync(0x3F, 0x03, persist, false, configuration, cancellationToken);

	public ValueTask<RazerMouseDpiProfileConfiguration> GetDpiPresetsV2Async(CancellationToken cancellationToken)
		=> GetDpiPresetsAsync(0x1F, 0x86, false, cancellationToken);

	public Task SetDpiPresetsV2Async(bool persist, RazerMouseDpiProfileConfiguration configuration, CancellationToken cancellationToken)
		=> SetDpiPresetsAsync(0x1F, 0x06, persist, true, configuration, cancellationToken);

	// NB: I'm really unsure about this one. It could be used entirely wrong, but it seems to return an info we need?
	public async ValueTask<ImmutableArray<RazerLedId>> GetLightingZoneIdsAsync(CancellationToken cancellationToken)
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
				buffer[7] = (byte)RazerDeviceFeature.LightingV2;
				buffer[8] = 0x80;

				UpdateChecksum(buffer);
			}

			static ImmutableArray<RazerLedId> ParseResponse(Span<byte> buffer)
			{
				uint itemCount = (uint)buffer.Length / 5;
				var ledIds = new RazerLedId[(int)itemCount];
				for (int i = 0; i < ledIds.Length; i++)
				{
					ledIds[i] = (RazerLedId)buffer[5 * i];
				}
				return ImmutableCollectionsMarshal.AsImmutableArray(ledIds);
			}

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV2, 0x80, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span.Slice(9, buffer.Span[6]));
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public static ILightingEffect? ParseEffectV1Async(ReadOnlySpan<byte> buffer)
	{
		var effect = (RazerLightingEffectV1)buffer[0];
		byte parameter = buffer[1];
		RgbColor color1 = parameter > 0 && effect is RazerLightingEffectV1.Static or RazerLightingEffectV1.Breathing or RazerLightingEffectV1.Reactive ? new(buffer[2], buffer[3], buffer[4]) : default;
		RgbColor color2 = parameter > 1 && effect is RazerLightingEffectV1.Breathing ? new(buffer[5], buffer[6], buffer[7]) : default;

		return effect switch
		{
			RazerLightingEffectV1.Disabled => DisabledEffect.SharedInstance,
			RazerLightingEffectV1.Static => new StaticColorEffect(color1),
			RazerLightingEffectV1.Breathing => parameter switch
			{
				0 => RandomColorPulseEffect.SharedInstance,
				1 => new ColorPulseEffect(color1),
				_ => new TwoColorPulseEffect(color1, color2),
			},
			RazerLightingEffectV1.SpectrumCycle => SpectrumCycleEffect.SharedInstance,
			RazerLightingEffectV1.Wave => SpectrumWaveEffect.SharedInstance,
			RazerLightingEffectV1.Reactive => new ReactiveEffect(color1),
			_ => null,
		};
	}

	// TODO: That was written a bit hastily. Because each effect actually requires a slightly different structure, rework this later.
	public static byte WriteEffectV1Async(Span<byte> buffer, RazerLightingEffectV1 effect, byte parameter, RgbColor color1, RgbColor color2)
	{
		buffer[0] = (byte)effect;

		if (effect is RazerLightingEffectV1.Disabled or RazerLightingEffectV1.SpectrumCycle) return 1;

		if (effect is RazerLightingEffectV1.Static)
		{
			buffer[1] = color1.R;
			buffer[2] = color1.G;
			buffer[3] = color1.B;
			return 4;
		}

		buffer[1] = parameter;

		if (parameter > 0 && effect is RazerLightingEffectV1.Static or RazerLightingEffectV1.Breathing or RazerLightingEffectV1.Reactive)
		{
			if (parameter > 2 && effect is RazerLightingEffectV1.Breathing) return 2;

			buffer[2] = color1.R;
			buffer[3] = color1.G;
			buffer[4] = color1.B;
			if (parameter > 1 && effect is RazerLightingEffectV1.Breathing)
			{
				buffer[5] = color2.R;
				buffer[6] = color2.G;
				buffer[7] = color2.B;
				return 8;
			}
			return 5;
		}
		return 2;
	}

	// NB: Reading does not seem to be actually possible ?
	public async ValueTask<ILightingEffect?> GetSavedEffectV1Async(CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x08;
				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x8a;

				UpdateChecksum(buffer);
			}

			static ILightingEffect? ParseResponse(ReadOnlySpan<byte> buffer) => ParseV2Effect(buffer[9..]);

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x8a, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetEffectV1Async(RazerLightingEffectV1 effect, byte parameter, RgbColor color1, RgbColor color2, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLightingEffectV1 effect, byte parameter, RgbColor color1, RgbColor color2)
			{
				buffer[2] = 0x1f;

				buffer[7] = (byte)RazerDeviceFeature.LightingV1;
				buffer[8] = 0x0a;

				buffer[6] = WriteEffectV1Async(buffer[9..], effect, parameter, color1, color2);

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, effect, parameter, color1, color2);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV1, 0x0a, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public static ILightingEffect? ParseV2Effect(ReadOnlySpan<byte> buffer)
	{
		byte colorCount = buffer[3];
		RgbColor color1 = colorCount > 0 ? new(buffer[4], buffer[5], buffer[6]) : default;
		RgbColor color2 = colorCount > 1 ? new(buffer[7], buffer[8], buffer[9]) : default;

		return (RazerLightingEffectV2)buffer[0] switch
		{
			RazerLightingEffectV2.Disabled => DisabledEffect.SharedInstance,
			RazerLightingEffectV2.Static => new StaticColorEffect(color1),
			RazerLightingEffectV2.Breathing => colorCount switch
			{
				0 => RandomColorPulseEffect.SharedInstance,
				1 => new ColorPulseEffect(color1),
				_ => new TwoColorPulseEffect(color1, color2),
			},
			RazerLightingEffectV2.SpectrumCycle => SpectrumCycleEffect.SharedInstance,
			RazerLightingEffectV2.Wave => SpectrumWaveEffect.SharedInstance,
			RazerLightingEffectV2.Reactive => new ReactiveEffect(color1),
			_ => null,
		};
	}

	public static int WriteV2Effect(Span<byte> buffer, RazerLightingEffectV2 effect, byte colorCount, RgbColor color1, RgbColor color2)
	{
		buffer[0] = (byte)effect;
		buffer[1] = 0x01;
		buffer[2] = 0x28;

		if (colorCount > 0)
		{
			buffer[3] = colorCount;

			buffer[4] = color1.R;
			buffer[5] = color1.G;
			buffer[6] = color1.B;
			if (colorCount > 1)
			{
				buffer[7] = color2.R;
				buffer[8] = color2.G;
				buffer[9] = color2.B;
				return 10;
			}
			return 7;
		}
		return 3;
	}

	public async ValueTask<ILightingEffect?> GetSavedEffectV2Async(RazerLedId ledId, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, RazerLedId ledId)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x0c;
				buffer[7] = (byte)RazerDeviceFeature.LightingV2;
				buffer[8] = 0x82;

				buffer[9] = 0x01;
				buffer[10] = (byte)ledId;

				UpdateChecksum(buffer);
			}

			static ILightingEffect? ParseResponse(ReadOnlySpan<byte> buffer) => ParseV2Effect(buffer[11..]);

			try
			{
				FillBuffer(buffer.Span, ledId);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV2, 0x82, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetEffectV2Async(bool persist, RazerLightingEffectV2 effect, byte colorCount, RgbColor color1, RgbColor color2, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, bool persist, RazerLightingEffectV2 effect, byte colorCount, RgbColor color1, RgbColor color2)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x0c;
				buffer[7] = (byte)RazerDeviceFeature.LightingV2;
				buffer[8] = 0x02;

				buffer[9] = persist ? (byte)0x01 : (byte)0x00;
				buffer[10] = 0x00;

				WriteV2Effect(buffer[11..], effect, colorCount, color1, color2);

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, persist, effect, colorCount, color1, color2);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.LightingV2, 0x02, 0, cancellationToken).ConfigureAwait(false);
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
				buffer[7] = (byte)RazerDeviceFeature.LightingV2;
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

	public async ValueTask<string> GetDockSerialNumberAsync(CancellationToken cancellationToken)
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
				buffer[8] = 0x92;

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

				await ReadResponseAsync(buffer, 0x08, (byte)RazerDeviceFeature.General, 0x92, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public static DotsPerInch ParseDpi(ReadOnlySpan<byte> buffer, bool bigEndian)
		=> bigEndian ?
			new(BigEndian.ReadUInt16(buffer[0]), BigEndian.ReadUInt16(buffer[2])) :
			new(LittleEndian.ReadUInt16(buffer[0]), LittleEndian.ReadUInt16(buffer[2]));

	public static void WriteDpi(Span<byte> buffer, DotsPerInch dpi, bool bigEndian)
	{
		if (bigEndian)
		{
			BigEndian.Write(ref buffer[0], dpi.Horizontal);
			BigEndian.Write(ref buffer[2], dpi.Vertical);
			BigEndian.Write(ref buffer[4], 0);
		}
		else
		{
			LittleEndian.Write(ref buffer[0], dpi.Horizontal);
			LittleEndian.Write(ref buffer[2], dpi.Vertical);
			LittleEndian.Write(ref buffer[4], 0);
		}
	}

	public async ValueTask<DotsPerInch> GetDpiAsync(bool persisted, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, bool persisted)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x07;
				buffer[7] = (byte)RazerDeviceFeature.Mouse;
				buffer[8] = 0x85;

				buffer[9] = persisted ? (byte)1 : (byte)0;

				UpdateChecksum(buffer);
			}

			static DotsPerInch ParseResponse(ReadOnlySpan<byte> buffer)
				=> ParseDpi(buffer[10..], true);

			try
			{
				FillBuffer(buffer.Span, persisted);

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

	public async Task SetDpiAsync(bool persist, DotsPerInch dpi, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, bool persist, DotsPerInch dpi)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x07;
				buffer[7] = (byte)RazerDeviceFeature.Mouse;
				buffer[8] = 0x05;

				buffer[9] = persist ? (byte)1 : (byte)0;

				WriteDpi(buffer[10..], dpi, true);

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, persist, dpi);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Mouse, 0x05, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}


	public async ValueTask<byte> GetPollingIntervalAsync(CancellationToken cancellationToken)
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
				buffer[7] = (byte)RazerDeviceFeature.General;
				buffer[8] = 0x85;

				UpdateChecksum(buffer);
			}

			static byte ParseResponse(ReadOnlySpan<byte> buffer)
				=> buffer[9];

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.General, 0x85, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetPollingIntervalAsync(byte interval, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, byte divider)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x01;
				buffer[7] = (byte)RazerDeviceFeature.General;
				buffer[8] = 0x05;

				buffer[9] = divider;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, interval);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.General, 0x05, 0, cancellationToken).ConfigureAwait(false);
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

	public async ValueTask<byte> GetLowPowerThresholdAsync(CancellationToken cancellationToken)
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
				buffer[7] = (byte)RazerDeviceFeature.Power;
				buffer[8] = 0x81;

				UpdateChecksum(buffer);
			}

			static byte ParseResponse(ReadOnlySpan<byte> buffer) => buffer[9];

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Power, 0x81, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetLowPowerThresholdAsync(byte value, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, byte value)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x01;
				buffer[7] = (byte)RazerDeviceFeature.Power;
				buffer[8] = 0x01;

				buffer[9] = value;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, value);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Power, 0x01, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<ushort> GetIdleTimerAsync(CancellationToken cancellationToken)
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
				buffer[8] = 0x83;

				UpdateChecksum(buffer);
			}

			static ushort ParseResponse(ReadOnlySpan<byte> buffer) => BigEndian.ReadUInt16(in buffer[9]);

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Power, 0x83, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async Task SetIdleTimerAsync(ushort value, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, ushort value)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x01;
				buffer[7] = (byte)RazerDeviceFeature.Power;
				buffer[8] = 0x03;

				BigEndian.Write(ref buffer[9], value);

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, value);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Power, 0x03, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<Version> GetFirmwareVersionAsync(CancellationToken cancellationToken)
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
				buffer[8] = 0x87;

				UpdateChecksum(buffer);
			}

			static Version ParseResponse(ReadOnlySpan<byte> buffer)
				=> new Version(buffer[0], buffer[1], buffer[2], buffer[3]);

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.General, 0x87, 0, cancellationToken).ConfigureAwait(false);

				return ParseResponse(buffer.Span.Slice(9, 4));
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

	public async ValueTask<byte> GetSensorStateAsync(byte parameter1, byte parameter2, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, byte parameter1, byte parameter2)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x03;
				buffer[7] = (byte)RazerDeviceFeature.Sensor;
				buffer[8] = 0x83;

				buffer[9] = parameter1;
				buffer[10] = parameter2;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, parameter1, parameter2);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Sensor, 0x83, 0, cancellationToken).ConfigureAwait(false);

				return buffer.Span[11];
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask SetSensorStateAsync(byte parameter1, byte parameter2, byte value, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, byte parameter1, byte parameter2, byte value)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x03;
				buffer[7] = (byte)RazerDeviceFeature.Sensor;
				buffer[8] = 0x03;

				buffer[9] = parameter1;
				buffer[10] = parameter2;
				buffer[11] = value;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, parameter1, parameter2, value);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.Sensor, 0x03, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask<byte> GetDeviceModeAsync(CancellationToken cancellationToken)
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
				buffer[7] = (byte)RazerDeviceFeature.General;
				buffer[8] = 0x84;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.General, 0x84, 0, cancellationToken).ConfigureAwait(false);

				return buffer.Span[9];
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	public async ValueTask SetDeviceModeAsync(byte mode, CancellationToken cancellationToken)
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerProtocolTransport));
		using (await @lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = Buffer;

			static void FillBuffer(Span<byte> buffer, byte mode)
			{
				buffer[2] = 0x1f;

				buffer[6] = 0x02;
				buffer[7] = (byte)RazerDeviceFeature.General;
				buffer[8] = 0x04;

				buffer[9] = mode;

				UpdateChecksum(buffer);
			}

			try
			{
				FillBuffer(buffer.Span, mode);

				await SetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

				await ReadResponseAsync(buffer, 0x1f, RazerDeviceFeature.General, 0x04, 0, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// TODO: Improve computations to take into account the written length.
				buffer.Span.Clear();
			}
		}
	}

	private async Task ReadResponseAsync(Memory<byte> buffer, byte id, RazerDeviceFeature feature, byte function, int errorResponseRetryCount, CancellationToken cancellationToken)
	{
		if (!await TryReadResponseAsync(buffer, id, feature, function, errorResponseRetryCount, cancellationToken).ConfigureAwait(false))
		{
			throw new InvalidOperationException("The device did not return a valid response.");
		}
	}

	private enum ResponseState : byte
	{
		Success = 0,
		MustRetry = 1,
		Failure = 2,
		DeviceNotConnected = 3,
	}

	private async ValueTask<bool> TryReadResponseAsync(Memory<byte> buffer, byte id, RazerDeviceFeature feature, byte function, int errorResponseRetryCount, CancellationToken cancellationToken)
	{
		static ResponseState ValidateResponse(Span<byte> buffer, byte id, RazerDeviceFeature feature, byte function)
		{
			if (buffer[2] == id && buffer[7] == (byte)feature && buffer[8] == function)
			{
				switch (buffer[1])
				{
				case 0x01:
					return ResponseState.MustRetry;
				case 0x02:
					ValidateChecksum(buffer);
					return ResponseState.Success;
				case 0x03:
					return ResponseState.DeviceNotConnected;
				case 0x04:
					return ResponseState.Failure;
				case 0x05:
					// TODO: Try to analyze the meaning further.
					throw new InvalidOperationException("Unsupported parameter.");
				default:
					throw new InvalidDataException($"The response could not be decoded properly. Unsupported status code: {buffer[1]}.");
				}
			}
			else
			{
				throw new InvalidDataException("The response was invalid.");
			}
		}

		// Try to wait for about 4ms. Hopefully this will not be too broken with Windows' internal timer. (But with chrome running on everyone's computer, I assume resolution is always 1ms)
		// For my testing, waiting this delay make most reads succeed on the first try, which is what we want. (NB: Using Wireshark "time delta from previous displayed frame" feature is useful)
		await Task.Delay(4, cancellationToken).ConfigureAwait(false);

		while (true)
		{
			buffer.Span.Clear();

			await GetFeatureAsync(buffer, cancellationToken).ConfigureAwait(false);

			switch (ValidateResponse(buffer.Span, id, feature, function))
			{
			case ResponseState.Success:
				return true;
			case ResponseState.MustRetry:
				break;
			case ResponseState.DeviceNotConnected:
				return false;
			case ResponseState.Failure:
				if (errorResponseRetryCount == 0) return false;
				errorResponseRetryCount--;
				break;
			}

			// Arbitrarily wait between read tentatives
			// From my recent captures, we have many read retries on some commands, amounting to about 10ms in some cases,
			// however sometimes the response is relatively quick and we get the result immediately.
			// As such, ensuring a retry delay of 6ms should be mostly fair, and avoid wasting CPU unnecessarily.
			await Task.Delay(6, cancellationToken).ConfigureAwait(false);
		}
	}

	// TODO: Improve computations to take into account the written length.
	private static void UpdateChecksum(Span<byte> buffer) => buffer[^2] = Checksum.Xor(buffer[3..^2], 0);

	private static void ValidateChecksum(ReadOnlySpan<byte> buffer)
	{
		if (buffer[^2] != Checksum.Xor(buffer[3..^2], 0)) throw new InvalidDataException("Invalid checksum.");
	}
}
