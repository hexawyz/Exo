using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DeviceTools.HumanInterfaceDevices;

public class HidDeviceStream : DeviceStream
{
	private StrongBox<NativeMethods.HidCollectionInformation>? _hidCollectionInformation;

	public HidDeviceStream(SafeFileHandle handle, FileAccess access) : base(handle, access)
	{
	}

	public HidDeviceStream(string path, FileMode mode) : base(path, mode)
	{
	}

#if NET6_0_OR_GREATER
	public HidDeviceStream(string path, FileStreamOptions options) : base(path, options)
	{
	}
#endif

	public HidDeviceStream(SafeFileHandle handle, FileAccess access, int bufferSize) : base(handle, access, bufferSize)
	{
	}

	public HidDeviceStream(string path, FileMode mode, FileAccess access) : base(path, mode, access)
	{
	}

	public HidDeviceStream(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync) : base(handle, access, bufferSize, isAsync)
	{
	}

	public HidDeviceStream(string path, FileMode mode, FileAccess access, FileShare share) : base(path, mode, access, share)
	{
	}

	public HidDeviceStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize) : base(path, mode, access, share, bufferSize)
	{
	}

	public HidDeviceStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync) : base(path, mode, access, share, bufferSize, useAsync)
	{
	}

	public HidDeviceStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options) : base(path, mode, access, share, bufferSize, options)
	{
	}

	public ValueTask<string?> GetManufacturerNameAsync(CancellationToken cancellationToken)
		=> GetStringAsync(NativeMethods.IoCtlGetManufacturerString, default, cancellationToken);

	public ValueTask<string?> GetProductNameAsync(CancellationToken cancellationToken)
		=> GetStringAsync(NativeMethods.IoCtlGetProductString, default, cancellationToken);

	public ValueTask<string?> GetSerialNumberAsync(CancellationToken cancellationToken)
		=> GetStringAsync(NativeMethods.IoCtlGetSerialNumberString, default, cancellationToken);

	public ValueTask<string?> GetStringAsync(int index, CancellationToken cancellationToken)
	{
		var input = new byte[4];
		Unsafe.As<byte, int>(ref input[0]) = index;
		return GetStringAsync(NativeMethods.IoCtlGetIndexedString, input, cancellationToken);
	}

	private async ValueTask<string?> GetStringAsync(int ioctl, ReadOnlyMemory<byte> input, CancellationToken cancellationToken)
	{
		int bufferLength = 512;
		while (true)
		{
			var buffer = ArrayPool<byte>.Shared.Rent(bufferLength);
			buffer[0] = 0;
			buffer[1] = 0;
			try
			{
				// Buffer length should not exceed 4093 bytes (so 4092 bytes because of wide chars)
				int length = Math.Min(buffer.Length, 4093) & -2;

				int resultLength = await IoControlAsync(ioctl, input, buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);

				static string GetString(ReadOnlySpan<char> buffer)
					=> buffer.IndexOf('\0') is int endIndex && endIndex >= 0 ?
						buffer.Slice(0, endIndex).ToString() :
						throw new Exception($"The string received was not null-terminated.");

				return resultLength > 2 ? GetString(MemoryMarshal.Cast<byte, char>(buffer.AsSpan(0, resultLength))) : null;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}
	}

	public ValueTask<DeviceId> GetDeviceIdAsync(CancellationToken cancellationToken)
		=> _hidCollectionInformation is { } box ? new(GetDeviceId(ref box.Value)) : SlowGetDeviceIdAsync(cancellationToken);

	private async ValueTask<DeviceId> SlowGetDeviceIdAsync(CancellationToken cancellationToken)
	{
		await EnsureCollectionInformationAvailabilityAsync(cancellationToken).ConfigureAwait(false);
		return GetDeviceId(ref _hidCollectionInformation!.Value);
	}

	// NB: Forcing the USB vendor ID origin here seem like a sensible choice, but it could be wrong.
	// Unless factual evidence that this is wrong, it can be left as-is.
	private static DeviceId GetDeviceId(ref NativeMethods.HidCollectionInformation hidCollectionInformation)
		=> new DeviceId(DeviceIdSource.Hid, VendorIdSource.Usb, hidCollectionInformation.VendorId, hidCollectionInformation.ProductId, hidCollectionInformation.VersionNumber);

	private ValueTask EnsureCollectionInformationAvailabilityAsync(CancellationToken cancellationToken)
		=> _hidCollectionInformation is not null ?
#if NET5_0_OR_GREATER
			ValueTask.CompletedTask :
#else
			new ValueTask(Task.CompletedTask) :
#endif
			CacheCollectionInformationAsync(cancellationToken);

	private async ValueTask CacheCollectionInformationAsync(CancellationToken cancellationToken)
	{
		int length = Unsafe.SizeOf<NativeMethods.HidCollectionInformation>();
		var buffer = ArrayPool<byte>.Shared.Rent(length);
		try
		{
			if (await IoControlAsync(NativeMethods.IoCtlGetCollectionInformation, buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false) == 0)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}

			Volatile.Write(ref _hidCollectionInformation, new StrongBox<NativeMethods.HidCollectionInformation>(Unsafe.As<byte, NativeMethods.HidCollectionInformation>(ref buffer[0])));
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	// TODO: Decide whether to expose the preparsed data as-is, or only the parsed descriptor.
	// For now, the HidDevice class depends on this method. It could be refactored to not go through the byte array stuff later on.
	internal async ValueTask<byte[]> GetPreparsedDataAsync(CancellationToken cancellationToken)
	{
		await EnsureCollectionInformationAvailabilityAsync(cancellationToken).ConfigureAwait(false);
		var buffer = new byte[_hidCollectionInformation!.Value.DescriptorSize];
		int count = await IoControlAsync(NativeMethods.IoCtlGetCollectionDescriptor, buffer, cancellationToken).ConfigureAwait(false);
		if (count != buffer.Length) throw new InvalidOperationException("Unexpected data length.");
		return buffer;
	}
}
