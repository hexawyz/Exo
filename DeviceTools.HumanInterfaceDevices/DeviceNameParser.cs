using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace DeviceTools.HumanInterfaceDevices
{
	// TODO: make public with a nicer API
	internal static class DeviceNameParser
	{
		public static (ushort VendorId, ushort ProductId) ParseDeviceName(string deviceName)
		{
			// Very naive parser looking for the substring VID_XXXX&PID_XXXX. (Manually because Regex would be more allocatey)
			int indexOfVendorId = deviceName.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);

			if
			(
				indexOfVendorId < 0 ||
				deviceName.Length < indexOfVendorId + 17 /* VID_XXXX&PID_XXXX */ ||
				!deviceName.AsSpan(indexOfVendorId + 9 /* VID_XXXX& */).StartsWith("PID_".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
#if NETSTANDARD2_0
				!TryParse(deviceName.AsSpan(indexOfVendorId + 4, 4), out ushort vendorId) ||
				!TryParse(deviceName.AsSpan(indexOfVendorId + 13, 4), out ushort productId)
#else
				!ushort.TryParse(deviceName.AsSpan(indexOfVendorId + 4, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort vendorId) ||
				!ushort.TryParse(deviceName.AsSpan(indexOfVendorId + 13, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort productId)
#endif
			)
			{
				throw new ArgumentException("The specified device name does not seem to contain Vendor ID and Product ID information.");
			}

			return (vendorId, productId);
		}

#if NETSTANDARD2_0
		// Simplified hexadecimal parser assuming span is always > 4 bytes.
		private static bool TryParse(ReadOnlySpan<char> span, out ushort value)
		{
			static bool TryParseDigit(char c, out byte digit)
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
#endif
	}
}
