[![Build Status](https://github.com/hexawyz/Exo/actions/workflows/build.yml/badge.svg)](https://github.com/hexawyz/Exo/actions/workflows/build.yml)

# [Exo](https://exo-app.io)

Exo is the exoskeleton for your Windows computer and its connected devices.

You can use it to control or monitor your Mouses, Keyboards, Monitors, RGB lighting, etc.

The project was started out of spite for all the client-side "drivers" for devices, that consume unimaginable amounts of RAM and CPU.
As such, the goal of Exo is to replace all of these tools in a single service that will be more stable, consume a relatively small memory footprint, with a low CPU overhead.

To support this vision, Exo is designed to be as modular as possible, and device support is provided through dynamically-loaded plugins that you can add or remove as you need.

⚠️ The project is still in the development phase, so some features are not implemented yet. It does nonetheless provide support for quite a few devices already, exposing some or all of their features in the UI.

ℹ️ Some devices are based on the same protocol as those already supported. It is possible that the current code already support a specific device, but does not "recognize" it.
In order to avoid communicating with a device that is incompatible, the code of the various device plugins will generally have an explicit list of each supported device.

💡 If you want to request support for a device, or if you believe one of your devices should already be supported by the code, please make this known by opening an issue.

For screenshots of the UI, go to https://exo-app.io

# Why

A lot of devices sold today, from mouses to RGB controllers and monitors, often provide features that can't be accessed directly at the OS level.
This is fine, but those device require drivers and/or applications to provide their useful features to you.

When these apps exist, they are more often than not presented as client-side application that will be a terrible Electron-based mess, and almost always as a bloated unoptimized suite with features you will never need.

Other than being slow and consuming a huge chunk of your RAM for nothing, those applications are more often than not somewhat unstable, and can have undesired behavior such as random updates or invisible crashes. (Do you really need 5 unstable chrome processes to manage a mouse?)

As the author of Exo, I believe (and by now, have mostly proven 😄) that it is possible to do a much better job than this on all aspects.
Exo is designed and architected with this in mind, and aims to provide a Windows service that will detect and operate on your device by being mindful of system resources. (Expect between 20 and 40 MB depending on your configuration, and mostly no CPU usage)

# Supported features

Exo currently support the following features, provided that there is a custom driver to expose the features of your device in the service:

* Overlay notifications: Some of the other features will push notifications that will be displayed on screen.
* RGB Lighting: Setting hardware effects is supported, dynamic ARGB lighting not yet ready.
* Device battery status: The service will be aware of, and display the battery charge of your device, as well as show notifications for low battery, etc. Also supports idle timer and low power mode for wireless devices that support it. (e.g. Razer)
* Monitor control: Brightness, Contrast, Audio Volume, Input Select and various settings, if supported by the monitor. (A configuration system for overriding monitor details is available)
* Keyboard backlighting: The service will observe keyboard backlight changes and push overlay notifications.
* Mouse: DPI change notifications, Configuration of DPI presets and polling frequency, and manual DPI changes from the UI.
* GPU: Provide support for accessing connected monitors in non-interactive (service) mode. (May rely on fallback using the Windows API if necessary)
* Sensors: For devices that provide data readings, expose sensors that can be read periodically and displayed in the UI.
* Coolers: For cooling devices, or devices equipped with a fan, expose controllable coolers and allow setting custom software cooling curves based on sensors.

All text in the application can be localized, and in addition to the default English localization, the following languages are supported out of the box: French.

# Supported devices

NB: Support of a device does not mean that all of its features will be exposed in the application. It will be recognized and connected to the application, but the bricks may not yet be in place to expose the features.

* Logitech
	* All HID++ devices (with some possible exceptions), including through USB receivers: Battery level, keyboard reporting features, support for displaying dpi presets of mouse profiles, dpi change notifications.
	* Tested on:
		* Unifying Receiver 
		* Bolt Receiver
		* Lightspeed Receiver
		* G Pro X Superlight
		* MX Keys Mac
		* MX Keys Mac Mini
	* ⚠️ There is limited to no support for mouses in host mode. Reading onboard profiles is supported, but only dpi settings are used at the moment.
* Razer
	* DeathAdder V2 Pro (USB, Dongle, Bluetooth LE): Battery Level, Charge status, RGB lighting, DPI changes, DPI Presets, Low Power mode, Idle Sleep Timer
	* DeathAdder V3 Pro (USB, Dongle): Battery Level, Charge status, DPI changes, DPI Presets, Low Power mode, Idle Sleep Timer (NB: This device has not been tested, but shares most features of the V2, so it is expected to work fine)
	* Mouse Dock Chroma: RGB
* Asus & Various RAM manufacturers:
	* Aura compatible RAM: RGB lighting (Provided that there is a SMBus driver for your motherboard or chipset, and that the RAM module ID is added to the list of supported module IDs is updated in the code)
		* G-Skill Trident Z Neo (F4-3600C18-32GTZN)
* NVIDIA
	* All GPUs: Support for the I2C interface to control connected monitors.
	* GeForce RTX 3090 FE and select other GPUs: RGB lighting
* Intel
	* WIP: Should at term be able to expose the I2C interface to control connected monitors, however the IGCL library support is currently very poor and doesn't work on many not-too-old configurations.
	* Monitor control via the I2C fallback (for when the IGCL library is unavailable, which is very frequent) 
* Gigabyte
	* Z490 VISION D and other similar motherboards:
		* IT5702 RGB controller: RGB lighting
		* SMBus driver for providing access to RGB RAM and other devices. (⚠️ Only available when run in Administrator mode or as a service)
* LG
	* 27GP950 Monitor (USB): Standard Monitor control, RGB lighting.
	* 27GP950 Monitor (DP/HDMI): Standard Monitor control (if connected through supported GPU)
* Elgato
	* SteamDeck XL (Protocol is implemented and mostly tested, but features are not exposed)
* Corsair
	* HX1200i: Sensors accessible via Corsair Link. (e.g. Temperature)
* Eaton
	* Various UPS Models: Power consumption and battery level.
* NZXT
	* Kraken Z devices: Screen brightness, Cooling control, Sensors for Liquid temperature, Pump speed and Fan speed.
* Other
	* Generic monitor support (Requires a GPU driver for the GPU the monitor is connected to; May require the UI helper to be started if the GPU driver cannot directly provide I2C support)

# Planned Features

Features are being added bit by bit, and while some are not yet fully designed, there is actually a vision on what to be done:

* dns-sd/mdns/bonjour service discovery (for Elgato lights, etc)
* Support for a "Light" feature, slightly different than "Lighting" feature, in that lights are independent entities that can be turned on and off externally.
* (Temporary?) support for persisting cooling settings between service restarts. This will probably be partially superseeded by the programming system. (NB: Persistence of lighting effects is implemented)
* Programming system that will allow creating customized complex setups to fit any user need, with predictable state transitions.
* CPU temperature sensor (Sadly requires a Kernel driver)

# Running Exo

## Prerequisites

### Mandatory

Exo relies on these runtimes, that you can download from the official websites:

* .NET 9.0.101 runtime: https://dotnet.microsoft.com/en-us/download/dotnet/9.0
* Windows App SDK 1.6 Runtime: https://aka.ms/windowsappsdk/1.6/1.6.241114003/windowsappruntimeinstall-x64.exe

### Optional

Some devices come with kernel drivers that are made by the manufacturer and help with providing support for the device features.
When these drivers are installed on the system, the features exposed to the OS can be different.
In some cases, Exo will require drivers from the manufacturer, and in other cases it will require them.

In any case, manufacturer drivers can't be bundled with Exo, for obvious reasons.

#### Razer kernel drivers

⚠️ Razer kernel drivers are not required anymore, and Exo should run fine **without** them. This section is kept for information only.

If you have installed and used Razer Synapse 3 with your device at one point, it is likely that those drivers are installed on your system.
You are free to decide whether to keep them or not. (Drivers can be uninstalled from the Windows device manager, using the driver view)

If you want to get the kernel drivers, (e.g. for preservation), the simplest way to get the drivers is to actually rely on a Synapse 3 installation:

1. Install or launch Razer Synapse 3 with your device(s) connected
2. Drivers will already be installed, but you can find the drivers in `C:\Program Files (x86)\Razer`.
3. Save the drivers somewhere else.
4. Either uninstall Razer Synapse (and reinstall the drivers if necessary) or disable and stop the Razer services (Setting them to manual startup is enough to disable them)

NB: It is also possible to obtain those drivers without a completing a full installation of Synapse 3, as there are multiple steps in the install.

### ⚠️ Conflicts with other software

As is usually the case for this category of software, Exo can sometimes conflict with other software running to control a device.
This is not always the case and depends on the device, but it is advised to stop other softwares (apps and services) before running Exo, in order to avoid problems.

e.g.:
* Logitech software: For HID++ 2.0 devices, an application ID is used in the protocol to prevent problems. For HID++ 1.0 devices (e.g. USB receivers), some conflicts are possible, although the code tries to ignoring interferences.
* Razer Synapse (in case of a supported device): The Razer protocol is not really designed to avoid conflicts, so the softwares can run into problems if two are running simultaneously.
* Stream Deck: When accessing the device Exo can slightly disrupt the StreamDeck software, but this is probably an intentional behavior from Elgato in order to allow others to control the device. Distruption is instantly fixed by simply opening the main window of the Stream Deck software.
* RGB Fusion 2.0: As long as the software is not open and running any effects, it seems that there are generally no conflicts.

## Getting a binary release

Exo is now released through a prebuild MSI installer, which you just need to run to get everything running.

You can grab a binary release either from the [Releases page](https://github.com/hexawyz/Exo/releases) or a build artifact from a recent [GitHub actions build](https://github.com/hexawyz/Exo/actions).
Depending on the situation it might be better to grab a release directly from a recent build, as preview releases may contain some bugs that have been fixed afterwards. Refer to the commit list for more details.

⚠️ You may still need to manually install the Windows App SDK from Microsoft if you haven't already done so. This is required to run the UI, but the service will always properly start without it.

# Developing and building Exo

## Prerequisites

* Visual Studio 2022 (Version Supporting .NET 8.0 at least) with the following workloads:
	* ASP.NET development
	* C++ Desktop development (This is only needed for a tiny bit of code that is sadly unavoidable)
	* .NET Desktop development
	* UWP development

## Within Visual Studio

From within Visual Studio, you can start `Exo.Service` to run the service, `Exo.Overlay` for the overlay UI, and `Exo.Settings.UI` for the Settings UI.

## Generating a publish release

Releases are generated using the WiX project `Exo.InstallerPackage`. Building this project in release mode will produce an MSI installer releasing the current code.

The WiX project will automatically request a publish build of each of three main projects, and bundle them into the installer.

While it is still possible to build and publish the three main projects manually, it requires more work and is unlikely to provide much extra value.
As such, this approach is not recommended, unless you have a very specific need in mind.

# Architecture

Disclaimer: I can't promise that Exo is (will not be) not over-engineered in some parts, as this is also an opinion matter. However, I'll try to choose the most efficient design to provide the most generic features possible.

## Top Level View

Exo is written entirely in C# (with the exception of a small unavoidable C++ helper in DeviceTools), running on the latest version of .NET, which will provide great performance.

The product is currently split in three executables:

* Exo.Service:
  This executable is runnable as a Windows service or regular console application (useful for debugging).
  It centralizes the management of all devices and implements all the features.
* Exo.Overlay:
  A user-facing application that will display overlay notifications, and provide means of interacting with the service, such as starting the settings UI.
  It displays a custom menu that will in the future, run command that are programmed by the user (you!) within the service.
  ⚠️ This application also provides the monitor control proxy that will be used as a fallback for kernel GPU drivers that don't expose their I2C. This can only work when the application is started.
* Exo.Settings.UI:
  A modern WinUI 3 user interface that will communicate with the service and provide graphical view and controls over all the discovered and supported devices.

Splitting the application this way allows to further preserve system resources, as running a settings UI should not be necessary 100% of the time.

As a general principle, the service communicates with the user interfaces using GRPC through named pipes.
The GRPC interfacing does indeed incur an overhead within the service itself, as the service has to maintain an observable state, but the service would be quite useless without any form of interface.

## Core

At its core, Exo is written with, and on top of helper libraries named `DeviceTools`, which provide access to useful and necessary device APIs.

The service itself is split in different layers:

* Driver layer: In order to expose features, a driver needs to be implemented for each device (or group of devices), which will expose a mostly direct mapping of hardware features of the device.
* High level service layer: For each group of features (e.g. RGB lighting), a service will collect the matching devices and expose the features in a centralized way.
* GRPC layer: For communication with the UI parts. The GRPC services will be a light or medium wrapping around the internal high-level services, providing UI-oriented informations.
* Core services: Everything that doesn't fit in the above categories
	* Configuration service that will persist important configuration
	* Device registry that references all known devices, connected or not
	* Discovery orchestrator that will detect and load optional components and drivers from plugin assemblies

## Device discovery

Before even speaking of drivers, it is important to understand that devices can be discovered in multiple ways.
We will generally on the Windows device APIs to enumerate devices and receive notifications, but there is some relatively heavy-lifting to do in order to consolidate the information in a useful way.
This is where the discovery orchestrator and the various discovery subsystems step in…

### The discovery orchestrator

The discovery orchestrator is responsible for orchestrating all the discovery and instanciation of the non-mandatory services within Exo, such as discovery subsystems and drivers.

It will scan plugin assemblies to identify all discovery subsystems and all component or driver factories, then wire everything together as needed, making sure to only load the required assemblies in memory.

This key part of the software is quite complex and may look a bit over-engineered-looking, but it provides the critical features necessary to manage components and drivers without loading everything in memory and duplicating a lot of code.

### Mandatory and optional components

Exo is built in a way so that many components are optional loaded on-demand. These components are defined within assemblies in the `plugins` directory and loaded on-demand.
There is only one component that is strictly mandatory, and that is, as such, not placed in the `plugins` directory. This special component is the root discovery subsystem.

Among assemblies placed in the `plugins` directory, some may declare mandatory components by tying them to the root discovery subsystem.
Components created in that way will never be unloaded. This is typically the case of all discovery subsystems.

These components are optional in the sense that you *can* remove them from the service. But if the assemblies are present, they will be loaded automatically.

### Discovery subsystems

The discovery subsystems will listen for components using means appropriate for the kind of component they manage.

Specifically in the case of devices, they will watch for already connected devices, and device arrivals and removals.
They will then provide a consolidated view of the device information to the driver factories, so that these can focus only on the features specific to the device they are supporting.

Currently available discovery subsystems are:

* Root in `Exo.Discovery.Root`, which is in charge of pulling in all the mandatory components.
* HID in `Exo.Discovery.Hid`, which is in charge of instantiating drivers for HID devices.
* PCI in `Exo.Discovery.Pci`, which is in charge of instantiating drivers for PCI devices. (NB: currently only supports GPUs)
* SMBIOS in `Exo.Discovery.SmBios`, which is in charge of instantiating drivers for devices detected through the SMBIOS tables. (NB: currently only support RAM devices)
* Monitor in `Exo.Discovery.Monitor`, which is in charge of instantiating drivers for monitor devices.

NB: Except for the root discovery subsystem, which is used to pull all the other ones, all discovery subsystems are themselves implemented as discoverable components.
This means that adding a discovery subsystem into the mix is very easy.

Each discovery subsystem provides a set of parameters available to factory methods.
Those parameters are defined as public properties of their Component/Driver creation context.

## Drivers

A driver for a device will derive from the `Driver` class, and implement `IDeviceDriver<>` for various kinds of features, such as `ILightingDeviceFeature`.

The drivers are instantiated from one or more factories declared as static methods attaching themselves to one or more discovery subsystems using the `DiscoverySubsystem<>` attribute.

The set of parameters available to the factory method depends on the device discovery subsystem. Their driver creation context will expose all of the available properties, and the factories will automatically be matched and validated by the discovery orchestrator.

In addition to this, discovery subsystems will use custom attributes to match devices to an appropriate factory method.
While those attributes are subsystem-dependent, the principle should be relatively easy to understand using a code example of a factory method declaration for a HID device:

````csharp
[DiscoverySubsystem<HidDiscoverySubsystem>]
[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
[ProductId(VendorIdSource.Usb, IteVendorId, 0x5702)]
public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
(
	ImmutableArray<SystemDevicePath> keys,
	ushort productId,
	ushort version,
	ImmutableArray<DeviceObjectInformation> deviceInterfaces,
	ImmutableArray<DeviceObjectInformation> devices,
	string topLevelDeviceName,
	CancellationToken cancellationToken
)
````

You can take a look at any of the drivers to get more specific examples of how things are done.

### Supported feature categories

Each driver must implement one or more features of one or more features category, through which the device features will be exposed.

#### `IGenericDeviceFeature`: Generic features

Features that are not specific to any device types will be in this category.

#### `IKeyboardDeviceFeature`: Keyboard features

Most keyboards would expose one or more features of this category.

#### `IMouseDeviceFeature`: Mouse features

Most mouses would expose one or more features of this category.

#### `IMonitorDeviceFeature`: Monitor features

Features in this category are used to expose features specific to monitor devices.

#### `IDisplayAdapterDeviceFeature`: Display adapter features

Currently only provides `IDisplayAdapterI2CBusProviderFeature`, which is used for a display adapter to give access to DDC/CI monitors.

#### `ILightingDeviceFeature`: Lighting features

Drivers for devices providing lighting zones would implement features in this category, starting with `ILightingControllerFeature`.

#### `IMotherboardDeviceFeature`: Motherboard features

Currently only provides `IMotherboardSystemManagementBusFeature`, which is used to provide access to the SMBus device on the motherboard.

This feature will be used to access RGB RAM that is controlled through SMBus.

#### `ISensorDeviceFeature`: Sensors features

Drivers for devices exposing sensors would generally implement features in this category, starting with `ISensorsFeature`.

Most drivers would also provide `ISensorsGroupedQueryFeature` as a means to efficiently query multiple sensors at once, depending on the usefulness.

#### `ICoolingDeviceFeature`: Cooling features

All devices having a controllable cooler would usually expose it through the `ICoolingControllerFeature` interface.

# Adding a monitor configuration

Support for various monitors can be improved or greatly improved by providing a custom configuration for your monitor.

Custom monitor configurations are located in [Exo.Devices.Monitors/Definitions](Exo.Devices.Monitors/Definitions/) in JSON format.
Those configurations will be parsed transformed into binary format during the build, in order to provide efficient runtime lookup.

## Determine if a custom configuration is needed

In all cases, you will need some degree of understanding of the VESA MCCS 2.2 specification. If you understand what is the capabilities string and what are VCP codes, you're probably good to go.

You may need a custom monitor configuration if:

* You want to provide a custom friendly name for the model of your monitor
* Your monitor incorrectly exposes VCP features, or if some of the exposed features are buggy (e.g. do not do what is expected)
* Some of the features that your monitor actually provides are not advertised in the capabilities string
* Features of your monitor have an unusual, non-standard mapping. (i.e. the VCP codes do not match the MCCS spec)
* Your monitor supports some of the custom features provided by Exo, and you want to enable them for your monitor:
	* Input lag
	* Response time
	* Blue light filter
	* On/Off power indicator
* You need to customize the text for discrete values of a setting such as input select

## Format of a custom configuration

The custom configuration model is defined in [Exo.Core/Monitors/MonitorDefinition.cs](Exo.Core/Monitors/MonitorDefinition.cs).

````json
{
	// Friendly name that will be used for the device
	"name": "BRAND Monitor",
	// Can override the capabilities string if desired. May be especially useful for monitors with a bogus capabilities string.
	"capabilities": null,
	// Provide manual overrides of some monitor features
	"overriddenFeatures": [
		// Provide a configuration to define the power indicator feature of the monitor
        {
            "vcpCode": 43,
            "feature": "powerIndicator",
			// Provide some explicit discrete values for the setting (if the setting is a discrete setting)
            "discreteValues": [
                {
                    // Off
                    "value": 1,
                    "nameStringId": "a9f9a2e6-2091-4bd9-b135-a4a5d6d4009e"
                },
                {
                    // On
                    "value": 2,
                    "nameStringId": "4d2b3404-1cb1-4536-918c-80facc124cf9"
                }
            ]
        },
		// Provides a configuration for the "Video Black Level (Red)" feature of the monitor, with a custom maximum value
        {
            "vcpCode": 108,
            "feature": "videoBlackLevelRed",
			// This maximum value will override the maximum returned by the monitor GetVCPFeature call.
            "maximumValue": 255
        },
	],
	// Ignore specific VCP codes from the capabilities string.
	"ignoredCapabilitiesVcpCodes": [
		// Ignore the Input Select VCP code
		96
	],
	// Allow to ignore all features advertised by the capabilities string and only rely on overriddenFeatures.
	// This is a stronger and simpler version of ignoredCapabilitiesVcpCodes.
	"ignoreAllCapabilitiesVcpCodes" : false
}
````

The strings are mapped using the metadata system of Exo.

## Naming your custom configuration

Custom configurations are named by the monitor (PNP) device ID, which is composed of 3 letters indicating the vendor, and 4 hexadecimal digits indicating the product ID.

A configuration can be mapped to multiple monitor IDs by separating those IDs with dashes.
For example, a configuration for monitor with the IDs `GSM5BBF`, `GSM5BC0` and `GSM5BEE`, should be named `GSM5BBF-GSM5BC0-GSM5BEE.json`.

# The metadata system

Exo provides a metadata system to provide non-critical data for devices that will mainly be used within the UI.

The core metadata component is the `Strings` component.
This component will provide localizable strings everywhere across the application. Those strings will always be referenced through a unique ID in the form of a GUID.

Other metadata components are:

* `LightingEffects`: Provides metadata for lighting effects.
* `LightingZones`: Provides metadata for lighting zones.
* `Sensors`: Provides metadata for sensors.
* `Coolers`: Provides metadata for coolers.

## GUID as object identifiers

Most elements within Exo are identified with a GUID. This GUID will be used to reference the element from various places, and will be used as (part of) the metadata key for the object.

Strings are no exception, and all (localizable) strings are referenced using their unique GUID.

## Defining metadata

As part of plugins, providing metadata is as simple as adding a JSON file appropriately named with the metadata category, e.g. `Strings.json`.
Metadata added in this way will be automatically detected, built and published with Exo.

NB: The service provides does provide core `Strings` and `LightingEffects` metadata, but those are not discovered dynamically.
It might make sense to provide common strings as part of the `Strings.json` file of `Exo.Service`.

The format of metadata depends on the kind of metadata represented. You can find examples in the code, or locate the core definitions in [Exo.Metadata](Exo.Metadata).

## When to provide metadata

You generally need to provide metadata as soon as you introduce a new UI-facing element that is identified by a GUID. (Lighting effect, lighting zone, sensor, cooler, …)

All those UI-facing elements will at least need to be provided with a friendly name that can be localized, and the way to provide that is through metadata.

## Example of string metadata

````json
{
	// Freshly generated GUID for a new string. (e.g. by calling Guid.NewGuid)
	"3d82a39b-9fd7-4393-854e-3c0e69532425": {
		// NB: English is considered to be the default. Always provide an english version for a string.
		"en": "The english text",
		"fr": "The french text",
		"de": "The german text",
		"ja": "The japanese text",
	}
}
````

# PowerShell functions to quickly generate or format GUIDs

As you may need to generate quite a few GUIDs when adding new features, these PowerShell functions may prove to be very useful:

````PowerShell
function New-Guid {
    param (
        [int] $Count = 1
    )

    for ($local:i = 0; $i -lt $Count; $i++) {
        Write-Output ([System.Guid]::NewGuid());
    }
}

function Format-Guid {
    param (
        [Parameter(Mandatory, ValueFromPipeline)]
        [System.Guid[]] $Guid
    )

    process {
        $local:Bytes = $Guid.ToByteArray();
        Write-Output "new Guid(0x$([System.BitConverter]::ToUInt32($local:Bytes, 0).ToString("X8")), 0x$([System.BitConverter]::ToUInt16($local:Bytes, 4).ToString("X4")), 0x$([System.BitConverter]::ToUInt16($local:Bytes, 6).ToString("X4")), 0x$($local:Bytes[8].ToString("X2")), 0x$($local:Bytes[9].ToString("X2")), 0x$($local:Bytes[10].ToString("X2")), 0x$($local:Bytes[11].ToString("X2")), 0x$($local:Bytes[12].ToString("X2")), 0x$($local:Bytes[13].ToString("X2")), 0x$($local:Bytes[14].ToString("X2")), 0x$($local:Bytes[15].ToString("X2")))"
    }
}
````
