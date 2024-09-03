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
