# The device

Vendor ID: 05AC (Apple)
Product ID: 0221

# Analysis

## HID APIs

Windows reports that the device has built-in (driver-less) ConsumerControl usage page for all media keys. However, the keyboard seems unable to emit the corresponding codes on its own (without bootcamp driver).
It also does not indicate that the keyboard would return any kind of status on the fn key. (But it does return it ! Only in a mostly compatible, non standard way 🙄)

Keycodes returned by various keys are significantly altered by the Apple driver (e.g. some physical keys are remapped to more Windows compatible keys), which also seems to be able to interact with the fn key at will.
To be confirmed, but I remember seing another HID usage page for the buttons not mapped in Consumer Control, which I don't get here without Apple's driver. (Maybe I remember wrong)

Keyboard HID collections being locked by Windows, it seems that it will be impossible to tweak this keyboard in any fun way without writing a custom filter driver. (Which then has to be signed, which is… impossible ?)

PS: HID seems to report 5 LEDs for the keyboard… Are those really "Compose" and "Kana" LEDs ?

## From Looking at raw USB packets

Apple uses a very "standard" input report for its USB keyboards. (6 keys, which is the widely used mapping, as more keys are not always supported by OSes…)
However, they try to be clever about it and don't use the input report strictly as the spec would mandate: The Keyboard will never report more that 5 non modifier keys at the same time.

A regular input report would be compsoed of a 6 byte buffer of current keypresses (or full of 0x01 to indicate rollover)

e.g.
0 0 0 0 0 0 // No keys
KEY0 0 0 0 0 0 // Key0 down first
KEY0 KEY1 0 0 0 0 // Key0 down first, then Key1
…
KEY0 KEY1 KEY2 KEY3 KEY4 KEY5 // Key0 down first, then Key1, etc.
1 1 1 1 1 1 // More than 6 keys down.

But Apple reserves the 6th entry in the buffer to report fn key status… all the time. (0x00 or 0x01)

e.g.
0 0 0 0 0 FN // No regular key
KEY0 0 0 0 0 0 FN // Key0 down first
…
KEY0 KEY1 KEY2 KEY3 0 FN // Key0~Key3 down (4 keys if fn not down, 5 keys if fn down)
KEY0 KEY1 KEY2 KEY3 KEY4 0 // Key0~Key4 down and fn *NOT* down (5 keys)
1 1 1 1 1 0 // More than 5 regular keys down
1 1 1 1 1 1 // More than 4 regular keys AND fn key down

Since Apple uses the code 0x01 (keybaord rollover) at a position where no OS should look for it, this behavior is hidden unless a specific driver is installed… 🙁
This is because OS will read keys sequentially in the buffer. Once 0x00 is encountered, there should be no other key information.
At worst, if a HID keyboard driver tried to interpret the data for fn key down it would probably conclude that the input report is invalid, so it wouldn't make things more or less useful.
All of this means that there should be no way to read that key from userland in Windows (what a shame 😥) and also that the keyboard is unable to report on more than 5 keys simultaneously. (including fn, excluding standard modifiers)

HYPOTHESIS:
Originally, the keyboard did process the fn key internally, but at some point Apple issued a firmware update to make the fn key software-accessible ?
This would explain the seemingly unused ConsumerControl usages ?

### The EJECT key

My trusty old Aluminium USB keyboard has an eject button. I wondered how it worked, and if anything special was required to support it.
The HID report without Apple's Driver seems to indicate support for the Eject key as part of the Consumer page (Endpoint 2 of the device), but I don't remember seeing anything happen.

At the raw level, the keyboard itself will send an interrupt on the endpoint 1 (Consumer page) containg two bytes: 08 00.
This will occur every time the Eject key is released. Nothing more.

The driver probably intercepts this and neutralizes it. Might be worth looking a bit more about this key later on.

## Using Apple's keyboard driver

I lived with this one for years without real problems. I only hoped I could get rid of it because it is not convenient to use on non Mac PC with Windows installations.
One can still download the keyboard drivers (BootCamp\Drivers\Apple\AppleKeyboard\Keymagic64.inf ATM) using a tool such as Brigadier, but it is way less convenient than not using the driver.
This is all the more sad, as the keyboard would be fully functional out of the box, if not for the missing fn key support. (This whole project started with the hope of remapping the keyboard using a KBD layout… 😗)

The Apple USB keyboard driver is not bad per-se, but is limited in functionality if used without the keyboard driver, and it still doesn't seem to let us remap modifier keys. (I personally would like to get rid of the right GUI key…)

💡 Pretty confident that modifier keys can't be remapped now 😥

