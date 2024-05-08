# NZXT Kraken Z63

This device is a composite device that has both a "vendor-specific" endpoint accessed by CAM through WinUsb for some reason, and a more regular "HID" endpoint, also relying on a fully custom protocol.
The reasons behind this are unclear yet, and naive observations only show data exchanged over the HID endpoint.

## Quick analysis using Wireshark

HID protocol is always 64 bytes long, including the report descriptor.
However, as is relatively unusual, commands are split in their own reports, meaning that the report ID byte is not "wasted".

It seems that commands go by pair, with input reports having the bit 0 set, and output/feature reports having the bit 0 unset.
This can be observed by the 74 / 75 (request/response) pair used by NZXT CAM. However, it does not seem to always be the case.

A few messages are regularly observed:

### Status

````
REQ: 74 01 00 …
RES: 75 01 1a 00 41 00 0a 51383430353132 01 1f 09 ff 06 23 23 01 00 af 03 19 19 00 00 …
````

This seems to contain information about the device, periodically requested by CAM.
It contains the string "Q840512" that we also observe in `FF` messages.
Other values are changing, and among them we can see `ff 06` and `af 03`, which I'm pretty certain are the pump speed and the fan speed.
The numbers here, and across various occurrences, are similar in value to the ones displayed in NZXT CAM. (To be interpreted in little endian, i.e. `06ff` and `03af`)

The values `23 23` and `19 19` also vary in messages, always in pairs. => See below, duplication unexplained, but requested pump and fan speed %.

The value `1f` above would likely be the liquid temperature in °C, as it varies between 31 and 32°, which matches the observed messages.

Te value `09` is yet unknown.

### Set pump/fan speed

We see the values `23` and `19` in the following requests:

````
72 02 01 01 19191919191919191919191919191919191919191919191919191919191919191919191919191919 0000000000000000000000000000000000000000
72 01 01 00 23232323232323232323232323232323232323232323232323232323232323232323232323232323 0000000000000000000000000000000000000000
````

The responses `FF` seem to be generated as a result of the `72` requests:

````
ff 01 1a 00 41 00 0a 51383430353132 72 02 000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
ff 01 1a 00 41 00 0a 51383430353132 72 01 000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
````

The response also contains the "Q840512" string (which might be some kind of model ID?)
Interestingly, we can see the request id `72` and what would be the subcommand id (`01` or `02` here) appearing here, probably acknowledging the request.

It is possible that the values sent here are the requested power of pump and fan in %.

… and indeed, these are the commands that are sent when the "fixed" mode is enabled (Fixed mode is Pump at 100% and fan at 40%)

````
72 01 01 00 646464646464646464646464646464646464646464646464646464646464646464646464646464640000000000000000000000000000000000000000
72 02 01 01 282828282828282828282828282828282828282828282828282828282828282828282828282828280000000000000000000000000000000000000000
````

As such, we can deduce that `01` is pump and `02` is fan. Following value unknown, and third value is `00` for pump and `01` for fan. (Meaning unknown yet)

### ???

Also observed are commands `38`/`39`, which are likely related to the cooling mode change (from default to fixed and vice-versa) done in the UI.

````
REQ: 38 01 04 0d 000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
RES: 39 01 1a 00 41000a 51383430353132 01 00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
````

The response also include the "Q840512" string
