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

### Read firmware version

The legacy driver does a GET_REPORT on report ID `05`.

It returns two bytes indicating the major and minor version.

e.g. Mine is 2.45:

`02 2D`

### Write settings

Synapse 2 sends the output report `10`, which contain 4 bytes.

Example:

02 01 01 02
02 01 01 03
02 01 01 01
02 01 01 00
01 01 01 03
03 01 01 03

RR DD PP LL

RR is the polling rate, where `01` is 1000Hz, `02` is 500Hz and `03` is 125Hz.
DD is a value indicating the DPI. `01` is either 1800 or 3500 (depending on the mouse model). `02` is 900. `03` is 450.
PP is a value representing a profile index (in the legacy driver). I have no idea what actual purpose it serves, as the whole settings are sent everytime. Values go from `01` to `05`.
LL is a bitfield indicating the active lighting zones. `01` is the logo, and `02` is the wheel.

It seems that the report used is not published in the HID descriptor, though.
So the proprietary driver might be strictly required to work with this mouse 🙁

## Communicating with the device

### Legacy driver

After fighting with the current driver, and installing Synapse 2 on my dev computer, it became apparent that the driver provided with Synapse 2 was completely broken on some setups.
I don't know if that could have been caused by something I did, but it seems unlikely as Synapse forcefully uninstalls and reinstalls all drivers.

Luckily enough, there was a legacy driver for the DeathAdder (3.5G), predating Synapse 2, and I had completely forgotten about it although I clearly remember seeing it.
But some forums mentioned its existence, so I finally gave up on Synapse 2 and started looking for that old driver…

It can be downloaded from cnet: https://download.cnet.com/razer-deathadder-3-5g-windows-driver/3000-18491_4-77542908.html

This installed fine despite the old age (yes, x64 was already available), and after a restart… tada, it actually worked.

The interface provided with the driver works fine and does exactly what it needs to do, but our goal here is to replace it. (NB: It is possible to find the raw drivers in the installation folder, which I recommend backing up)

Thankfully, this time, the driver is very lean compared to the mess of the newer drivers. It also helps that it is a raw WDF driver instead of a KMDF driver. (Also it might actually predate KMDF)

What appears obvious from the get-go is that this driver can actually read the firmware version, something that I never observed with Synapse.
This is done via a get report URB.

### Synapse 2 Driver

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

So, apparently, the Razer_ device won't show up if all drivers aren't installed, in which case, IOCTLs to RzUdd will fail.

However, once the drivers are installed, we can send to RzUdd the IOCTL 88883000, which will be used to enumerate the devices. This, it turns out, will allow to find out the name of the `Razer_` device that corresponds to our device.

The input IOCTL packet is composed as such:

````csharp
ulong Unknown0; // IIRC this was supposed some kind of pointer within the driver?
uint Two; // Set to `2`
uint One; // Set to `1`
uint Index; // Zero-based index of the RzUdd device to retrieve
uint Unknown1; // Don't know if it does something here.
````

The output will be composed as such:
````csharp
ulong Unknown0; // Should be zero
uint Handle; // This is the value that will be used in the `Razer_` device name.
uint Unknown1; // 0x0000677e ? This value is supposed to be called `IsBluetooth` from what I found in the dlls, but I can't make sense of the value
ushort ProductId; // Zero-based index of the RzUdd device to retrieve
ushort Unknown2; // Zero
ushort Unknown3; // 0x0001 ?
ushort Unknown4; // 0x0001 ?
uint Unknown5; // 0x00000033 ?
uint Unknown6; // Zero
````

So, basically, we can enumerate on index until we get one product ID matching our device. It none is found, the IOCT will fail with an exception indicating a malfunctioning device.

I tested all I could, and ended up validating my theory about IOCTL `88883020`, which is that is is simply a URB setup data + data fragment, however, I couldn't get it to work.
Apparently, something needs to be initialized within the driver before some of the IOCTLs work. And I couldn't figure out what yet.

Another important IOCTL seem to be 88883004. It is composed of several functions which seem to implement most of the device features such as key remapping on the kernel side.

