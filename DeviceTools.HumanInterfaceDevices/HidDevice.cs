using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace DeviceTools.HumanInterfaceDevices
{
	// TODO: Maybe split some properties in a cache that is lazily-allocated.
	public abstract class HidDevice : IDisposable
	{
		public static IEnumerable<HidDevice> GetAll(bool includeAll = false)
		{
			var @lock = new object();
			foreach (string name in Device.EnumerateAllInterfaces(DeviceInterfaceClassGuids.Hid, includeAll))
			{
				yield return new GenericHidDevice(name, @lock);
			}
		}

		public static HidDevice FromPath(string deviceName)
			=> new GenericHidDevice(deviceName, new object());

		private DeviceStream? _deviceStream;
		private IntPtr _preparsedDataPointer;
		private string? _productName;
		private string? _manufacturerName;
		private string? _serialNumber;
		private string? _instanceId;
		private Guid? _containerId;

		// As RawInput is (seems to be) a layer over the regular HID APIs, that core information is pretty much the only one we can share.
		/// <summary>Gets the name of the device, useable by the file APIs to access the device.</summary>
		public abstract string DeviceName { get; }

		// Kinda hoping there are no HID devices without a VID_XXXX&PID_XXXX there…
		/// <summary>Gets the device ID associated with this device.</summary>
		/// <remarks>
		/// The device ID information allows to uniquely identify hardware, but not a specific hardware instance.
		/// This information can usually be found inside the <see cref="DeviceName"/>.
		/// </remarks>
		public abstract DeviceId DeviceId { get; }

		// A lock object used to protect restricted operations on the class, such as opening the device file.
		private protected abstract object Lock { get; }

		/// <summary>Gets a value indicating if this instance has been disposed.</summary>
		public abstract bool IsDisposed { get; }

		public virtual void Dispose()
		{
			if (_deviceStream is DeviceStream deviceStream)
			{
				deviceStream.Dispose();
				// Preparsed data always requires accessing the file handle.
				if (_preparsedDataPointer != IntPtr.Zero)
				{
					NativeMethods.HidDiscoveryFreePreparsedData(_preparsedDataPointer);
				}
			}
		}

		private void EnsureNotDisposed()
		{
			if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
		}

		private protected SafeFileHandle FileHandle => DeviceStream.SafeFileHandle;

		// We wrap the device within a DeviceStream instance in order to access the async IOCTL features.
		// This incurs an extra allocation but it will be more helpful, as the stream caches the async state for us.
		private protected DeviceStream DeviceStream => _deviceStream ?? SlowGetDeviceStream();

		private DeviceStream SlowGetDeviceStream()
		{
			EnsureNotDisposed();
			// The file handle should not be opened more than once. We can't use optimistic lazy initialization like in the other cases here.
			lock (Lock)
			{
				if (Volatile.Read(ref _deviceStream) is not DeviceStream deviceStream)
				{
					EnsureNotDisposed();
					// Always open the device without specific access, as the goal of this class is not to read or write to the device.
					var fileHandle = Device.OpenHandle(DeviceName, DeviceAccess.None);
					// TODO: Find a better solution to access IO control without a stream object? Lying about the read access is a bit hacky but it works for now.
					deviceStream = new(fileHandle, FileAccess.Read);
					Volatile.Write(ref _deviceStream, deviceStream);
				}
				return deviceStream;
			}
		}

		public ValueTask<string> GetProductNameAsync(CancellationToken cancellationToken)
			=> _productName is string name ? new(name) : SlowGetProductNameAsync(cancellationToken);

		private async ValueTask<string> SlowGetProductNameAsync(CancellationToken cancellationToken)
		{
			var manufacturerName = await SlowGetWellKnownStringAsync(DeviceStream, 0xb01be, cancellationToken).ConfigureAwait(false);
			return _productName = manufacturerName;
		}

		public ValueTask<string> GetManufacturerNameAsync(CancellationToken cancellationToken)
			=> _manufacturerName is string name ? new(name) : SlowGetManufacturerNameAsync(cancellationToken);

		private async ValueTask<string> SlowGetManufacturerNameAsync(CancellationToken cancellationToken)
		{
			var manufacturerName = await SlowGetWellKnownStringAsync(DeviceStream, 0xb01ba, cancellationToken).ConfigureAwait(false);
			return _manufacturerName = manufacturerName;
		}

		public ValueTask<string> GetSerialNumberAsync(CancellationToken cancellationToken)
			=> _serialNumber is string name ? new(name) : SlowGetSerialNumberAsync(cancellationToken);

		private async ValueTask<string> SlowGetSerialNumberAsync(CancellationToken cancellationToken)
		{
			var manufacturerName = await SlowGetWellKnownStringAsync(DeviceStream, 0xb01c2, cancellationToken).ConfigureAwait(false);
			return _serialNumber = manufacturerName;
		}

		private static async ValueTask<string> SlowGetWellKnownStringAsync(DeviceStream device, int ioctl, CancellationToken cancellationToken)
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

					if (await device.IoControlAsync(ioctl, buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false) == 0)
					{
						throw new Win32Exception(Marshal.GetLastWin32Error());
					}

					static string GetString(ReadOnlySpan<char> buffer)
						=> buffer.IndexOf('\0') is int endIndex && endIndex >= 0 ?
							buffer.Slice(0, endIndex).ToString() :
							throw new Exception($"The string received was not null-terminated.");

					return GetString(MemoryMarshal.Cast<byte, char>(buffer.AsSpan(0, length)));
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(buffer);
				}
			}
		}

		public string InstanceId => _instanceId ?? SlowGetInstanceId();

		private string SlowGetInstanceId() => _instanceId = Device.GetDeviceInstanceId(DeviceName);

		public Guid ContainerId => _containerId ?? SlowGetContainerId();

		private Guid SlowGetContainerId()
		{
			uint deviceNode = Device.LocateDeviceNode(InstanceId);
			var containerId = Device.GetContainerId(deviceNode);
			_containerId = containerId;
			return containerId;
		}

		// TODO: How should this be exposed?
		private protected IntPtr PreparsedDataPointer
		{
			get
			{
				var preparsedData = _preparsedDataPointer;
				if (preparsedData != IntPtr.Zero) return preparsedData;
				return SlowGetNativeAllocatedPreparsedData();
			}
		}

		private IntPtr SlowGetNativeAllocatedPreparsedData()
		{
			var preparsedData = Volatile.Read(ref _preparsedDataPointer);

			if (preparsedData != IntPtr.Zero) return preparsedData;

			// We may end up allocating more than once in case this method is called concurrently, but it shouldn't matter that much.
			if (NativeMethods.HidDiscoveryGetPreparsedData(FileHandle, out preparsedData) == 0)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}

			if (preparsedData == default)
			{
				throw new InvalidOperationException();
			}

			// Give priority to the previously assigned value, if any.
			{
				var previousPreparsedData = Interlocked.CompareExchange(ref _preparsedDataPointer, preparsedData, default);

				// Free the preparsed data we just allocated because it is now redundant.
				if (previousPreparsedData != default)
				{
					NativeMethods.HidDiscoveryFreePreparsedData(preparsedData);
					preparsedData = previousPreparsedData;
				}
			}

			return preparsedData;
		}

		// FIXME: Is there a better way to get "ref null" ?
		// Basically, the code below is converting "IntPtr" to "ref byte", so that we can have an equivalent API ignoring the origin of preparsed data.
		private protected virtual ref byte PreparsedDataFirstByte
			=> ref Unsafe.Add(ref default(Span<byte>).GetPinnableReference(), PreparsedDataPointer);

		// TODO: Better API
		public (int InputReportLength, int OutputReportLength, int FeatureReportLength) GetReportLengths()
		{
			ref byte preparsedDataFirstByte = ref PreparsedDataFirstByte;

			var result = NativeMethods.HidParsingGetCaps(ref preparsedDataFirstByte, out var caps);

			if (result != NativeMethods.HidParsingResult.Success)
			{
				throw new InvalidOperationException();
			}

			return (caps.InputReportByteLength, caps.OutputReportByteLength, caps.FeatureReportByteLength);
		}

		// TODO: Wrap this in a high level structure.
		public NativeMethods.HidParsingLinkCollectionNode[] GetLinkCollectionNodes()
		{
			ref byte preparsedDataFirstByte = ref PreparsedDataFirstByte;

			NativeMethods.HidParsingGetCaps(ref preparsedDataFirstByte, out var caps);

			uint count = caps.LinkCollectionNodesCount;

			if (caps.LinkCollectionNodesCount == 0)
			{
				return Array.Empty<NativeMethods.HidParsingLinkCollectionNode>();
			}

			var nodes = new NativeMethods.HidParsingLinkCollectionNode[count];
			if (NativeMethods.HidParsingGetLinkCollectionNodes(ref nodes[0], ref count, ref preparsedDataFirstByte) != NativeMethods.HidParsingResult.Success)
			{
				throw new InvalidOperationException();
			}
			return nodes;
		}

		// TODO: Wrap this in a high level structure.
		public NativeMethods.HidParsingButtonCaps[] GetButtonCapabilities(NativeMethods.HidParsingReportType reportType)
		{
			ref byte preparsedDataFirstByte = ref PreparsedDataFirstByte;

			NativeMethods.HidParsingGetCaps(ref preparsedDataFirstByte, out var caps);

			ushort count = reportType switch
			{
				NativeMethods.HidParsingReportType.Input => caps.InputButtonCapsCount,
				NativeMethods.HidParsingReportType.Output => caps.OutputButtonCapsCount,
				NativeMethods.HidParsingReportType.Feature => caps.FeatureButtonCapsCount,
				_ => throw new ArgumentOutOfRangeException(nameof(reportType))
			};

			if (count == 0)
			{
				return Array.Empty<NativeMethods.HidParsingButtonCaps>();
			}

			var buttonCaps = new NativeMethods.HidParsingButtonCaps[count];

			if (NativeMethods.HidParsingGetButtonCaps(reportType, ref buttonCaps[0], ref count, ref preparsedDataFirstByte) != NativeMethods.HidParsingResult.Success)
			{
				throw new InvalidOperationException();
			}
			return buttonCaps;
		}

		// TODO: Wrap this in a high level structure.
		public NativeMethods.HidParsingValueCaps[] GetValueCapabilities(NativeMethods.HidParsingReportType reportType)
		{
			ref byte preparsedDataFirstByte = ref PreparsedDataFirstByte;

			NativeMethods.HidParsingGetCaps(ref preparsedDataFirstByte, out var caps);

			ushort count = reportType switch
			{
				NativeMethods.HidParsingReportType.Input => caps.InputValueCapsCount,
				NativeMethods.HidParsingReportType.Output => caps.OutputValueCapsCount,
				NativeMethods.HidParsingReportType.Feature => caps.FeatureValueCapsCount,
				_ => throw new ArgumentOutOfRangeException(nameof(reportType))
			};

			if (count == 0)
			{
				return Array.Empty<NativeMethods.HidParsingValueCaps>();
			}

			var valueCaps = new NativeMethods.HidParsingValueCaps[count];

			if (NativeMethods.HidParsingGetValueCaps(reportType, ref valueCaps[0], ref count, ref preparsedDataFirstByte) != NativeMethods.HidParsingResult.Success)
			{
				throw new InvalidOperationException();
			}
			return valueCaps;
		}

		// TODO: Wrap this in a high level structure.
		public string GetString(int index)
			=> NativeMethods.GetIndexedString(FileHandle, (uint)index);

		public PhysicalDescriptorSetCollection GetPhysicalDescriptorSets()
			=> NativeMethods.GetPhysicalDescriptor(FileHandle);

		/// <summary>Tries to locate the best information source for <see cref="DeviceId"/> based on the device name.</summary>
		/// <remarks>
		/// <para>
		/// This method should only be called after <see cref="DeviceName"/> is properly accessible.
		/// It may or may not load <see cref="InstanceId"/> depending on whether its contents are needed.
		/// </para>
		/// <para>
		/// For the most complete information possible, we want to look ath the Hardware IDs, that may or may not contain more information than the device interface name.
		/// We are looking for a string containing VID, PID and REV if possible.
		/// </para>
		/// </remarks>
		/// <param name="deviceId">The resolved device ID.</param>
		/// <returns>true if the name was successful resolved; otherwise false.</returns>
		protected bool TryResolveDeviceIdFromNames(out DeviceId deviceId)
		{
			// For Bluetooth devices at least, device interface names should contain the REV field,
			// so we don't need to look up to the hardware IDs to get this information.
			if (DeviceName.IndexOf("REV", StringComparison.OrdinalIgnoreCase) >= 0 && DeviceNameParser.TryParseDeviceName(DeviceName, out deviceId))
			{
				return true;
			}

			try
			{
				uint deviceNode = Device.LocateDeviceNode(InstanceId);
				var hardwareIds = Device.GetDeviceHardwareIds(deviceNode);

				// Hardware IDs seem to be ordered from most precise to least precise, so this should ideally match on the first one if any of them is valid.
				// If they are not ordered in that way, the risk is only to miss the "REV" field.
				foreach (var hardwareId in hardwareIds)
				{
					if (DeviceNameParser.TryParseDeviceName(hardwareId, out deviceId))
					{
						return true;
					}
				}
			}
			catch (ConfigurationManagerException)
			{
				// Ignore potential errors here, as we have a fallback.
			}

			return DeviceNameParser.TryParseDeviceName(DeviceName, out deviceId);
		}
	}
}
