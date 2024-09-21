# Razer Mamba Chroma

The device is part of the ones incompatible with Synapse 3, however, from a quick glance at Wireshark, it uses the same protocol as the enwer DeathAdder V2 Pro.
Writing the code for that device should be just an extension of the same code.

# Device features

* 7 Buttons
* ARGB lighting (like all Chroma devices?)
* 16000 DPI (configurable)
* Wired or wireless through a dedicated dock (also acting as a dongle)
* Battery (obviously)
* Onboard memory for some settings such as lighting effects (To be investigated)

# Dock

Detecting device presence is somewhat hard.

As soon as the device becomes online the first time, the dock will cache most device informations.

Sending the lighting effect `0A` command to the dock when the mouse is not connected will update the lighting effect on both the mouse and the dock as soon as the mouse becomes online.
It seems however that the dock has little intelligence on its own. (Maybe it is possible to update the dock with the other commands though, I just havent't tested that yet.)

# Protocol

The mouse seem to e based on a older version of the protocol (Likely also why it is not compatible with Synapse 3. That and because they would have needed to write another set of kernel drivers I guess)
Many functions work identically to the newer protocol, but that does not include lighting.

Synapse seems to use IDs `1f` and `3f` to communicate with the device, whereas DeathAdder V2 Pro would use `08` and `1f`.
Despite this, my hastily adapted POC with the same code and IDs as for DeathAdder V2 Pro seemed to work fine out of the box.

Exact meaning of those Razer IDs remains a mystery…

## Basic lighting stuff

The lighting on this feature uses category code `03` instead of `0f` on the newer devices (`0f` seems like it might do something else on this mouse though)

### Brightness

00 3f 000000 03 03 03 01 05 ff 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000f800

This one command sets the brightness. While synapse 2.0 does not do it, testing shows that the brightness can actually be read back (unlike newer devices ?)

The meaning of the two parameters `01` and `05` is unknown.
I suspected `01` could indicate persistence, but there doesn't seem to be an equivalent for lighting (always persisted?), so this could as well be the wired vs wireless flag.

### Built-in effects

Exact details unclear, but there seem to be at least two commands to set lighting effects. (Probably to address the multiple lighting zones)
In my tests, I used `0a` (as it was the one that popped of the most when playing with Synapse) and it updated the whole mouse with the effect I picked.

Also, parameters are specific to each effect, and commands need to be sent exactly (no invalid data length with zero padding) for them to work.
Effect IDs are also not the same as for newer devices.

#### Command `0A`

`01` Wave:

00 3f 000000 02 03 0a 01 02 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000800

Wave has one extra parameter which is the direction. Can be `01` or `02` (represented by upward and downward arrows in Synapse)

`02` Reactive:

00 3f 000000 05 03 0a 02 03 00ffff 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000d00
00 3f 000000 05 03 0a 02 03 00ff00 000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000f200
00 3f 000000 05 03 0a 02 01 00ffff 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000f00
00 3f 000000 05 03 0a 02 04 00ffff 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000a00

This effect has a speed parameter. `01` is slow, `03` is mid, and `04` is fast. (From the three settings exposed in Synapse)

`03` Breathing:

00 3f 000000 08 03 0a 03 02 00ffff ff00ff 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
00 3f 000000 08 03 0a 03 03 000000 000000 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000100

Breathing has a color count parameter. (I didn't pay attention at the time, but `03` might be the random colors setting, instead of zero)

`04` Color cycle:

00 3f 000000 01 03 0a 04 000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c00

This effect has no parameter at all.

`05` Dynamic ???:

00 3f 000000 02 03 0a 05 00 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000e00

Not entirely certain but this one appeared when playing with the Chroma configurator. (Interleaved with `0C` commands)

`06` Static:

00 3f 000000 04 03 0a 06 00ffff 000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000b00

Static has only three RGB bytes as a parameter.

#### Command `01`

00 3f 000000 05 03 01 00 03 00ffff 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000400

### Unknown

00 3f 000000 03 03 00 01 05 01 00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000500

00 3f 000000 01 03 10 00 000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001200
00 3f 000000 01 03 10 01 000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001300

### Dynamic colors

Dynamic colors are set by the `0C` command. Details not investigated, but the payload is 47 bytes, which can only hold 15 colors. (7 + 7 + 1 ? I didn't count the number of leds but that could be it)

00 3f 000000 2f 03 0c 00 0e 00ff00 00ff00 00ff00 00ff00 00ff00 00ff00 00ff00 00ff00 00ff00 00ff00 00ff00 00ff00 00ff00 00ff00 00ff00 000000000000000000000000000000000000000000000000000000000000000000d100


## DPI

### DPI Presets

This one command seems to obviously sets the mouse DPI presets:

00 3f 000000 26 04 03 01 00 05 | 00 0320 0320 0000 | 00 0708 0708 0000 | 00 1194 1194 0000 | 00 2328 2328 0000 | 00 3e80 3e80 00000 | 000000000000000000000000000000000000000000000000000000000000000000000000000000000002500
00 3f 000000 26 04 03 01 00 04 | 00 0320 0320 0000 | 00 0708 0708 0000 | 00 1194 1194 0000 | 00 2328 2328 0000 | 00 0000 0000 00000 | 000000000000000000000000000000000000000000000000000000000000000000000000000000000002400

It seems to be different than the one from newer devices, but is assigns multiple presets at once too. (Trying to use the newer command will fail)

The `01` is likely the persistence flag as usual, and `05`/`04` is for sure the number of presets.

Presets seem to occupy 7 bytes as in the newer version, however only 4 out of those 7 bytes are useful.

There is at least one (`00`) unknown parameter, depending on where we put the profile boundary in the above captures.
For now, I'm assuming the `00` byte preceding the DPI X value is part of the profile. (As in that case it would be more similar to the newer command)
This won't change much as we will ignore this value.

… Actually the `00` is in the exact same spot as the active preset ID would be in the newer command, so we can likely reuse the exact same code for both ? 🤷

Testing will indicate how well this goes 😁

NB: Reading does not seem possible.

### Active DPI Preset

This one command sets the active DPI preset: (Or maybe at least the one assigned on startup)

00 3f 000000 02 04 04 01 04 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000700
00 3f 000000 02 04 04 01 05 0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000600

So, unlike the newer protocol, switching the active DPI preset is done separately from updating the profile… Which kinda made more sense.

I'm assuming they merged the two in newer versions in order to optimize wear and tear, as the two settings are likely to be stored close in device memory.
Having the two updated in a single operation would be twice as efficient in terms of wear.

### Current DPI

The command to read/write the current DPI level is the same as for newer models. (`05`)