This IOCTL is likely the key to unlocking IOCTL `88883020`, as it is one of the only three called by Razer Synapse when starting up.
However, which functions are called is a mystery (the last release of IRPMon doesn't allow to see the contents of IOCTLs. It would have been soooooo simple 😢)

Functions:

|    3 |  ID  | Input Length | Output Length | Description | Parameter 1 | Parameter 2 |
| ---- | ---: | -----------: | ------------: | ----------- | ----------- | ----------- |
| `03` | `01` |       `8018` |          `10` | CMapping::InternalSetMappingForPauseBreakKey, CMapping::SetMappingsToHW |             |             |
| `03` | `02` |         `18` |          `10` | CMapping::ClearAllMappings |             |             |
| `03` | `03` |         `18` |          `10` | ? |           ? |             |
| `03` | `06` |         `28` |         `1b0` | ? |           ? |          ?+ |
| `03` | `06` |         `28` |          `10` | ? |           ? |          ?+ |
| `03` | `07` |         `18` |          `10` | CMapping ? |             |             |
| `03` | `08` |         `18` |          `10` | CMapping ? |             |             |
| `03` | `0B` |         `18` |          `10` | CSensitivityScaler::Enable |             |             |
| `03` | `0C` |         `18` |          `10` | CSensitivityScaler::Disable |             |             |
| `03` | `0D` |         `18` |          `10` | CSensitivityScaler::SetActiveLevel |           X |           Y |
| `03` | `0E` |         `18` |          `10` | CSensitivityScaler::? (GetActiveLevel?) |             |             |
| `03` | `0F` |         `18` |          `10` | CMapping ? |             |             |
| `03` | `11` |         `18` |          `10` | ? |             |             |
| `03` | `12` |         `18` |          `10` | ? |             |             |
| `03` | `13` |         `18` |          `10` | ? If flag = 1 / Enable ? |             |             |
| `03` | `14` |         `18` |          `10` | ? If flag = 0 / Disable ? |             |             |
| `03` | `15` |         `18` |          `10` | ? |             |             |
| `03` | `16` |         `18` |          `10` | ? |             |             |
| `03` | `17` |         `18` |          `10` | ? |             |             |
| `03` | `18` |         `28` |          `10` | ? |           ? |          ?+ |
| `03` | `1C` |         `18` |          `10` | ? |             |             |
| `03` | `1D` |         `18` |          `10` | ? |             |             |
| `03` | `1E` |         `18` |          `10` | ? |             |             |
| `03` | `1F` |         `18` |          `10` | ? |             |             |
| `03` | `20` |         `18` |          `10` | ? If flag = 1 / Enable ? |             |             |
| `03` | `21` |         `18` |          `10` | ? If flag = 0 / Disable ? |             |             |
| `03` | `22` |         `18` |        `8010` | CMapping::GetMappings |             |             |
| `03` | `23` |         `18` |          `10` | ? |             |             |
| `03` | `25` |         `18` |          `10` | ? |             |             |
| `03` | `26` |         `18` |          `10` | ? |     (OPT) ? |     (OPT) ? |
| `03` | `27` |         `28` |          `10` | CMacroExecute::StartMacro |           ? | (18 bytes)? |
| `03` | `28` |         `20` |          `10` | CMacroExecute::StopMacro |           ? | (10 bytes)? |
| `03` | `2A` |         `18` |          `10` | CSensorConfig::StartCalibration |        1000 |             |
| `03` | `2B` |         `18` |          `10` | CSensorConfig::StartCalibration |           ? |             |
| `03` | `2C` |   (Var) `18` |    (Var) `10` | CUsageInfo::GetUsage |           ? |          ?+ |
| `03` | `2D` |         `18` |          `10` | CUsageInfo::ResetUsage |             |             |
| `03` | `2E` |         `18` |        `7C08` | CSensorConfig::GetResult |             |             |
| `03` | `2F` |         `18` |          `10` | ? |    (byte) ? |             |
| `03` | `30` |         `18` |          `10` | ? If flag = 1 / Enable ? |             |             |
| `03` | `31` |         `18` |          `10` | ? If flag = 0 / Disable ? |             |             |
| `03` | `32` |         `18` |          `10` | ? |             |             |
| `03` | `33` |         `18` |          `10` | CWireless::IsConnected |             |             |
| `03` | `34` |   (Var) `18` |    (Var) `10` | CUsageMapInfo::GetUsage |           ? |          ?+ |
| `03` | `35` |         `18` |          `10` | CUsageMapInfo::ResetUsage, CUsageMapInfoEx::ResetUsage |             |             |
| `03` | `36` |         `18` |          `10` | (Combo of `36` then `38`) ? |             |             |
| `03` | `37` |         `18` |          `10` | (Combo of `37` then `39`) ? |             |             |
| `03` | `38` |         `18` |          `10` | (Combo of `36` then `38`) ? |             |             |
| `03` | `39` |         `18` |          `10` | (Combo of `37` then `39`) ? |             |             |
| `03` | `3A` |         `18` |          `10` | ? |             |             |
| `03` | `3B` |         `18` |          `10` | ? |             |             |
| `03` | `3C` |         `18` |          `10` | ? |             |             |
| `03` | `3D` |         `18` |          `10` | ? |             |             |
| `03` | `3E` |         `18` |          `10` | ? |             |             |
| `03` | `3F` |         `18` |          `10` | ? |             |             |
| `03` | `40` |   (Var) `18` |    (Var) `10` | CUsageMapInfoEx::GetUsage |           ? |          ?+ |
| `03` | `41` |       `1018` |        `1010` | GetFromDriverStore, CKeyBoardLayout::GetEditionInfo |             |             |
| `03` | `42` |       `1018` |          `10` | CEventManager::SetToDriverStore, CStoreData::SetStoreData |             |             |
| `03` | `43` |         `18` |          `10` | Sets or unsets a timer ? |           ? |             |
| `03` | `44` |         `10` |          `10` | CRzFrameEngine::IsEnable |             |             |
| `03` | `45` |         `10` |          `10` | CRzFrameEngine::GetRefreshRate |             |             |
| `03` | `46` |         `18` |          `10` | ? |             |             |
| `03` | `48` |        `858` |          `10` | (Two message sizes possible) ? |             |             |
| `03` | `48` |        `b18` |          `10` | (Two message sizes possible) ? |             |             |
| `03` | `4A` |        `858` |          `10` | (Two message sizes possible) ? |             |             |
| `03` | `4A` |        `b18` |          `10` | (Maybe send a manual mouse input) (Two message sizes possible) ? |             |             |
| `03` | `4B` |         `18` |          `18` | CRzEffectMgr::EnumRzEffect |             |             |
| `03` | `4C` |         `20` |          `10` | CRzEffectMgr::NewRzEffect |             |             |
| `03` | `4D` |         `20` |          `10` | CRzEffectMgr::DeleteRzEffect |           ? |          ?+ |
| `03` | `4E` |         `20` |          `30` | CRzEffect::GetInfo |           ? |          ?+ |
| `03` | `4F` |         `38` |          `10` | ? |           ? |          ?+ |
| `03` | `51` |       `7C28` |          `10` | ? |           ? |          ?+ |
| `03` | `53` |        `2E8` |          `10` | (Calls of `41`, `53`, `61` are related) |           ? |          ?+ |
| `03` | `54` |         `20` |          `10` | ? |           ? |          ?+ |
| `03` | `55` |         `20` |          `10` | ? |           ? |          ?+ |
| `03` | `56` |         `20` |          `10` | ? |           ? |          ?+ |
| `03` | `58` |         `A8` |          `10` | ? |           ? |          ?+ |
| `03` | `59` |         `18` |          `10` | ? If flag = 1 / Enable ? |           ? |             |
| `03` | `5A` |         `18` |          `10` | ? If flag = 0 / Disable ? |           ? |             |
| `03` | `5B` |         `18` |          `10` | ? |           ? |             |
| `03` | `5C` |       `8018` |          `10` | ? |           ? |          ?+ |
| `03` | `5D` |         `18` |          `10` | ? |           ? |             |
| `03` | `5F` |       `7C28` |          `10` | ? |           ? |          ?+ |
| `03` | `61` |        `2E8` |          `10` | (Calls of `41`, `53`, `61` are related) ? |           ? |          ?+ |
| `03` | `66` |         `18` |        `7C08` | CSensorConfig::GetQResult |             |             |
| `07` | `03` |        `190` |          `10` | ? |           1 |          ?+ |
| `07` | `05` |         `28` |         `1B0` | ? |           1 |          ?+ |
| `07` | `05` |         `28` |         `1B0` | CMultipleKeyManager::EnumMultipleKey |           2 |           ? |
| `07` | `06` |         `28` |         `1A8` | ? |           1 |             |
| `03` | `07` |         `18` |          `10` | ? |             |             |

Another IOCTL seem to be used to receive notifications from the driver: `8888C008`
I'm not entirely sure of how to use it, but from my understanding, the idea is to start an async IOCTL on it, and the IOCTL will complete when an event is ready.

From what I understand, the client side will send out 10 concurrent IOCTLs to the driver, and re-emit one when there is a completion.
Each of those IOCTL calls uses a 64 bytes buffer, presumably the same for input and output. I'm unsure of what would be needed for initialization, but maybe an empty buffer is enough.

Could also be the key to unlock the features of the driver, although I didn't find any confirmation of that and didn't try to use it yet.

Razer Synapse initialization sequence:

IOCTL `88883000` IN `0018` OUT `0020` 2x

IOCTL `88883008` IN `0040` OUT `0040` 10x

IOCTL `88883000` IN `0018` OUT `0020` 2x

IOCTL `88883004` IN `0018` OUT `0010`

IOCTL `88883000` IN `0018` OUT `0020` 4x

IOCTL `88883004` IN `1018` OUT `0010` (03:42 only one possible?)

IOCTL `88883000` IN `0018` OUT `0020` 4x

IOCTL `88883004` IN `0018` OUT `0010`

IOCTL `88883000` IN `0018` OUT `0020` 46x

IOCTL `88883004` IN `0018` OUT `0010`
IOCTL `88883004` IN `0018` OUT `0010`
IOCTL `88883004` IN `8018` OUT `0010` 7x (03:01 or 03:5C)

IOCTL `88883020` IN `000C` OUT `000C` 3x

DriverStore values

`00`(`02`) EditionInfo
`02`(`16`) Serial Number ?
`18`(`04`) P3 Firmware Version ?
`39`(`04`) ??? Set to zero during initialization, 0 or 1 after HidD_SetFeature (for devices that support this call)
`42`(`01`) Active DPI Stage
`3D`(`05`) ???
`43`(`15`) DPI Stages
`58`(`04`) ???
