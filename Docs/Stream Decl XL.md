# Stream Deck XL

The Stream Deck XL from Elgato has 32 configurable buttons, each sporting an individual display, arranged in a 8x4 layout.

Internally, as seen in the multiple teardowns, the device uses a single screen for all buttons.

The device does not have much intelligence outside of decoding JPEG and drawing images in the proper part of its internal framebuffer.
Everything has to be controlled from a client application.

## HID Protocol

The device uses report IDs from `01` to `0C` to communicate.
All feature reports are 32 bytes long, including the report ID.
All data is presented in little endian form.

### Input report `01` - Device events

This report is 512 bytes long.

```
01 00 <ButtonCount:U8> <Unknown:U8> <IsButtonDown:U1[]>
```

Each time the state of a button changes, the device should send this packet including the status of all buttons.

### Output report `02` - Packet writes

This report is 1024 bytes long. It is split in multiple commands used to transfer data to the device.

#### Command `05` - Firmware update

```
02 05 <SectorIndex:U8> <IsFinalPacket:B8> <IsLastSector:B8> <PacketIndex:U16> <PacketLength:U16> 02 <Zero:U8[6]> <Data:U8[]>
```

This command is similar to the image write commands, but the data is written in 4k sectors.
So, each write for one sector is composed of 4 transfers of 1008 bytes followed by one transfer of 64 bytes, except for the last sector written.

Impossible to know for sure, but the fixed `02` byte might indicate a specific firmware to update.
As there are up to three versions returned, one can assume that there are things like bootloader firmware, etc.

There does not seem to be any command to prepare or complete a firmware update, except for the writes themselves.
The firmware transfer is completed by a device reset, then the device should restart. (In order: Set sleep timer; Reset; Set brightness)

Firmware 1.01.000 end is 115272 bytes.

#### Command `07` - Write button image data

```
02 07 <ButtonIndex:U8> <IsFinalPacket:B8> <PacketLength:U16> <PacketIndex:U16> <Data:U8[]>
```

#### Command `09` - Write screensaver data

```
02 09 08 <IsFinalPacket:B8> <PacketIndex:U16> <PacketLength:U16> <Data:U8[]>
```

#### Command `0C` - Stream Deck + - Write image region

```
02 0C <Left:U16> <Top:U16> <Width:U16> <Height:U16> <IsFinalPacket:B8> <PacketIndex:U16> <PacketLength:U16> <Zero:U8> <Data:U8[]>
```

Stream Deck + devices would also include a touch screen, that can be updated entirely or partially using this command.

### Feature report `03` - Write - Device configuration

#### Command `02` - Reset

```
03 02
```

This command will reset the display and put the device in screensaver mode.

#### Command `05` - ???

Sent when the software is starting up. Possibly an initialization command that would switch the screen out from sleep mode.

#### Command `06` - ???

#### Command `08` - Brightness

```
03 08 <Brightness:U8>
```

#### Command `0D` - Idle Sleep Timer

```
03 08 <Duration:U32>
```

The idle sleep timer is defined in seconds.

### Feature report `04` - Read - Some firmware version

```
04 <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Version:ASCII[8]>
```

Version numbers use the format `0.00.000`.

Known version numbers are:

* 0.01.007

### Feature report `05` - Read - Main firmware version

```
05 <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Version:ASCII[8]>
```

Version numbers use the format `0.00.000`.

The version number returned by this command is the one displayed in the official software.

Known firmware version numbers are:

* 1.00.012
* 1.01.000

### Feature report `06` - Read - Serial number

```
06 <SerialNumber:ASCII[12]>
```

### Feature report `07` - Read - Some firmware version

```
07 <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Version:ASCII[8]>
```

Version numbers use the format `0.00.000`.

Known version numbers are:

* 1.00.008

### Feature report `08` - Read - Device information

```
08 <RowCount:U8> <ColumnCount:U8> <ButtonImageWidth:U16> <ButtonImageHeight:U16> <FramebufferWidth:U16> <FramebufferHeight:U16> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U16> <Unknown:U16>
```

Example:

```
08 04 08 6000 6000 0004 5802 02 00 01 04 04 00 0000 0000 0000000000000000000000
```

NB: This likely includes touch screen width and height for SD+ devices.

### Feature report `09` - Read - Device total uptime

```
09 <Unknown:U8> <Unknown:U8> <Unknown:U8> <Unknown:U8> <Duration:U32>
```

Example values for the unknown bytes: `04 00 02 00`

### Feature report `0A` - Read - Sleep timer

```
09 <Unknown:U8> <Duration:U32>
```

Example:

```
0A 04 201C
```

### Feature report `0B` - Read - ???

This seems to return a single byte. (`02`)

### Feature report `0C` - Read - ???

This seems to return a single byte. (`08`)