Having had to reinstall it to get back support for fn key, it occured to me that maybe we could be able to work *WITH* the driver, replacing bootcamp altogether…

Apple's USB keyboard driver exposes and reads (writes ?) some informations from the registry at HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\KeyMagic (didn't remember this to be the path, but… hey)
When using Bootcamp.exe (which we obviously shouldn't want on a non-Mac computer) the software is able to dynamically toggle the state of fn key support, meaning it is able to communicate with the driver somehow.

Regular way of communicating with a driver seems to be via ioctl… Let's look into that.

### Default key remappings done by the driver

#### Always

The driver will remap the following key combinations when "fn" is down:

* ENTER => INSERT
* RETURN => INSERT
* ESCAPE => PAUSE
* SHIFT + F11 => PRINT SCREEN
* ALT + SHIFT + F11 => ALT + PRINT SCREEN
* SHIFT + F12 => SCROLL LOCK
* DELETE => DELETE FORWARD
* RIGHT ARROW => END
* LEFT ARROW => HOME
* DOWN ARROW => PAGE DOWN
* UP ARROW => PAGE UP

This behavior is hardcoded, but can probably be disabled with a registry setting. (Need to confirm; but most of those remaps are actually useful, so that's of little interest here)

#### Depending of configured fn key behavior

The driver will remap Fn keys to Usages from the HID Consumer Page:

* F1 => 0x70 (Reserved)
* F2 => 0x6F (Reserved)
* F7 => HID_USAGE_CONSUMER_SCAN_PREV_TRACK
* F8 => HID_USAGE_CONSUMER_PLAY_PAUSE
* F9 => HID_USAGE_CONSUMER_SCAN_NEXT_TRACK
* F10 => HID_USAGE_CONSUMER_MUTE
* F11 => HID_USAGE_CONSUMER_VOLUME_DECREMENT
* F12 => HID_USAGE_CONSUMER_VOLUME_INCREMENT

As it turns out, this doesn't seem to be configurable… (Not that much surprising, considering these usages are on a different page)

### Registry keys exposed by the Apple driver

````
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\KeyMagic]
"Keymap"=hex:69,46,6a,47,6b,48,91,8b,90,88
"KeymapFn"=hex:0c,22,0d,1e,0e,1f,0f,20,10,27,12,23,13,55,18,21,27,54,2d,67,33,\
  56,38,57
"KeymapNumlock"=hex:0c,5d,0d,59,0e,5a,0f,5b,10,62,12,5e,13,55,18,5c,24,5f,25,\
  60,26,61,27,54,2d,67,33,56,37,63,38,57
"enable"=hex:01
"OSXFnBehavior"=hex:01
````

Those registry keys are quite interesting, but the most straightforward, and most useful out of the box is the OSXFnBehavior key.
When set to 0, Fn keys will behave as regualr Fn keys by default, and when set to 1, they will behave as media/device control keys by default.

Keymaps seem to be a slightly different beast, though.

#### The keymap key

After having read quite a bit of the USB HID specifications, this one actually seems easy enough.

This key is an array of paris of bytes, where each byte is a HID Keyboard Scan code.

````csharp
// The Keymap key would be equivalent to this, except that usages are stored as bytes and not dword.
(HidKeyboardUsage OriginalKey, HidKeyboardUsage RemappedKey) defaultKeymap = new[]
{
	(HidKeyboardUsage.F14, HidKeyboardUsage.PrintScreen),
	(HidKeyboardUsage.F15, HidKeyboardUsage.PrintScreen),
	(HidKeyboardUsage.F16, HidKeyboardUsage.PrintScreen),
	(HidKeyboardUsage.Lang2, HidKeyboardUsage.International5),
	(HidKeyboardUsage.Lang1, HidKeyboardUsage.International2),
};
````

This registry key indicates keys that will be permanently remapped to other keys, as long as those keys have a HID usage assigned to them.
Doesn't seem like modifier keys can be remapped, though. 😕 (More experimentation needed, but simply trying to remap RIGHT_GUI to MENU… (or maybe that's APPLICATION)

#### KeymapFn

This one is weirder. Bytes still seem to go two by two, but I can't really make sense of what this does. 🤨

0c 22
0d 1e
0e 1f
0f 20
10 27
12 23
13 55
18 21
27 54
2d 67
33 56
38 57

This old keyboard has keys for:
F1 Brightness Up
F2 Brightness Down
F3 Expose (Probably not used)
F4 Dashboard (Probaby not used)
F5
F6
F7 Media Previous
F8 Media Play/Pause
F9 Media Next
F10 Volume Mute
F11 Volume Down
F12 Volume Up
(Eject)

