This device and the compatible dock can work without any driver, but if we want to configure it and play with the RGB features, we need to analyze the protocol.
I sacrificed my computer for science and installed the terrible software that Synapse 3 is, and the experience was less than pleasing.

One interesting thing is that as soon as the installer started, some requests were emitted on the two devices, likely to get information, using feature reports.

SET => 000800000002008600000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000008400            
GET => 020800000002008600000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000008400                    

SET => 000800000002008600000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000008400            
GET => 020800000002008600000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000008400                    

After that, when starting the actual installation, the bloated software sent a huge lot of requests, while taking several minutes to do what should have been nothing…
And messed up with ALL USB devices on the system. ALL of them. Competitor's mouse, Stream Deck, etc. TWO TIMES. My other mouse stopped working for tens of seconds while this was happening.
It seems it was doing some nasty stuff to the windows USB stack. At least something like restarting it, but maybe it installed some kind of filter driver on the system.
Indeed I see two drivers files, "RzDev_007c.sys" and "RzDev_007e.sys" associated with the mouse (USB dongle) and Dock. I'm unsure they were here prior to this.
Worthy of note, is that this crappy software asked me to restart the system after the installation, as was also notified by the windows driver install popups.
It did still seem to somehow work without doing that.
Let's hope these drivers are not a vital part of anything.

Upon starting razer synapse, it started emitting a lot of requests to the device. This was presumably to manually orchestrate the color cycling effect, which is also an hardware effect -_-
From testing around with Wireshark still opened, it would seem as if all effects are implemented in software using regular frequency manual color updates. Even fading out to black, etc.

# Analyzing the protocol

As often, Wireshark is operating a bit weird here. It seems unable to interpret the data part of GET feature responses while present in the packet.

When starting Synapse 3:

SET => 001f00000002008400000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000008600
GET => 021f00000002008400000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000008600

SET => 001f00000002000403000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000500
GET => 021f00000002000403000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000500

First of all the n-1 byte of a packet (89th byte out of 90) seems to be a checksum of sorts. (We see that value constantly changing, and it is well isolated)
Judging by the requests/responses above, this is definitely a XOR checksum of all the bytes except for the fist two and last two.

BTW we can see the installer using various report IDs to communicate with the devices, but they are probably not the ones we are interested in for now. (We cans till dig in the captures later)
The report shown above (report ID 0 of 90 bytes) is the one we are interested in and is likely the standard razer protocol for these devices.

Hopefully, the razer protocol is relatively universal across devices. (Which I seem was the rational for devices supported in Synapse 3 vs those only supported by Synapse 2)
If that's the case, what we find here can be reused more widely 🤞

This is a request that seemingly sets the RGB color on the dock:

SET => 001f000000080f030000000000ff5f04000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000a000

Since these are (always?) sent using feature reports, they don't expect any response other than an ack from the device.

We can see what seems to be the color above: ff5f04. This was chosen semi randomly from a long sequence of similar requests.
The color values that are sent to the device seem somewhat strange, as they are not the aggressive FF0000 => FFC000 => FF8000 etc. sequences that we would usually see.

It seems that all requests start with 001f. (And all responses start with 021f ?)

Let's ignore the checksum byte and trim the requests to compare various requests sent to the device


001f 000000 02 00 84  # ?
001f 000000 02 00 04 03 # ?
001f 000000 02 00 86  # ?
001f 000000 08 0f 03  # ?
001f 000000 06 0f 02 00 00 08 00 01 # ?
001f 000000 08 0f 03 0000000000 ff5f04 # Set color ?
001f 000000 08 0f 03 0000000000 ff5e04 # Set color ?
001f 000000 08 0f 03 0000000000 ff6004 # Set color ?
001f 000000 08 0f 03 0000000000 00ff00 # Set color ? (To green, done in the UI)
001f 000000 03 0f 04 01 00 54 # ?
001f 000000 03 0f 04 01 00 ed # ?

The excerpts below are the parts where I tried to adjust the effect brightness:

001f000000030f04010049
001f000000030f04010082
001f000000030f040100ad
001f000000030f040100ba
001f000000030f04010038
001f000000080f03000000000000ff00 // Green (Static Color defined in the UI)
001f000000030f0401003d
001f000000030f04010047
001f000000030f04010051
001f000000030f04010051
001f000000030f04010054
001f000000030f040100ff

001f000000030f040100ed
001f000000030f040100c9
001f000000080f03000000000000ff00 // Green (Static Color defined in the UI)
001f000000030f040100af
001f000000030f04010091
001f000000030f04010084
001f000000030f04010059
001f000000030f0401003d
001f000000030f04010035
001f000000030f0401003d
001f000000030f04010044
001f000000080f03000000000000ff00 // Green (Static Color defined in the UI)
001f000000030f0401004c
001f000000030f0401004f
001f000000030f0401004f
001f000000030f04010054 // Brightness to 33 ?

Default brightness was "33" in the UI, so we would be looking for 0x21 or 0x55 if the values are based on 255 (which they seemed to be)…
Turns out the last command ends with the value 0x54 (after a manual reset to the value "33", which is a too close match to be a coincidence)
