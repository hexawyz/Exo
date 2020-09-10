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

From what I remember, may need to confirm again later. (Especially on the values)

We can be thankful enough that the fn key status is both reported in a simple way and also always reported.
However, the "clever" way it is reported has some implications:

* It is reported as the 8th key in the buffer (standard buffer size for a USB keyboard is 8, which is also what Apple reports)
* The key can be either 0x00 (no key press) or 0x80 
* The key is not seen by the OS because the OS should stop parsing keys in the buffer as soon as it encounters 0x00
* This would imply that the keyboard can never return data for more than 6 keys + fn key ?

# HID Extract from Windows APIs

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