Supposedly, the 12 lines could indicate one remap per Fn Key.
Bytes does not seem to map either to HID (Keyboard/Consumer) usages or VK_ codes or scancodes.

e.g.
Volume Up:
Windows VK_VOLUME UP: 0xAF
Scancode 1: E0 30 / E0 B0
Scancode 2: E0 (F0) 32
HID Usage Page 0007 (Keyboard): 0080
HID Usage Page 000C (Consumer): 00E9

Could this be an internal key ID in the keyboard ? (How weird if that is the case)
Or maybe driver-internal IDs ?

#### KeymapNumlock

Very similar to KeymapFn, still can't tell what this does.

0c 5d
0d 59
0e 5a
0f 5b
10 62
12 5e
13 55 Same as Fn
18 5c
24 5f
25 60
26 61
27 54 Same as Fn
2d 67 Same as Fn
33 56 Same as Fn
37 63
38 57 Same as Fn

This one seems to "remap" more keys than the Fn one. (16 keys)
The numpad has 18 keys: 0 - 9, Decimal, Clear (NumLock), Equal (Clear), Divide, Multiply, Minus, Plus, Enter

It might be safe to assume that Clear/NumLock and Equal/Clear are not remapped. Which then makes 16 keys, corresponding to the 16 lines above.

#### More on keymaps

It would seem like only "keymap" is used on modern (true HID) keyboards, and that keymapFn and keymapNumlock are there for old keyboards. (OLD_LAYOUT, as opposed to NEW_LAYOUT and NEW_LAYOUT2)
Probably not worth digging there any more 🙁

### IOCTL used by the Apple USB keyboard driver

⚠️ Values seem to be the same for most keyboards (with this driver version at least), with no guarantee that they will always work.
Some keyboard drivers do seem to support less IOCTL. Minimum of IOCTL_KEYBOARD_SET_OSX_FN_BEHAVIOR is always supported.

HEX        Name                               InputLength OutputLength Parameters
0xb4032018 IOCTL_KEYBOARD_GET_OSX_FN_BEHAVIOR 0           4            BOOL (0: Disabled; 1: Enabled)
0xb403201c IOCTL_KEYBOARD_SET_OSX_FN_BEHAVIOR 4           0            BOOL (0: Disabled; 1: Enabled)
0xb4032020 Palm rejection disable ?           0           0            (Used when an internal trackpad is found… Probably to make Apple drivers communicate and disable the trackpad when one is typing ?)
0xb4032024 Palm rejection enable ?            0           0
0xb4032048 IOCTL_ACPI_BRIGHTNESS_AVAILABLE    4           0            BOOL (1: ACPI Brightness Available)

Internal IO Control Codes (probably not useful; they mostly seem to return predetermined values anyway)
0xb4032007
0xb403200b
0xb403200f
0xb3
0xb4032017
0xb403201b
0xb4032043
0xb4032057
0xb403205b
0xb403205f

For Internal keyboards:
0x9c402460                                    5           12           'LKSB\0' ? (USB Keyboard)
0xb403204c                                    0           7            ? (SPI Keyboard)

Names or descriptions for the IOCTL were kindly provided by Bootcamp.exe's error message. (Thank you people @Apple, I guess 🙂)
Should be obvious, but there might be some other IOCTL operations available in the driver, that I haven't found yet.

Example:

````csharp
const uint IOCTL_KEYBOARD_SET_OSX_FN_BEHAVIOR = 0xb403201c;
const uint IOCTL_ACPI_BRIGHTNESS_AVAILABLE = 0xb4032048;

using var device = Device.OpenHandle(@"\\.\AppleKeyboard", DeviceAccess.ReadWrite);

// Enables OSX fn key behavior (function keys defaults to multimedia functions)
device.IoControl(IOCTL_KEYBOARD_SET_OSX_FN_BEHAVIOR, new byte[] { 1, 0, 0, 0 }, default);
// Disables OSX fn key behavior
device.IoControl(IOCTL_KEYBOARD_SET_OSX_FN_BEHAVIOR, new byte[] { 0, 0, 0, 0 }, default);
// Enable brightness control on F1/F2 keys. (Even if not a laptop. Saldy, still not wired to external monitor DDC by Windows 😕)
device.IoControl(IOCTL_ACPI_BRIGHTNESS_AVAILABLE, new byte[] { 1, 0, 0, 0 }, default);
// Disable brightness control on F1/F2 keys.
device.IoControl(IOCTL_ACPI_BRIGHTNESS_AVAILABLE, new byte[] { 0, 0, 0, 0 }, default);
````

