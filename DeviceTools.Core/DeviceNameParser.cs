using System;
using System.Globalization;

namespace DeviceTools;

/// <summary>Parser for Windows device names.</summary>
/// <remarks>
/// <para>
/// The names used in the Windows device tree are dependent on the enumerator that registered the device.
/// It is not guaranteed that all device names will contain the required necessary information, but at least USB and Bluetooth will report the information we need in the default case.
/// </para>
/// </remarks>
public static class DeviceNameParser
{
	public static bool TryParsePciDeviceName(string deviceName, out DeviceId value)
	{
		// Very naive parser looking for the substring VEN_XXXX&DEV_XXXX&SUBSYS_XXXXXXXX&REV_XX. (Manually because Regex would be more allocatey)
		int indexOfVendorId = deviceName.IndexOf("VEN_", StringComparison.OrdinalIgnoreCase);

		if
		(
			indexOfVendorId < 0 ||
			deviceName.Length < indexOfVendorId + 17 /* VID_XXXX&DEV_XXXX */ ||
			!deviceName.AsSpan(indexOfVendorId + 8 /* VID_XXXX */).StartsWith("&DEV_".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
#if NETSTANDARD2_0
			!TryParseUInt16(deviceName.AsSpan(indexOfVendorId + 4, 4), out ushort vendorId) ||
			!TryParseUInt16(deviceName.AsSpan(indexOfVendorId + 13, 4), out ushort productId)
#else
			!ushort.TryParse(deviceName.AsSpan(indexOfVendorId + 4, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort vendorId) ||
			!ushort.TryParse(deviceName.AsSpan(indexOfVendorId + 13, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort productId)
#endif
		)
		{
			value = default;
			return false;
		}

		// Make the REV number optional, as it could be missing or have an invalid value.
		// Most importantly, this REV info is likely not present in device names, but should always be present in device hardware IDs.
		if
		(
			deviceName.Length >= indexOfVendorId + 40 /* VEN_XXXX&DEV_XXXX&SUBSYS_XXXXXXXX&REV_XX */ &&
			deviceName.AsSpan(indexOfVendorId + 33 /* VEN_XXXX&DEV_XXXX&SUBSYS_XXXXXXXX */).StartsWith("&REV_".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
#if NETSTANDARD2_0
			TryParseByte(deviceName.AsSpan(indexOfVendorId + 38, 2), out byte version)
#else
			byte.TryParse(deviceName.AsSpan(indexOfVendorId + 38, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte version)
#endif
		)
		{
			value = DeviceId.ForPci(vendorId, productId, version);
		}
		else
		{
			value = DeviceId.ForPci(vendorId, productId);
		}

		return true;
	}

	public static bool TryParseUsbDeviceName(string deviceName, out DeviceId value)
	{
		// Very naive parser looking for the substring VID_XXXX&PID_XXXX&REV_XXXX. (Manually because Regex would be more allocatey)
		int indexOfVendorId = deviceName.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);

		if
		(
			indexOfVendorId < 0 ||
			deviceName.Length < indexOfVendorId + 17 /* VID_XXXX&PID_XXXX */ ||
			!deviceName.AsSpan(indexOfVendorId + 8 /* VID_XXXX */).StartsWith("&PID_".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
#if NETSTANDARD2_0
			!TryParseUInt16(deviceName.AsSpan(indexOfVendorId + 4, 4), out ushort vendorId) ||
			!TryParseUInt16(deviceName.AsSpan(indexOfVendorId + 13, 4), out ushort productId)
#else
			!ushort.TryParse(deviceName.AsSpan(indexOfVendorId + 4, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort vendorId) ||
			!ushort.TryParse(deviceName.AsSpan(indexOfVendorId + 13, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort productId)
#endif
		)
		{
			value = default;
			return false;
		}

		// Make the REV number optional, as it could be missing or have an invalid value.
		// Most importantly, this REV info is likely not present in device names, but should always be present in device hardware IDs.
		if
		(
			deviceName.Length < indexOfVendorId + 26 /* VID_XXXX&PID_XXXX&REV_XXXX */ ||
			!deviceName.AsSpan(indexOfVendorId + 17 /* VID_XXXX&PID_XXXX */).StartsWith("&REV_".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
#if NETSTANDARD2_0
			!TryParseUInt16(deviceName.AsSpan(indexOfVendorId + 22, 4), out ushort version)
#else
			!ushort.TryParse(deviceName.AsSpan(indexOfVendorId + 22, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort version)
#endif
		)
		{
			version = 0xFFFF;
		}

		value = DeviceId.ForUsb(vendorId, productId, version);
		return true;
	}

	public static bool TryParseBluetoothDeviceName(string deviceName, out DeviceId value)
	{
		// Very naive parser looking for the substring _VID&XXXXXX_PID&XXXX_REV&XXXX.
		// Similar to the one we use for USB devices above.
		// Couldn't find many details on how these names are generated inside Windows, as there seem to exist *at least* two flavors of it, but here's what we know:
		// - The part we are interested in can start with either _Dev_VID or just _VID. (Could be _VID for BT and _Dev_VID for BT LE maybe ?)
		// - So, looking for _VID works in both cases.
		// - VID seems to be presented as 6 digits, and it looking for details on this does not look very fruitful.
		// - However, looking for more HID-specific Bluetooth info leads to this more useful "Device Information Service Specification":
		//   https://www.bluetooth.org/docman/handlers/downloaddoc.ashx?doc_id=244369
		// - As such we know that devices are identified by 4 things (3 really, but…)
		//   - The Vendor ID Source
		//   - The Vendor ID
		//   - The Product ID
		//   - The version
		// - As it appears, the 3 byte "VID" in our device name is an agglomerate of Vendor ID Source and Vendor ID
		// - Which is even better news, as if Vendor ID Source is 0x02, then it is a ID straight from the USB database.
		//   01 Bluetooth SIG- assigned Device ID Vendor ID value from the Assigned Numbers document
		//   02 USB Implementer’s Forum assigned Vendor ID value
		// - And the REV field here should match the version.
		// - Version should be interpreted as BCD, and 0xJJMN would give version JJ.M.N (Same as USB, it turns out)
		// The two additional fields are added to the Device ID structure so that we provide the most complete information possible.
		int indexOfVendorId = deviceName.IndexOf("_VID&", StringComparison.OrdinalIgnoreCase);

		if
		(
			indexOfVendorId < 0 ||
			deviceName.Length < indexOfVendorId + 20 /* _VID&XXXXXX_PID&XXXX */ ||
			!deviceName.AsSpan(indexOfVendorId + 11 /* _VID&XXXXXX */).StartsWith("_PID&".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
#if NETSTANDARD2_0
			!TryParseByte(deviceName.AsSpan(indexOfVendorId + 5, 2), out byte vendorIdSource) ||
			!TryParseUInt16(deviceName.AsSpan(indexOfVendorId + 7, 4), out ushort vendorId) ||
			!TryParseUInt16(deviceName.AsSpan(indexOfVendorId + 16, 4), out ushort productId)
#else
			!byte.TryParse(deviceName.AsSpan(indexOfVendorId + 5, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte vendorIdSource) ||
			!ushort.TryParse(deviceName.AsSpan(indexOfVendorId + 7, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort vendorId) ||
			!ushort.TryParse(deviceName.AsSpan(indexOfVendorId + 16, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort productId)
#endif
		)
		{
			value = default;
			return false;
		}

		// Make the REV number optional, as it could be missing or have an invalid value.
		if
		(
			deviceName.Length < indexOfVendorId + 29 /* _VID&XXXXXX_PID&XXXX_REV&XXXX */ ||
			!deviceName.AsSpan(indexOfVendorId + 20 /* _VID&XXXXXX_PID&XXXX */).StartsWith("_REV&".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
#if NETSTANDARD2_0
			!TryParseUInt16(deviceName.AsSpan(indexOfVendorId + 25, 4), out ushort version)
#else
			!ushort.TryParse(deviceName.AsSpan(indexOfVendorId + 25, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort version)
#endif
		)
		{
			version = 0xFFFF;
		}

		// Assuming that _Dev_VID means BLE, and _VID alone means BT. This seems to be true to the extent of the data I have.
		value = indexOfVendorId >= 4 && deviceName.AsSpan(indexOfVendorId - 4, 4).Equals("_Dev".AsSpan(), StringComparison.OrdinalIgnoreCase) ?
			DeviceId.ForBluetoothLowEnergy((BluetoothVendorIdSource)vendorIdSource, vendorId, productId, version) :
			DeviceId.ForBluetooth((BluetoothVendorIdSource)vendorIdSource, vendorId, productId, version);
		return true;
	}

	public static bool TryParseDeviceName(string deviceName, out DeviceId value)
		=> TryParseUsbDeviceName(deviceName, out value) || TryParseBluetoothDeviceName(deviceName, out value) || TryParsePciDeviceName(deviceName, out value);

	public static DeviceId ParseDeviceName(string deviceName)
	{
		if (!TryParseDeviceName(deviceName, out var deviceId))
		{
			throw new ArgumentException("The specified device name does not seem to contain Vendor ID and Product ID information.");
		}

		return deviceId;
	}

#if NETSTANDARD2_0
	// Simplified hexadecimal parser assuming span is always > 2 bytes.
	private static bool TryParseByte(ReadOnlySpan<char> span, out byte value)
	{
		byte b;
		int v;
		if (span.Length >= 2)
		{
			if (TryParseDigit(span[0], out b))
			{
				v = b;
				if (TryParseDigit(span[1], out b))
				{
					value = (byte)(v << 4 | b);
					return true;
				}
			}
		}
		value = 0;
		return false;
	}

	// Simplified hexadecimal parser assuming span is always > 4 bytes.
	private static bool TryParseUInt16(ReadOnlySpan<char> span, out ushort value)
	{
		byte b;
		int v;
		if (span.Length >= 4)
		{
			if (TryParseDigit(span[0], out b))
			{
				v = b;
				if (TryParseDigit(span[1], out b))
				{
					v = v << 4 | b;
					if (TryParseDigit(span[1], out b))
					{
						v = v << 4 | b;
						if (TryParseDigit(span[1], out b))
						{
							value = (ushort)(v << 4 | b);
							return true;
						}
					}
				}
			}
		}
		value = 0;
		return false;
	}

	private static bool TryParseDigit(char c, out byte digit)
	{
		if (c >= '0' && c <= 'f')
		{
			if (c <= '9')
			{
				digit = (byte)(c - '0');
			}
			else if (c >= 'A')
			{
				if (c >= 'a')
				{
					digit = (byte)(c - ('a' - 10));
				}
				else if (c <= 'F')
				{
					digit = (byte)(c - ('A' - 10));
				}
				else
				{
					goto Failed;
				}
			}
			else
			{
				goto Failed;
			}

			return true;
		}

	Failed:;
		digit = 0;
		return false;
	}
#endif
}
