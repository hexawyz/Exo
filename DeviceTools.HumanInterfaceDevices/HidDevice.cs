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

namespace DeviceTools.HumanInterfaceDevices;

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

	private HidDeviceStream? _deviceStream;
	private HidCollectionDescriptor? _collectionDescriptor;
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
		}
	}

	private void EnsureNotDisposed()
	{
		if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
	}

	private protected SafeFileHandle FileHandle => DeviceStream.SafeFileHandle;

	// We wrap the device within a DeviceStream instance in order to access the async IOCTL features.
	// This incurs an extra allocation but it will be more helpful, as the stream caches the async state for us.
	private protected HidDeviceStream DeviceStream => _deviceStream ?? SlowGetDeviceStream();

	private HidDeviceStream SlowGetDeviceStream()
	{
		EnsureNotDisposed();
		// The file handle should not be opened more than once. We can't use optimistic lazy initialization like in the other cases here.
		lock (Lock)
		{
			if (Volatile.Read(ref _deviceStream) is not HidDeviceStream deviceStream)
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

	public ValueTask<string> GetManufacturerNameAsync(CancellationToken cancellationToken)
		=> _manufacturerName is string name ? new(name) : SlowGetManufacturerNameAsync(cancellationToken);

	private async ValueTask<string> SlowGetManufacturerNameAsync(CancellationToken cancellationToken)
	{
		string manufacturerName = await DeviceStream.GetManufacturerNameAsync(cancellationToken).ConfigureAwait(false);
		return _manufacturerName = manufacturerName;
	}

	public ValueTask<string> GetProductNameAsync(CancellationToken cancellationToken)
		=> _productName is string name ? new(name) : SlowGetProductNameAsync(cancellationToken);

	private async ValueTask<string> SlowGetProductNameAsync(CancellationToken cancellationToken)
	{
		string productName = await DeviceStream.GetProductNameAsync(cancellationToken).ConfigureAwait(false);
		return _productName = productName;
	}

	public ValueTask<string> GetSerialNumberAsync(CancellationToken cancellationToken)
		=> _serialNumber is string name ? new(name) : SlowGetSerialNumberAsync(cancellationToken);

	private async ValueTask<string> SlowGetSerialNumberAsync(CancellationToken cancellationToken)
	{
		string serialNumber = await DeviceStream.GetSerialNumberAsync(cancellationToken).ConfigureAwait(false);
		return _serialNumber = serialNumber;
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

	public ValueTask<HidCollectionDescriptor> GetCollectionDescriptorAsync(CancellationToken cancellationToken)
		=> Volatile.Read(ref _collectionDescriptor) is { } descriptor ? new(descriptor) : GetAndCacheCollectionDescriptorAsync(cancellationToken);

	private async ValueTask<HidCollectionDescriptor> GetAndCacheCollectionDescriptorAsync(CancellationToken cancellationToken)
	{
		var descriptor = HidCollectionDescriptor.Parse(await GetPreparsedDataAsync(cancellationToken).ConfigureAwait(false));
		Volatile.Write(ref _collectionDescriptor, descriptor);
		return descriptor;
	}

	private protected virtual async ValueTask<byte[]> GetPreparsedDataAsync(CancellationToken cancellationToken)
		=> await DeviceStream.GetPreparsedDataAsync(cancellationToken).ConfigureAwait(false);

	public ValueTask<string> GetStringAsync(int index, CancellationToken cancellationToken)
		=> DeviceStream.GetStringAsync(index, cancellationToken);

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
