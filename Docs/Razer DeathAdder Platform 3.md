# Razer Platform 3

Platform 2 and Platform 3 are the names given by Razer for this protocol used for Chroma devices and newer. (Also saw a reference to "Protocol 2.5" in the files 🤷)
This protocol uses 90 byte packets sent and received through HID Feature GET/SET on report ID `00`.

Razer generally installs kernel drivers to access those devices, although their use does not seem to be strictly necessary.
When the driver is installed, it is necessary to access the device through the driver, as it will hide the 90 bytes report ID `00` from the OS.

Working with Razer drivers is possible, and newer drivers seem to use a very simple layout with IOCTLs serving as drop-in replacements of GET/SET Feature.

# Protocol details

## General observations

Data larger than one byte in the protocol tend to use the Big Endian byte order.

Percentages are generally expressed as byte values from 0 to 255 instead of 0 to 100. (e.g. 33% will be the value 84 or 0x54)

## Format used in message structures below

<NAME:TYPE> => This is an element of the message named NAME with the following TYPE.

TYPE:
U8: 8-bit unsigned integer
U16: 16-bit unsigned integer
RGB: <R:U8> <G:U8> <B:U8>
P8: percentage scaled from 0 to 255, expressed as a 8-bit unsigned integer.
X[N]: Array of N items of type X

## Device notifications

Because the core protocol is based on raw get/set feature calls on the root device itself and does not actually follows the standard HID operation mode, hence requiring specific drivers,
notifications are emitted on a separate and much more standard channel. They will use standard HID output reports accessible through standard ReadFile calls (FileStream in .NET).

These HID reports are exposed on Device Interface 5.
The report ID `05` is used to transmit notifications from the connected device or its child devices, as packets of exactly 16 bytes including the report ID.
Other report IDs with the same structure exist, but I've not observed them in any trace yet.
Because notifications do not always contain a device ID, I suspect these other reports could be used in this case?

There are also notifications on other report IDs, whose meaning is yeat to be determined.

### Structure of a notification.

All notifications follow a very simple format:

``` 
<Report ID:U8> <ID:U8> <Data:U8[14]>
```

The HID report ID is part of the HID protocol and will be `05`.

The ID is a notification ID.

The rest is message data associated with the notification ID.

### Known notifications

#### 02 - Device DPI

Structure:

```
<DPI X:U16> <DPI Y:U16>
```

This notification will be sent when a DPI change request has been processed by the device as the result of clicking one of the DPI buttons.
It will always be sent, even if the DPI did not actually change, for example if it was already at the minimum or maximum setting.

#### 09 - Device Connection Status Change

Structure:

```
<Status:U8> <Device Index:U8>
```

This notification is sent when a device connected to a receiver changes from being offline to online, or from being online to offline.

The Status will be `02` if the device is now offline, and `03` if the device is now online. (Or `00` if unspecified; see below)

The device index seems like a fair interpretation, but it stills need to be confirmed.

⚠️ On older devices, this notification contains no parameter. This can be identified from the value `00` in the status.

#### 0C - External Power

Structure:

```
<Status:U8>
```

This notification is sent when the device is connected to or disconnected from external power.

Status will be `00` if the device is not connected and `01` if the device is connected.

NB: Just thinking about it, but maybe the format of the notification explained above is entirely wrong and that the contents of this notification are actually the connected device IDs?
It is impossible to tell without observing a dongle with two devices appaired.

#### 10 - ??? (KeepAlive?)

This notification is seen (only?) when the device is in BLE mode, and seems to actually spam when the device is put on the dock.

#### 31 - Battery Level

Structure:

```
<BatteryLevel:U8> <Unknown:U8> <Unknown:U8> <Device Index?:U8>
```

