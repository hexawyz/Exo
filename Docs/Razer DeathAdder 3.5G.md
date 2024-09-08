# Razer DeathAdder 3.5G

This is now a very old mouse, but why not look up at how it works…

By firing up Synapse 2, we can observe the very primitive protocol used by this mouse.

It would seem that most configuration is purely software, as changing DPI, button mappings, etc, does nothing on the USB side.

However, the limited hardware settings that can be changed seem to be chanegd via a very simple protocol.

## Information

VID: 1532

PID: 006

The mouse has two lighting zones: Mouse Wheel and Logo.

Those two lighting zones can only be on or off.

Another setting that can be configured is the polling rate, with three settings: 125Hz, 500Hz and 1000Hz (similar to some of the much newer models)

Judging by the protocol, there are two other settings that can be adjusted, but I couldn't find those on synapse

## Protocol

Synapse 2 sends the output report `10`, which contain 4 bytes.

Example:

02 01 01 02
02 01 01 03
02 01 01 01
02 01 01 00
01 01 01 03
03 01 01 03

RR ?? ?? LL

RR is the polling rate, where `01` is 1000Hz, `02` is 500Hz and `03` is 125Hz.
LL is a bitfield indicating the active lighting zones. `01` is the logo, and `02` is the wheel.

It seems that the report used is not published in the HID descriptor, though.
So the proprietary driver might be strictly required to work with this mouse 🙁

## Communicating with the device

Razer, as always, installs a lot of system drivers for their devices. This made things a bit more complicated to find out, but one driver was seemingly "pre-installed" on my computer, presumably from an connection of the mouse a few years ago.
Others were not installed, so I went and installed them

So, looking at how everything is wired up, it important one is `rzdaendpt.sys` which is the one that was already installed here. It might be provided through Windows Update?

As the name indicates, it serves as the endpoint for DeathAdder. The other drivers are probably not needed.

Took me a while to find out, but the `rzdaendpt` driver is actually setup on the USB device node and not on the HID device node, while the `rzudd.sys` driver would be installed on HID.

From this, I am assuming that `rzudd` (which from the ini, is a generic driver for all Razer devices), is actually used as a filter to intercept mouse HID reports and reprocess them in Synapse.
That's a somewhat clever way of doing things, but which is thankfully not necessary anymore with the newer mouses with onboard profiles.

There are references to multiple NT device objects in the code, but using WinObj to explore the current objects on the system was the quickest way to understand what could be done.

Apparently, `rzdaendpt` will setup a device named `RzEpt_Pid0016&00000006` on my machine. Here, `0016` is "obviously" the PID of the mouse, and `00000006` can be relatively identified to be the `Address` of the device.

With this in mind, connecting to the driver should be as simple as crafting that name from the information we retrieve about the device.

After investigation, it would seem that the driver expects to receive an USB URB to be sent raw to the USB device.

Which device object to open and which IOCTL code to use is still uncertain, as the driver creates many objects.
From my current knowledge, the IOCTL but it should be either 0x88883020 with raw URB data or 0x00220003 (`IOCTL_INTERNAL_USB_SUBMIT_URB`).

So, by looking a bit more using IRPMon, it seems that Razer Synapse 2 will in fact communicate with the RzUdd device…

From what I can see the rzudd driver will interact with the rzdaendpt driver and send internal IOCTLs, so both drivers are actually required to be present.
Basically, the rzudd driver will try to connect to the `RzEpt` device to forward some `IOCTL_INTERNAL_USB_SUBMIT_URB` requests to it.

Another weird thing is that a `Razer_NNNNNNNNNNNNNNNN` device will be created in some circumstances by the RzUdd driver, but not always.
Maybe it requires some initialization packet from the user mode software?

From what I know those devices should be pretty similar, so it is unclear to me why we have two separate instances.

The main one still seems to be the `RzUdd` device.

Razer Synapse will mostly send `88883000` IOCTLs with 24 bytes of input and 32 bytes of output buffer to the RzUdd device.

Other IOCTLs are sent to the Razer device.

