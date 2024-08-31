using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace DeviceTools.HumanInterfaceDevices;

public sealed class HidFullDuplexStream : Stream
{
	private readonly HidDeviceStream _readStream;
	private readonly HidDeviceStream _writeStream;

	public HidFullDuplexStream(string deviceName) : this(deviceName, 4096) { }

	public HidFullDuplexStream(string deviceName, int readBufferSize)
	{
		SafeFileHandle? readHandle = null;
		SafeFileHandle? writeHandle = null;
		HidDeviceStream? readStream = null;
		HidDeviceStream? writeStream = null;
		try
		{
			readHandle = Device.OpenHandle(deviceName, DeviceAccess.Read);
			writeHandle = Device.OpenHandle(deviceName, DeviceAccess.Write);
			readStream = new HidDeviceStream(readHandle, FileAccess.Read, readBufferSize, true);
			writeStream = new HidDeviceStream(writeHandle, FileAccess.Write, 0, true);
		}
		catch
		{
			if (writeStream is not null) writeStream.Dispose();
			else if (writeHandle is not null) writeHandle.Dispose();
			if (readStream is not null) readStream.Dispose();
			else if (readHandle is not null) readHandle.Dispose();
			throw;
		}

		_readStream = readStream;
		_writeStream = writeStream;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_readStream.Dispose();
			_writeStream.Dispose();
		}
	}

#if !NETSTANDARD2_0
	public override async ValueTask DisposeAsync()
	{
		await _readStream.DisposeAsync().ConfigureAwait(false);
		await _writeStream.DisposeAsync().ConfigureAwait(false);
	}
#endif

	/// <summary>Sends a feature report to the HID device.</summary>
	/// <param name="buffer">The buffer containing the feature report, including the report ID byte.</param>
	/// <exception cref="Win32Exception"></exception>
	public void SendFeatureReport(ReadOnlySpan<byte> buffer)
	{
		if (NativeMethods.HidDiscoverySetFeature(_writeStream.SafeFileHandle, ref MemoryMarshal.GetReference(buffer), (uint)buffer.Length) == 0)
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}

	/// <summary>Receives a feature report from the HID device</summary>
	/// <remarks>Before calling this method, the first byte of the buffer must be initialized with the report ID.</remarks>
	/// <param name="buffer">The buffer containing the feature report, including the report ID byte.</param>
	/// <exception cref="Win32Exception"></exception>
	public void ReceiveFeatureReport(Span<byte> buffer)
	{
		if (NativeMethods.HidDiscoveryGetFeature(_readStream.SafeFileHandle, ref MemoryMarshal.GetReference(buffer), (uint)buffer.Length) == 0)
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}

	/// <summary>Sends a feature report to the HID device.</summary>
	/// <param name="buffer">The buffer containing the feature report, including the report ID byte.</param>
	/// <param name="cancellationToken"></param>
	public ValueTask SendFeatureReportAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
		=> _writeStream.SendFeatureReportAsync(buffer, cancellationToken);

	/// <summary>Receives a feature report from the HID device</summary>
	/// <remarks>Before calling this method, the first byte of the buffer must be initialized with the report ID.</remarks>
	/// <param name="buffer">The buffer containing the feature report, including the report ID byte.</param>
	/// <param name="cancellationToken"></param>
	public async ValueTask ReceiveFeatureReportAsync(Memory<byte> buffer, CancellationToken cancellationToken)
		=> await _readStream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);

	public override bool CanRead => true;
	public override bool CanWrite => true;
	public override bool CanSeek => false;

	public override int Read(byte[] buffer, int offset, int count) => _readStream.Read(buffer, offset, count);
	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _readStream.ReadAsync(buffer, offset, count, cancellationToken);
#if !NETSTANDARD2_0
	public override int Read(Span<byte> buffer) => _readStream.Read(buffer);
	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _readStream.ReadAsync(buffer, cancellationToken);
#endif

	public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => _readStream.BeginRead(buffer, offset, count, callback, state);
	public override int EndRead(IAsyncResult asyncResult) => _readStream.EndRead(asyncResult);

	public override void Write(byte[] buffer, int offset, int count) => _writeStream.Write(buffer, offset, count);
	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _writeStream.WriteAsync(buffer, offset, count, cancellationToken);
#if !NETSTANDARD2_0
	public override void Write(ReadOnlySpan<byte> buffer) => _writeStream.Write(buffer);
	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _writeStream.WriteAsync(buffer, cancellationToken);
#endif

	public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => _writeStream.BeginWrite(buffer, offset, count, callback, state);
	public override void EndWrite(IAsyncResult asyncResult) => _writeStream.EndWrite(asyncResult);

	public override void WriteByte(byte value) => _writeStream.WriteByte(value);

	public override void Flush() => _writeStream.Flush();

	public override long Length => throw new NotSupportedException();
	public override long Position
	{
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
	public override void SetLength(long value) => throw new NotSupportedException();

	public ValueTask<string?> GetManufacturerNameAsync(CancellationToken cancellationToken) => _readStream.GetManufacturerNameAsync(cancellationToken);

	public ValueTask<string?> GetProductNameAsync(CancellationToken cancellationToken) => _readStream.GetProductNameAsync(cancellationToken);

	public ValueTask<string?> GetSerialNumberAsync(CancellationToken cancellationToken) => _readStream.GetSerialNumberAsync(cancellationToken);

	public ValueTask<string?> GetStringAsync(int index, CancellationToken cancellationToken) => _readStream.GetStringAsync(index, cancellationToken);

	public ValueTask<HidCollectionDescriptor> GetCollectionDescriptorAsync(CancellationToken cancellationToken) => _readStream.GetCollectionDescriptorAsync(cancellationToken);
}
