# Asus Aura RAM

Most useful info can be found here: https://web.archive.org/web/20211011041834/https://gitlab.com/CalcProgrammer1/OpenRGB/-/wikis/ASUS-Aura-Overview
With the exception that the command to read bytes is actually 0x81 and not 0x01.

## Dumping the whole chip data

It can be useful to dump the whole address space of the chip in order to inspect what can be found. Operation takes a bit of time, so it should only be done for debugging.

For this, simply setting the R/W address to 0x0000 then doing 65536 incrementing reads will provide the entirety of the memory.
It seems that the reads can sometimes be a bit corrupted, but that might be user error.

From this dump, we can observe that at address 7FE0, there is what seems to be a chip ID:

````
20 18 03 05 "6K582"
````

I would assume that this indicates a date in BCD format, 2018-03-05, followed by the name of the chip "6K582".
This ID can very likely be used to identify the features that are supported and avoid operating on unsupported chips.

If we look for strings in the dump using strings64, we can find three strings:
"AUDA0-E6K5-0101
6K582
80000000

The string `AUDA0-E6K5-0101` is located at 0x1000, and is likely indicating a device ID.
I'm assuming this one is the device ID while "6K582" is the specific chip information.
This string seems to be what is used by OpenRGB to identify aura devices, so maybe we can also rely on it.
If we look at it, the "AUD" part probably means "AUra Device", and the "E6K5" is probably related to the chip "6K582". And the "0101" part could be a version number.

The string `80000000` is located at 80C8, and followed by a strange sequence of `0F 4F` bytes repeated 8 times.

It seems that the address 8586 would contain the RAM stick index, while address 80F8 will be easily overwritten on any stick of RAM?

## RAM Addresses

On my system, the RAM sticks were mapped at SMBus addresses 0x39, 0x3A, 0x3B and 0x3C.

For initial setup of the ram sticks, we can probe those addresses using SMB quick writes, if I refer to the method used by i2cdetect in this case.
Otherwise, it seems the addresses 73, 74, 75, 76 are still valid candidates.

## Useful addresses

Many addresses were identified as part of the OpenRGB reverse engineering efforts.
See:

* https://web.archive.org/web/20211010175028/https://gitlab.com/CalcProgrammer1/OpenRGB/-/wikis/Aura-Controller-Registers
* https://gitlab.com/CalcProgrammer1/OpenRGB/-/issues/3 "Identify motherboard, RAM, etc. combinations and build a list of them"
* https://gitlab.com/CalcProgrammer1/OpenRGB/-/issues/8 "Different RGB RAM protocols and their findings"
* https://gitlab.com/CalcProgrammer1/OpenRGB/-/issues/16 "Investigate device configuration table"
* https://gitlab.com/CalcProgrammer1/OpenRGB/-/issues/271 "crosshair hero viii + Trident Z neo wrong led count"

### 1C02 or 1C03 - LED count

Need to investigate when to use which address. On my set of memory modules, both addresses contain 08.

### 7FE0‥7FE4 - MCU chip manufacture date ? (version ?)

This would likely contain the date at which the MCU was manufactured, in BCD format, YYYYMMDD

### 7FE5‥7FE8 - MCU chip name

This contains the name of the MCU.

Known values:

* 6K582

### 8000‥800E - Dynamic colors (Legacy)

This is for legacy controllers that have at most 5 LEDs.
This contains 5 colors in RBG order (*not* RGB).
These colors are used when the controller is set to dynamic colors.
Updates to these colors are applied immediately, and as such, updates will occur faster, as they will require a single block transfer SMBus operation rather than multiple separate operations.

### 8000‥800E - Static colors (Legacy)

This is for legacy controllers that have at most 5 LEDs.
This contains 5 colors in RBG order (*not* RGB).
These colors are used for predefined effects.
Updates to these colors are applied only when changes are applied by writing at `80A0`.

### 8020 - Enable dynamic colors

This allows choosing between the dynamic colors mode and the static color mode. Value `00`

### 8021 - Predefined effect

This allows choosing the predefined effect that is displayed when the MCU is in predefined effect mode.

Modes:

* `00` Disabled
* `01` Static
* `02` Pulse
* `03` Flash
* `04` Color Cycle
* `05` Color Wave
…

### 8022 - Frame Delay

Setting the speed to maximum in RGB Fusion 2.0 updates this value to `FE`. Setting this to the preceding speed setting updates this values to `FF`…
Because RGB Fusion 2.0 is generally refreshing the sticks, I'm wondering if this couldn't be an automatic refresh setting that would ask the module to reset its effect after N cycles ?

Observed values:
* Speed setting 0: `02`
* Speed setting 1: `01`
* Speed setting 2: `00`
* Speed setting 3: `00`
* Speed setting 4: `FF`
* Speed setting 5: `FE`

So, in fact, this value seems to be a signed offset from the default delay. We can go below 0 up until a point where the animation will be freezed.
That value seems to be 0xFB (-5), implying the default delay would be 6. I don't really know the units of these ticks, btu if the value is set to -6 or below, animation will stop running.

As for the delay stuff:
In fact, there seems to be two different refresh mechanisms dedicated to keeping the ram sticks "mostly in sync", one of which is blocked by the global mutex, the other not.
It seems the software tries to measure the SMBus delay in some way, including the locked time. So, if the mutex is not available, it will mess up the refresh delay somehow.
But another refresh seems to be happening outside of the Mutex, and the sticks seem to be refreshed in the middle of the animation ?
So there is possibly a control for that.

### 8023 - Reverse ?

The color wave effect from RGB Fusion 2.0 is visually reversed, (from bottom to top instead of top to bottom), and this value is updated to `01` when it is applied.
Setting this to `01` should thus reverse the effect direction when applicable.

### 8025 - ???

This seems to always hold the value 5 in the controllers I own.

### 80F8 - Slot index to move

This should be written with the index of the memory slot whose (SMBus) address should be changed.
It can also be read, like everything, but reading this is utterly pointless, as it can be updated to any value at any time.

### 80F9 - SMBus device address

This should be written with the shifted address to use for the device at the slot index specified at `80F8`.
Devices whose slot index does not match the value in `80F8` will ignore this write.
Reading from this is also pointless. I didn't check if it updates when written with a value that should be ignored, but the current address of the device is always known, so this is useless.

### 8000‥800E - Dynamic colors (New)

This is for newer controllers that can have more than 5 LEDs.
This contains 10 colors in RBG order (*not* RGB).
These colors are used when the controller is set to dynamic colors.
Updates to these colors are applied immediately, and as such, updates will occur faster, as they will require a single block transfer SMBus operation rather than multiple separate operations.

### 8000‥800E - Static colors (New)

This is for newer controllers that can have more than 5 LEDs.
This contains 10 colors in RBG order (*not* RGB).
These colors are used for predefined effects.
Updates to these colors are applied only when changes are applied by writing at `80A0`.

### 8586 - Slot Index ?

This value is the RAM slot index or a value that is correlated to it.
All the data in this area slightly change between the different sticks, but this one has linearly increasing values matching the slot index.
It can be used to determine which RAM slot is mapped at a specific SMBus address.