````
31 BB 01 01 01 - Wired / Charging`
31 BD 01 01 01 - Wired / Charging
31 C0 01 01 01 - Wired / Charging
31 C2 01 01 01 - Wired / Charging
31 C5 01 01 01 - Wired / Charging
31 CA 00 00 01 - Dongle / Discharging
31 CA 01 01 01 - Dongle / Docked / Charging
31 FF 01 01 01 - Dongle / Docked / Charge complete
````

This notification contains the current battery level as well as three other bytes of data, whose meaning is not yet known.
It also does not trigger very often, but it seems that it does trigger periodically for a data refresh. (With a very long period, explaining why it is so easy to miss)
Occurrences of this notification can be followed by the external power notification (which can also happen alone, more often?), indicating that none of the bytes here might be related to external power.

Also, it seems that the value can quickly fluctuate sometimes (e.g. `ca` to `c7` to `ca`).
Given the values observed, I'm assuming that the [0..255] values are actually [0..100] scaled up to [0..255] for some reason.
The last byte could be the device index maybe, however, it is weird that the device index does not seem to exist on the external power notification.
The two bytes before seem to be correlated with external power, but it is difficult to know what they mean exactly, as one byte would be enough for external power status.

## Function/Register based protocol

The core protocol is composed of SET/GET feature calls to the root device itself.
On Windows, Razer install a special driver to communicate with the device.
The official Razer driver will expose a new "Razer control" device interface to communicate.

For the newer drivers (at the time of writing), the following IO Control Codes are used to implement the GET and SET feature calls:

- SetFeature: 0x88883010
- GetFeature: 0x88883014

Packets are always 91 bytes long, including the report ID which is `00`.
This means that commands themselves are 90 bytes long.

Without the driver, the feature report #00 will be exposed on the Mouse device interface (the same one that is opened in exclusive mode by Windows).
This might be the reason why Razer felt the need to install a custom driver (that, or maybe some more obscure features?),
however it is possible to send ioctl on devices opened without any R/W access.
This means that the driver is actually not needed to communicate.

All messages follow a common pattern:

```
<Status:U8> <CommunicationID:U8> <Zero:U8[3]> <Length:U8> <Feature:U8> <Read:U1> <Function:U7> <Data:U8[80]> <Checksum:U8> <Zero:U8>
```

The Status byte will be as follows:

- `00` Request
- `01` Retry again
- `02` Success
- `03` Error (Invalid Parameter ?)
- `04` Device Not Available
- `05` Error (Invalid Parameter ?)

The following byte, labelled as CommunicationID here, is likely indicating a device or internal sub-device to communicate with.
I suspect this indicates the device to communicate. Maybe a channel ID or internal chip ID.
Observed values for this field are `08`, `1F`, `3F`, `88` and `9F`. (Depending on the device)
For some reason, the devices do not seem to be too picky about which value is used, but it can be important in some cases.
(e.g. Synapse uses `3F` to communicate with Mamba Chroma and `1F` for the few "management" calls if that's what they are, but `1F` somehow seems to just always work ?)

These are followed by three bytes seemingly always zero.

The next three bytes form the actual command:

The first byte indicates the data length. For writes, it is simply the data length following the function ID.
For read requests, it is a bit more unclear, and it could indicate the expected maximum length of the response.

The second byte is a feature category ID that we can compare to what is used by logitech HID++.

The third byte's MSB indicates if the operation is a read operation. (`1` is a read; `0` is a write)
The 7 remaining bits indicate the register or function ID.

e.g. `80` means "Read register 0", and `00` means "Write register 0"

⚠️ Still needs confirmation for values such as `bb`: Is it paired with `ab`, or is it something else ?

All the bytes following this, up to the last two, represent the command data.

This data is followed by a checksum byte which is a XOR checksum of all previous bytes but the first two bytes. It is followed by a null byte.

Unless the command was a successful read, status responses seem to always include the whole original command, meaning the checksum is also unchanged in that case.

### Features

#### Category `00` - General

##### Function `00`:`01` - ???

##### Function `00`:`02` - Serial Number

Read Request: Empty

Read Response:

```
<Serial Number:U8[]>
```

The serial number is a null-terminated string. (If less long than the response buffer)

This function does not work when the device is offline.

##### Function `00`:`04` - Mode

Read:

Write:

```
<Mode:U8> <Unknown:U8>
```

The only values observed across devices I own are `03 00` and `00 00`.

This function works when the device is offline.

Valid values seem to be `00`, `01`, `03`, `04`, `05`, `06`. (`02` is apparently invalid)

`00` would be the "normal" mode ?
`01` might be DFU mode ? (Observed when the FW updater was launched)

##### Function `00`:`05` - Polling Frequency

Read Response:

```
<Divider:U8>
```

Write:

```
<Divider:U8>
```

The parameter to this command is a frequency divider from the maximum frequency. (How do we know the maximum frequency then ?)

e.g. 1000Hz is 1, 500Hz is 2, 125Hz is 8


##### Function `00`:`06` - Keyboard Layout

Some devices return information here.

Data length can be `02` or `03` at least? (`03` for Mamba Chroma when ID is `1F` but `03` when ID is `3F` ?)

Read Response:

```
<Unknown:U8> <Unknown:U8>
```

##### Function `00`:`07` - Firmware Version ?

* Mamba Chroma: `01 00 08 00`
* DeathAdder V2 Pro: `02 02 01 00`

See here: https://mysupport.razer.com/app/answers/detail/a_id/4557/~/razer-deathadder-v2-pro-firmware-updater-%7C-rz01-03350

The latest firmware for DeathAdder V2 Pro is 2.05.01, so this is likely it.

This function works when the device is offline. (Returns the dock/dongle FW version?)

##### Function `00`:`08` - Game Mode / Key Cover - On / Off

```
<Boolean:U8> <Zero:U8[3]>
```

##### Function `00`:`12` - Dock Serial Number

Read Request: Empty

Read Response:

```
<Serial Number:U8[16]>
```

This will return the S/N of a mouse dock, as written on the sticker.
It does however not return the S/N for USB dongles.

##### Function `00`:`13` - Receiver Firmware Version

```
00 00 000000 080093 00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000009b00
02 00 000000 080093 02 02 01 00 00 00 00 000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000009a00
```

Observed by launching the DA V2 Pro FW Updater. Sent on ID zero though. (Does it actually matter?)

##### Function `00`:`15` - ???

7 values (Last might be a boolean)

##### Function `00`:`22` - Wireless Connection Status

```
<Boolean:U8>
```

##### Function `00`:`23` - Bluetooth Signal

```
<Value:U16>
```

##### Function `00`:`1F` - ???

```
<Value1:U8> <Value2:U8> <Value3:U8> <Value4:U8>
```

##### Function `00`:`3B` - ???

##### Function `00`:`3C` - ???

```
001f0000000300bc0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000bf00
021f0000000300bc0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000bf00
```

##### Function `00`:`3F` - Pairing information

Read Request:

```
<DeviceCount:U8>
```

Read Response:

```
<DeviceCount:U8> <DeviceInfo:<Status:U8> <ProductID:U16>[]>
```

It seems that for the input, the device count, is the requested maximum count ?
Some requests have the value `02` and some have the value `10`, but all the responses have `02`.
The value `10` makes sense, as the message length `31` could fit exactly 16 device information structures. (Each is 3 bytes long)

For each device, the connection status is either `01` if the device is online, or `00` if the device is offline.
The product ID is the USB product ID. However there is a quirk there, as receivers share the same product ID as their associated product.

##### Function `00`:`45` - Device ID ?

Read Request: Empty

Read Response:

```
<DeviceCount:U8> <ProductID:U16[]>
```

This returns similar information to the pairing information stuff.

Maybe this is used to confirm the ID of the connected device ? It would be useful for multiple device stuff, I guess.

#### Category `02` - Keyboard

##### Function `02`:`00` - Mappings

(Length 9, with one parameter)

##### Function `02`:`01` - Single Hard Switch Status

```
<Parameter:U8> <Boolean:U8>
```

##### Function `02`:`06` - Function Key Alternate State

```
<Parameter:U8> <Value:U8> <Value?:U8>
```

Value might be a boolean.

#### Category `03` - Lighting V1

This feature is used by older Chroma devices. Newer ones like DeathAdder V2 Pro will use `0F`.

Apparently, persistence is supposed to be disabled by default for LedIds `03`, `07` and `09`, whatever the meaning is.
Also disabled for device PID `0118` (USB ID Database: RZ03-0080, Gaming Keyboard [Deathstalker Essential])

Most functions here do a very little focused change and, multiple functions need to be called to achieve a specific effect.

Function `0A` is different in that regards, as it will set an effect in a single call.
It does however seem to override both mouse and dock effects in the same call, as it is the only one not having a Led ID specified.

##### Led IDs

Led IDs are hardcoded values, which we can find a list of (names) in the strings of some DLLs.
A device will only have a specific set of Led IDs, often only one. Those presumably represent lighting zones composed of one or more LEDs.

0. None
1. ScrollWheel
3. Dpi
4. Battery
5. Logo
6. Backlight
7. Apm
8. Macro
9. GameMode
10. WirelessConnected
11. UnderGlow
12. SideStripe
13. KeyMapRed
14. KeyMapGreen
15. KeyMapBlue
16. Dongle
17. RightIo
18. LeftIo
19. AltLogo
20. Power
21. Suspend
22. Fan
23. DonglePower
24. MousePower
25. Volume
26. Mute
27. Port1
28. Port2
29. Port3
30. Port4
31. Port5
32. Port6
33. Charging
34. FastCharging
35. FullCharging
36. IosLedArray
37. Knob

##### Precedence of individual effect states

For correctly applying an effect using the "normal" functions (per Led ID), it is necessary to have in mind the priorities of various settings.

1. Mouse synchronization effect: Will override everything else. (This effect is for mouse docks only)
2. LED On/Off state: Disabled state will override everything other than the mouse synchronization effect
3. Effect ID: Effect parameters can be set at any time, but they won't apply unless the effect ID has been selected.
4. Effect parameters: Some effects take extra parameters. These can be set at any time and will only be used when applicable.

##### Function `03`:`00` - LED On/Off

Read Request:

```
<Persist:U8> <LedId:U8>
```

Write:

```
<Persist:U8> <LedId:U8> <Boolean:U8>
```

##### Function `03`:`01` - LED Color

Read Request:

```
<Persist:U8> <LedId:U8>
```

Write:

```
<Persist:U8> <LedId:U8> <Color:RGB>
```

##### Function `03`:`02` - Lighting effect V1

Read Request:

```
<Persist:U8> <LedId:U8>
```

Write:

```
<Persist:U8> <LedId:U8> <Effect:U8>
```

##### Function `03`:`03` - Brightness

Read Request:

```
<Persist?:U8> <LedId:U8>
```

Read Response:

```
<Persist:U8> <LedId:U8> <Brightness:P8>
```

Write:

```
<Persist?:U8> <Zero:U8> <Brightness:P8>
```

This writes the lighting level used for all lighting on the device.

##### Function `03`:`04` - Period

Read Request:

```
<Persist:U8> <LedId:U8>
```

Write:

```
<Persist:U8> <LedId:U8> <Value1:U8> <Value2:U8>
```

##### Function `03`:`05` - Parameter

Read Request:

```
<Persist:U8> <LedId:U8>
```

Write:

```
<Persist:U8> <LedId:U8> <Values:U6[9]>
```

##### Function `03`:`07` - ???

Read Request:

```
<Persist:U8> <LedId:U8>
```

Write:

```
<Persist:U8> <LedId:U8> <Value1:U16> <Value2:U16>
```

##### Function `03`:`0A` - Lighting effect V2

This one will set the lighting effect on the mouse.

Format of the command is dependent on the effect, however, the first byte is always the effect ID.

##### Function `03`:`0B` - LED Matrix Custom V1 ?

##### Function `03`:`0C` - LED Matrix Custom V2 ?

```
<Offset:U8> <Count:U8> <Colors:RGB[]>
```

##### Function `03`:`0D` - Lighting effect V3 ?

###### Read Response and Write

```
<Effect:U8>
```

##### Function `03`:`0E` - LED Pulsating Extended Param

When setting an effect on the Mamba Chroma Dock:

```
00 3f 000000 09030e 01 0f 02 00ffff ff00ff 00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000800
```

This sets the breathing effect parameters for the specified led. It does not enable the breathing effect, which needs to be enabeld separately.

##### Function `03`:`0F` - Dock Lighting Synchronization

```
<Persist:U8> <LedId:U8> <Boolean:U8>
```

This would be the command used to synchronize the dock lighting effect with the mouse's lighting effect.

##### Function `03`:`10` - Charging Effect

```
<Boolean:U8>
```

#### Category `05` - Buttons ?

(From what I found from Synapse)

#### Category `04` - Mouse

##### Function `04`:`01` - DPI Level V1

Write:

```
<DpiX:U8> <DpiY:U8> <DpiZ:U8>
```

The DPI is represented using bytes that represent the DPI by increments of 25, starting at DPI 0 representing value 100.

Like all DPI calls, this seems to include a Z value which is never used.

##### Function `04`:`03` - Predefined DPI levels V1

##### Function `04`:`04` - Active DPI preset V1

Obsoleted by function `06` in devices that support it.

##### Function `04`:`05` - DPI Level V2

Read Request:

```
<Persisted:U8>
```

Read Response:

```
<???:U8> <DpiX:U16> <DpiY:U16>
```

The first byte has been observed to be both `00` and `01` depending on the situation.
Logically, it would either indicate that the setting must be or is persisted, or that the horizontal and vertical DPIs are linked.
The first possibility might be the most likely.

##### Function `04`:`06` - Predefined DPI levels V2

Read Request:

```
<Persisted:U8>
```

Read Response/Write Request:

```
<Persisted:U8> <ActiveProfile:U8> <ProfileCount:U8> <DpiPresetList:<DpiPreset:<Index:U8> <DpiX:U16> <DpiY:U16> <DpiZ:U16>>[]>
```

This is still guesswork, but assuming the format of the packets we received it makes sense that each profile would define three DPI values for X, Y and Z, with Z being zero if unsupported.
The meaning of the first byte is assumed to be the persistance state, and should be `00` for volatile memory and `01` for non-volatile memory. This could be wrong, though.
The Razer Synapse software only seems to write values where the first byte is set to `01`.
The second byte has been confirmed by simple testing to be the active profile index (e.g. `04` is the 4th profile on a mouse with `05` profiles).
It follows logically that the following byte should be the number of profiles.

##### Function `04`:`09` - Sensor Parameter

Read Request:

```
<Parameter:U8>
```

Read Response:

```
<Parameter:U8> <Value1:U16> <Value2:U16> <Value3:U8> <Value4:U8>
```

Write:

```
<Parameter:U8> <Value1:U16> <Value2:U16> <Value3:U8> <Value4:U8>
```

##### Function `04`:`09` - Sensor Control

Read Request:

```
<Parameter:U8>
```

Read Response:

```
<Parameter:U8> <Value1:U8> <Value2:U8> <Value3:U8>
```

Write:

```
<Parameter:U8> <Value1:U8> <Value2:U8> <Value3:U8>
```

##### Function `04`:`0A` - Raw ADC value

Read Request:

```
<Parameter:U8>
```

Read Response:

```
<Parameter:U8> <Value1:U16>
```

##### Function `04`:`10` - ???

Read Request:

```
<Parameter:U8>
```

Read Response:

```
<Parameter:U8> <Value1:U8> <Value2:U8> 
```

Write:

```
<Parameter:U8> <Value1:U8> <Value2:U8>
```

##### Function `04`:`11` - ???

Read Request:

```
<Parameter:U8>
```

Read Response:

```
<Parameter:U8> <Value1:U8> <Value2:U8> 
```

Write:

```
<Parameter:U8> <Value1:U8> <Value2:U8>
```

#### Category `05` - Profiles

##### Function `05`:`00` - Number of profiles

```
<Count:U8>
```

This returns one byte, whose value is `01` for DeathAdder V2 Pro.
For DeathStalker V2 Pro and Naga V2 Pro, the response seems to be `05`?

##### Function `05`:`01` - List of Profiles

```
<Count:U8> [<Index:U8> […]]
```

This command returns at least 2 bytes, but the request indicates 65 bytes.

* DeathAdder V2 Pro: `05 01`.
* DeathStalker V2 Pro and Naga V2 Pro: `05 01 02 03 04 05`?

To investigate:

* Can the list be non-contiguous ?
* Are deleted profiles shifting the list or just leaving their spot empty

##### Function `05`:`02` - Create Profile

```
<Index:U8>
```

##### Function `05`:`03` - Delete Profile

```
<Index:U8>
```

##### Function `05`:`05` - Current Profile

```
<Index:U8>
```

##### Function `05`:`2C` - Profile Name

```
<Index:U8> <Length:U8> <Name:U16[21]>
```

Interesting that as for Logitech, the profile names are encoded using UTF-16 rather than UTF-8.

##### Function `05`:`0A` - ???

```
<Unknown:U8>
```

This returns one byte, whose value is `05` for DeathAdder V2 Pro, and apparently the same for DeathStalker V2 Pro and Naga V2 Pro.

##### Function `05`:`00` - ???

```
<Unknown:U8> <Unknown:U8>
```

Returns `00 00` on DeathAdder V2 Pro.
Returns `00 01` on DeathStalker V2 Pro and Naga V2 Pro.

##### Function `05`:`0E` - ???

This command returns 15 bytes.

```
DA V2 Pro:   00 64 00 05 e0 00 00 05 e0 00 00 00 00 00 00
DS V2 Pro:   00 64 00 05 f0 00 00 05 e6 00 00 00 08 00 00
Naga V2 Pro: 00 64 00 01 f0 00 00 01 e6 00 00 00 04 00 00
```

#### Category `06` - ???

#### Category `07` - Power

##### Function `07`:`00` - Battery Level

Read Request: Empty

Read Response:

```
<Zero:U8> <BatteryLevel:P8>
```

##### Function `07`:`01` - Low Power Mode

Write:

```
<BatteryLevel:P8>
```

The low power mode is entered when the device's battery level goes below the specified percentage.

##### Function `07`:`02` - Maximum Brightness

Write:

```
<Brightness:P8>
```

This sets the battery level of the mouse when it is wireless.

##### Function `07`:`03` - Power Saving

Write:

```
<Duration:U16>
```

The duration before the device enters sleep mode is expressed in seconds.

##### Function `07`:`04` - External Power Status

Read Request: Empty

Read Response:

```
<Status:U8>
```

Status is `01` if external power is connected, and `00` otherwise.

#### Category `08` - External GPU

##### Function `09`:`00` - Light Control

#### Category `09` - Display

##### Function `09`:`00` - Information

Length 22

##### Function `09`:`01` - Brightness

Length 3; 2 parameters

#### Category `0B` - Sensor

The functions in this category are mainly what is used by Synapse in the mouse calibration tab, which I never dared to use on my own mouse.

##### Function `0B`:`01` - Calibration Result

Length: 8

##### Function `0B`:`02` - Sensor Threshold

Write:

```
<Parameter1:U16> <Parameter2:U16> <Zero:U8[4]>
```

##### Function `0B`:`03` - Sensor State

Read request:

```
<Parameter1:U8> <Parameter2:U8> <Zero:U8>
```

Write:

```
<Parameter1:U8> <Parameter2:U8> <Parameter3:U8>
```

This command is emitted by Synapse for some reason.
Middle value on Mamba Chroma is `05`, and `04` on Razer DeathAdder V2 Pro and Razer Mouse Dock.

##### Function `0B`:`04` - Sensor Hardware

Read request and Write:

```
<Parameter1:U8> <Parameter2:U8> <Zero:U8[4]>
```

##### Function `0B`:`05` - Sensor Configuration

(Up to length 22, min length 3, including two byte parameters)

##### Function `0B`:`05` - Manual Calibration

(Length 7, two parameters)

##### Function `04`:`07` - Accuracy

Read Request:

```
<Parameter1:U8> <Parameter2:U8>
```

Read Response:

```
<Parameter1:U8> <Parameter2:U8> <Boolean:U8>
```

Write:

```
<Zero:U8> <Parameter2:U8> <Boolean:U8>
```

##### Function `0B`:`08` - Calibration Data

Read Request:

```
<Parameter1:U8> <Parameter2:U8>
```

Read Response:

```
<Parameter1:U8> <Parameter2:U8> <Values:U32[12]>
```

Write:

```
<Zero:U8> <Parameter2:U8> <Values:U32[12]>
```

Values should be in order:

* Offset_X
* Swing_X
* TIAGain_X
* RefHeightA_X
* RefHeightB_X
* TempVar_A
* TempVar_B
* Offset_Y
* Swing_Y
* TIAGain_Y
* RefHeightA_Y
* RefHeightB_Y

##### Function `0B`:`09` - Control / Start Calibration

```
<Parameter1:U8> <Parameter2:U8> <Parameter3:U8> <Zero:U8>
```

##### Function `0B`:`0A` - Lifted Indicator

Read Request:

```
<Parameter1:U8> <Parameter2:U8>
```

Read Response:

```
<Parameter1:U8> <Parameter2:U8> <Value:U8>
```

Write:

```
<Parameter1:U8> <Parameter2:U8> <Value:U8>
```

Value might be a boolean (it would make sense), will have to test.

##### Function `0B`:`0B` - ???

This is used by Synapse 3 (DA V2 Pro here):

```
00 1f 000000 040b0b 00 04 01 01 000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
```

#### Category `0C` - Controller

##### Function `0C`:`00` - Control ID List

Length 11

##### Function `0C`:`01` - Analog Control

2 parameters; 2 values

##### Function `0C`:`02` - Analog Control Sensitivity

2 parameters; 1 value

##### Function `0C`:`03` - Analog Control Threshold

2 parameters; U16 value

##### Function `0C`:`04` - Analog Control Polarity

2 parameters; 1 value

##### Function `0C`:`05` - Analog Control Zones

2 parameters; 3 U16 value

##### Function `0C`:`06` - Analog Control Motor Scaling

2 parameters; 1 value

#### Category `0D` - Sensors

##### Function `0D`:`01` - Speed

2 parameters; U16 value

##### Function `0D`:`01` - Thermal Mode

2 parameters; 1 value

#### Category `0E` - Lighting V2

##### Function `0E`:`04` - Brightness

Read Request:

```
<Persist?:U8>
```

Read Response:

```
<Persist:U8> <Brightness:P8>
```

Write:

```
<Persist?:U8> <Brightness:P8>
```

#### Category `0F` - Lighting V3

This lighting feature is for newer Razer devices. Older ones might use `03`.

Both features offer similar capabilities but there are actual differences.

For example, the persist flag is replaced by a profile id. This does however not make an actual difference for devices supporting a single profile.

##### Function `0F`:`00` - Lighting device information

Read Response:

```
[<LedId:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> […]]
```

This returns informations items about a lighting device. Maximum (requested) length is 50 bytes (`32`), and returned length is 5 * number of items.

Presumably, the first byte of these packets is the LedID.
This call should return the list of LEDs or lighting zones supported by the device, up to 10.

Meaning of values other than the first byte are unknown (but they do have a meaning for sure) and Synapse does not seem to use them either 🤷

Typical response examples:

* `04 19 03 02 02` (Razer DeathAdder V2 Pro)
* `05 19 03 01 02` (Razer Mouse Dock)

##### Function `0F`:`02` - Current Lighting Effect

Read Request:

```
<ProfileId?:U8> <Magic:U8>
```

Read Response:

```
<ProfileId?:U8> <Magic:U8> <Effect:U8> <Parameter0:U8> <Parameter1:U8> <ColorCount:U8> <Color0:RGB> <Color1:RGB>
```

Write:

```
<ProfileId:U8> <Zero:U8> <Effect:U8> <Parameter0:U8> <Parameter1:U8> <ColorCount:U8> <Color0:RGB> <Color1:RGB>
```

The parameters passed to read commands are usually `01` followed by the "magic" byte read from the device information earlier.
This value seems to be important, and *can* be passed at the same place in write responses, but it does not seem necessary.
Passing another value than this or zero in a write may result in weird behavior of the device.

##### Function `0F`:`03` - Current Color

Write:

```
<Zero:U8[5]> <Color:RGB>
```

This sets the current color of the device, in "streamable"/addressable mode.
It ignores the current effect and overrides it.

##### Function `0F`:`04` - Brightness

Read Request:

```
<ProfileId?:U8> <LedId:U8>
```

Read Response:

```
<ProfileId:U8> <LedId:U8> <Brightness:P8>
```

Write:

```
<ProfileId?:U8> <Zero:U8> <Brightness:P8>
```

This writes the lighting level used for all lighting on the device.

#### Category `10` - Firmware Update

##### Function `10`:`00` - Read some info

9 bytes; Seemingly contains the PID at the end

```
02 00 000000 091080 07 17 00 00 00 00 00 007c 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000f500
```

##### Function `10`:`01` - Update start ?

```
00 00 000000 081001 00 02 60 00 00 05 6e 14 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000400
```

##### Function `10`:`02` - Write packet

Contains a data length and an offset; Writes are donc by 64 bytes packet.

```
00 00 000000 081002 40 00 02 6c c0 007858b93a480078012807d10020384908700120354908702af036fe354800783549097888425bd03448007830b10020324908704ff4fa60314908602f48007800000000000000000000004700
```

Unsure about all the bytes; The command declares 8 bytes length so there might be 8 bytes of parameters.

First byte is the data length
Four following bytes is the memory offset ?

As we can see in the captured packet `10`:`01`, the transfer might start at `00 02 60 00`. We see data packets increasing from this value by steps of 64.

##### Function `10`:`03` - Read packet

Format similar to the write command. Would logically be used to verify the integrity of a firmware update.
Can maybe be used to dump the firmware of a device.

##### Function `10`:`05` - Device restart ?

This command is emitted at the very end of the update, just before the USB communication is cut. (It is actually cut in the middle of a second run of the command)
