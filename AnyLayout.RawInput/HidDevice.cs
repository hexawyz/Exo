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
		public static IEnumerable<HidDevice> All()
		{
			using var handle = NativeMethods.SetupDiGetClassDevs
			(
				NativeMethods.HidDeviceInterfaceClassGuid,
				IntPtr.Zero,
				IntPtr.Zero,
				NativeMethods.GetClassDeviceFlags.DeviceInterface | NativeMethods.GetClassDeviceFlags.Present
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
						fileHandle = NativeMethods.CreateFile(DeviceName, 0, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
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
	}
}