# HID Extract from Windows APIs

## Without Apple Driver

╔═══════════════════════════════════════
║ Device Type: Hid
║ Device Name: \\?\HID#VID_05AC&PID_0221&MI_01#a&1a6297a2&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}
║ Device Manufacturer: Apple, Inc
║ Device Product Name: Apple Keyboard
║ Device Vendor Name: Apple, Inc.
║ Device Product Name: Aluminum Keyboard (ISO)
║ Device Vendor ID: 05AC
║ Device Product ID: 0221
║ Device Version Number: 71
║ Device Usage Page: Consumer
║ Device Usage: ConsumerControl
║ Link Collection Nodes: 1
║ ╒═══════ Node #0
║ │ Collection Type: Application
║ │ Node Usage Page: Consumer
║ │ Node Usage: ConsumerControl
║ │ Is Alias: False
║ │ Parent: 0
║ │ Next Sibling: 0
║ ╘═══════
║ ╒═══════ Input Button #0
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: Consumer
║ │ Collection Usage: ConsumerControl
║ ├───────
║ │ Button Usage Page: Consumer
║ │ Button Usage: PlayPause
║ │ Data Index: 0
║ │ Designator Index: 0
║ │ Is Absolute: False
║ │ Is Alias: False
║ ╞═══════ Input Button #1
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: Consumer
║ │ Collection Usage: ConsumerControl
║ ├───────
║ │ Button Usage Page: Consumer
║ │ Button Usage: ScanNextTrack
║ │ Data Index: 1
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╞═══════ Input Button #2
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: Consumer
║ │ Collection Usage: ConsumerControl
║ ├───────
║ │ Button Usage Page: Consumer
║ │ Button Usage: ScanPreviousTrack
║ │ Data Index: 2
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╞═══════ Input Button #3
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: Consumer
║ │ Collection Usage: ConsumerControl
║ ├───────
║ │ Button Usage Page: Consumer
║ │ Button Usage: Eject
║ │ Data Index: 3
║ │ Designator Index: 0
║ │ Is Absolute: False
║ │ Is Alias: False
║ ╞═══════ Input Button #4
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: Consumer
║ │ Collection Usage: ConsumerControl
║ ├───────
║ │ Button Usage Page: Consumer
║ │ Button Usage: Mute
║ │ Data Index: 4
║ │ Designator Index: 0
║ │ Is Absolute: False
║ │ Is Alias: False
║ ╞═══════ Input Button #5
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: Consumer
║ │ Collection Usage: ConsumerControl
║ ├───────
║ │ Button Usage Page: Consumer
║ │ Button Usage: VolumeDecrement
║ │ Data Index: 5
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╞═══════ Input Button #6
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: Consumer
║ │ Collection Usage: ConsumerControl
║ ├───────
║ │ Button Usage Page: Consumer
║ │ Button Usage: VolumeIncrement
║ │ Data Index: 6
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╘═══════
╠═══════════════════════════════════════
║ Device Type: Keyboard
║ Device Name: \\?\HID#VID_05AC&PID_0221&MI_00#a&974a620&0&0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}
║ Device Manufacturer: Apple, Inc
║ Device Product Name: Apple Keyboard
║ Device Vendor Name: Apple, Inc.
║ Device Product Name: Aluminum Keyboard (ISO)
║ Keyboard Type: 81
║ Keyboard SubType: 0
║ Keyboard Key Count: 265
║ Keyboard Indicator Count: 3
║ Keyboard Function Key Count: 12
║ Keyboard Mode: 1
║ Device Usage Page: GenericDesktop
║ Device Usage: Keyboard
║ Link Collection Nodes: 1
║ ╒═══════ Node #0
║ │ Collection Type: Application
║ │ Node Usage Page: GenericDesktop
║ │ Node Usage: Keyboard
║ │ Is Alias: False
║ │ Parent: 0
║ │ Next Sibling: 0
║ ╘═══════
║ ╒═══════ Input Button #0
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: GenericDesktop
║ │ Collection Usage: Keyboard
║ ├───────
║ │ Button Usage Page: Keyboard
║ │ Button Usage: LeftControl .. RightGui
║ │ Data Index: 0 .. 7
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╞═══════ Input Button #1
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: GenericDesktop
║ │ Collection Usage: Keyboard
║ ├───────
║ │ Button Usage Page: Keyboard
║ │ Button Usage: NoEvent .. 255
║ │ Data Index: 8 .. 263
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╘═══════
║ ╒═══════ Input Value #0
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: GenericDesktop
║ │ Collection Usage: Keyboard
║ ├───────
║ │ Is Nullable: False
║ │ Value Length: 8 bits
║ │ Report Count: 1
║ │ Units Exponent: 0
║ │ Units: 0
║ │ Logical Min: 0
║ │ Logical Max: 255
║ │ Physical Min: 0
║ │ Physical Max: 0
║ ├───────
║ │ Value Usage Page: 255
║ │ Value Usage: 03
║ │ Data Index: 264
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╘═══════
║ ╒═══════ Output Button #0
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: GenericDesktop
║ │ Collection Usage: Keyboard
║ ├───────
║ │ Button Usage Page: Led
║ │ Button Usage: 01 .. 05
║ │ Data Index: 0 .. 4
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╘═══════
╚═══════════════════════════════════════

