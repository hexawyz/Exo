# Links

## HID documentation

* HID Usage Tables 1.12: https://www.usb.org/sites/default/files/documents/hut1_12v2.pdf
* Header file with more HID keyboard values than Windows headers: https://gist.github.com/MightyPork/6da26e382a7ad91b5496ee55fdc73db2

## Windows API Documentation

* Keyboard types: https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getkeyboardtype (81 = USB)
* Introduction to HID concepts: https://docs.microsoft.com/en-us/windows-hardware/drivers/hid/introduction-to-hid-concepts
* List devices (not HID-specific): https://docs.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_get_device_interface_listw

## HID Usage tables in Google Online Viewer (only way to copy text ?)

https://drive.google.com/viewerng/viewer?url=https://www.usb.org/sites/default/files/documents/hut1_12v2.pdf

## Other Links

* Gamedev comment hinting at how to link RawInput & HID: https://www.gamedev.net/forums/topic/700010-winapi-raw-input-confusion/5395721/

# Other Bits

## Regex replacements to quickly transform text from HID Usage tables to C# enums

1. Bulk transformation of table lines
   `^([0-9A-F]{1,3}) ((?:[^ \.\r\n]+ )+)[^ \.\r\n]{2,4} \d+(?:\.\d+)+`
   `$2 = 0x$1,`
2. Remove spaces between words
   `([^ \r\n]+) ([^ \r\n]+) *(= 0x[0-9A-F]+),`
   `$1$2 $3,`
3. Detect ALLCAPS
   `(<!0x[0-9A-F]*)[A-Z][A-Z]`
4. WiN32 GUIDs
   `DEFINE_GUID\(GUID_DEVCLASS_([^,]+),\s+`
   `public static readonly Guid $1 = new Guid(`
5. Bulk transformation of table lines for HID Power Devices
   `([0-9A-Z]{2}) (\b(?:\w|\d)+(?: (?!C[ALP]|D[FV]|S[FV])(?:\w|\d)+)*) (?:C[ALP]|D[FV]|S[FV])(?: x){0,3} (?:N\/A|R(?:\/[OW])?)(?: x)? (?:\d+\.\d+\.\d+)`
   `$2 = 0x$1,`
6. Find identifiers to fix (Two uppercase letters or one lowercase initial)
   `([A-Z]{2}|\b[a-z])`

## Enum template

````csharp
= 0x00,
= 0x01,
= 0x02,
= 0x03,
= 0x04,
= 0x05,
= 0x06,
= 0x07,
= 0x08,
= 0x09,
= 0x0A,
= 0x0B,
= 0x0C,
= 0x0D,
= 0x0E,
= 0x0F,
````
