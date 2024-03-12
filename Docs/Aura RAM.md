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