### With Apple Driver

╔═══════════════════════════════════════
║ Device Type: Keyboard
║ Device Name: \\?\HID#VID_05AC&PID_0221&MI_00#a&974a620&0&0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}
║ Device Manufacturer: Apple, Inc
║ Device Product Name: Apple Keyboard
║ Device Vendor Name: Apple, Inc.
║ Device Product Name: Aluminum Keyboard (ISO)
║ Keyboard Type: 81
║ Keyboard SubType: 0
║ Keyboard Key Count: 265
║ Keyboard Indicator Count: 3
║ Keyboard Function Key Count: 12
║ Keyboard Mode: 1
║ Device Usage Page: GenericDesktop
║ Device Usage: Keyboard
║ Link Collection Nodes: 1
║ ╒═══════ Node #0
║ │ Collection Type: Application
║ │ Node Usage Page: GenericDesktop
║ │ Node Usage: Keyboard
║ │ Is Alias: False
║ │ Parent: 0
║ │ Next Sibling: 0
║ ╘═══════
║ ╒═══════ Input Button #0
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: GenericDesktop
║ │ Collection Usage: Keyboard
║ ├───────
║ │ Button Usage Page: Keyboard
║ │ Button Usage: LeftControl .. RightGui
║ │ Data Index: 0 .. 7
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╞═══════ Input Button #1
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: GenericDesktop
║ │ Collection Usage: Keyboard
║ ├───────
║ │ Button Usage Page: Keyboard
║ │ Button Usage: NoEvent .. 255
║ │ Data Index: 8 .. 263
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╘═══════
║ ╒═══════ Input Value #0
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: GenericDesktop
║ │ Collection Usage: Keyboard
║ ├───────
║ │ Is Nullable: False
║ │ Value Length: 8 bits
║ │ Report Count: 1
║ │ Units Exponent: 0
║ │ Units: 0
║ │ Logical Min: 0
║ │ Logical Max: 255
║ │ Physical Min: 0
║ │ Physical Max: 0
║ ├───────
║ │ Value Usage Page: 255
║ │ Value Usage: 03
║ │ Data Index: 264
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╘═══════
║ ╒═══════ Output Button #0
║ │ Report ID: 0
║ │ Collection Index: 0
║ │ Collection Usage Page: GenericDesktop
║ │ Collection Usage: Keyboard
║ ├───────
║ │ Button Usage Page: Led
║ │ Button Usage: 01 .. 05
║ │ Data Index: 0 .. 4
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╘═══════
╠═══════════════════════════════════════
║ Device Type: Hid
║ Device Name: \\?\HID#VID_05AC&PID_0221&MI_01#a&1a6297a2&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}
║ Device Manufacturer: Apple, Inc
║ Device Product Name: Apple Keyboard
║ Device Vendor Name: Apple, Inc.
║ Device Product Name: Aluminum Keyboard (ISO)
║ Device Vendor ID: 05AC
║ Device Product ID: 0221
║ Device Version Number: 71
║ Device Usage Page: Consumer
║ Device Usage: ConsumerControl
║ Link Collection Nodes: 1
║ ╒═══════ Node #0
║ │ Collection Type: Application
║ │ Node Usage Page: Consumer
║ │ Node Usage: ConsumerControl
║ │ Is Alias: False
║ │ Parent: 0
║ │ Next Sibling: 0
║ ╘═══════
║ ╒═══════ Input Button #0
║ │ Report ID: 82
║ │ Collection Index: 0
║ │ Collection Usage Page: Consumer
║ │ Collection Usage: ConsumerControl
║ ├───────
║ │ Button Usage Page: Consumer
║ │ Button Usage: Undefined .. 255
║ │ Data Index: 0 .. 255
║ │ Designator Index: 0
║ │ Is Absolute: True
║ │ Is Alias: False
║ ╘═══════
╚═══════════════════════════════════════
