using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AnyLayout.RawInput
{
	public abstract class HidDevice : IDisposable
	{
		public static IEnumerable<HidDevice> GetAllFromSetupApi(bool onlyPresent = true)
		{
			using var handle = NativeMethods.SetupDiGetClassDevs
			(
				NativeMethods.HidDeviceInterfaceClassGuid,
				IntPtr.Zero,
				IntPtr.Zero,
				onlyPresent ?
					NativeMethods.GetClassDeviceFlags.DeviceInterface | NativeMethods.GetClassDeviceFlags.Present :
					NativeMethods.GetClassDeviceFlags.DeviceInterface
			);

			var @lock = new object();

			var interfaceData = new NativeMethods.DeviceInterfaceData
			{
				Size = (uint)Marshal.SizeOf<NativeMethods.DeviceInterfaceData>()
			};

			uint index = 0;
			while (true)
			{
				if (NativeMethods.SetupDiEnumDeviceInterfaces(handle, IntPtr.Zero, NativeMethods.HidDeviceInterfaceClassGuid, index++, ref interfaceData) == 0)
				{
					int lastError = Marshal.GetLastWin32Error();

					if (lastError == NativeMethods.ErrorNoMoreItems)
					{
						break;
					}

					throw new Win32Exception(lastError);
				}

				yield return new GenericHidDevice(NativeMethods.SetupDiGetDeviceInterfaceDetail(handle, ref interfaceData), @lock);
			}
		}

		public static IEnumerable<HidDevice> GetAllFromConfigurationManager(bool onlyPresent = true)
		{
			var flag = onlyPresent ? NativeMethods.GetDeviceInterfaceListSizeFlags.Present : NativeMethods.GetDeviceInterfaceListSizeFlags.All;

			uint charCount;

			{
				uint result = NativeMethods.CM_Get_Device_Interface_List_Size(out charCount, NativeMethods.HidDeviceInterfaceClassGuid, null, flag);
				if (result != 0)
				{
					throw new InvalidOperationException();
				}
			}

			var @lock = new object();
			var buffer = ArrayPool<byte>.Shared.Rent(checked((int)(charCount * 2)));
			try
			{
				uint result = NativeMethods.CM_Get_Device_Interface_List(NativeMethods.HidDeviceInterfaceClassGuid, null, ref MemoryMarshal.Cast<byte, char>(buffer)[0], charCount, flag);
				if (result != 0)
				{
					throw new InvalidOperationException();
				}

				int position = 0;
				while (position < charCount)
				{
					var chars = MemoryMarshal.Cast<byte, char>(buffer).Slice(position);
					int endIndex = chars.IndexOf('\0');

					// Last string will be empty (It seems that the buffer is terminated by double null chars)
					if (endIndex <= 0)
					{
						yield break;
					}

					yield return new GenericHidDevice(chars.Slice(0, endIndex).ToString(), @lock);

					position += endIndex + 1;
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		public static HidDevice FromPath(string deviceName)
			=> new GenericHidDevice(deviceName, new object());

		private SafeFileHandle? _fileHandle;
		private IntPtr _preparsedDataPointer;

		// As RawInput is (seems to be) a layer over the regular HID APIs, that core information is pretty much the only one we can share.
		/// <summary>Gets the name of the device, useable by the file APIs to access the device.</summary>
		public abstract string DeviceName { get; }

		// Kinda hoping there are no HID devices without a VID_XXXX&PID_XXXX there…
		/// <summary>Gets the Vendor ID (VID) associated with this device.</summary>
		/// <remarks>This information can usually be found inside the <see cref="DeviceName"/>.</remarks>
		public abstract ushort VendorId { get; }

		/// <summary>Gets the Product ID (PID) associated with this device.</summary>
		/// <remarks>This information can usually be found inside the <see cref="DeviceName"/>.</remarks>
		public abstract ushort ProductId { get; }

		// A lock object used to protect restricted operations on the class, such as opening the device file.
		private protected abstract object Lock { get; }

		/// <summary>Gets a value indicaing if this instance has been disposed.</summary>
		public abstract bool IsDisposed { get; }

		public virtual void Dispose()
		{
			if (FileHandle is SafeFileHandle fileHandle)
			{
				fileHandle.Dispose();
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

		private protected SafeFileHandle FileHandle => _fileHandle ?? SlowGetFileHandle();

		private SafeFileHandle SlowGetFileHandle()
		{
			EnsureNotDisposed();
			// The file handle should not be opened more than once. We can't use optimistic lazy initialization like in the other cases here.
			lock (Lock)
			{
				if (!(_fileHandle is SafeFileHandle fileHandle))
				{
					EnsureNotDisposed();
					// Try to acquire the device as R/W shared.
					fileHandle = NativeMethods.CreateFile(DeviceName, 0, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
					// Collections opened in exclusive mode by the OS (e.g. Keyboard and Mouse) can still be accessed without requesting read or write.
					if (fileHandle.IsInvalid)
					{
						int errorCode = Marshal.GetLastWin32Error();

						//if (errorCode != ???)
						//{
						//	throw new Win32Exception(errorCode);
						//}

						fileHandle = NativeMethods.CreateFile(DeviceName, 0, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
						if (fileHandle.IsInvalid)
						{
							throw new Win32Exception(Marshal.GetLastWin32Error());
						}
					}
					Volatile.Write(ref _fileHandle, fileHandle);
				}
				return fileHandle;
			}
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
	}
}
