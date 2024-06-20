[![Build Status](https://github.com/hexawyz/Exo/actions/workflows/build.yml/badge.svg)](https://github.com/hexawyz/Exo/actions/workflows/build.yml)

# Exo

Exo is the exoskeleton for your Windows computer (Or at least it aims to be ☺️)

You can use it to manage your Mouses, Keyboards, RGB lighting, etc.

The project was started out of spite for all the client-side "drivers" for devices, that consume unimaginable amounts of RAM and CPU.
As such, the goal of Exo is to replace all of these tools in a single service that will be more stable, consume a relatively small memory footprint, with a low CPU overhead.

⚠️ The project is still in the development phase, but it does run and already provides working drivers for quite a few devices.
As the author, I generally try to be conservative relative to the supported devices, but some of the code is probably already compatible with more devices than it declares. In that case, do not hesitate to contribute by opening an issue.

# Why

A lot of devices sold today, from mouses to RGB controllers and monitors, often provide features that can't be accessed directly at the OS level.
This is fine, but those device require drivers and/or applications to provide their useful features to you.

When these apps exist, they are more often than not presented as client-side application that will be a terrible Electron-based mess, and almost always as a bloated unoptimized suite with features you will never need.

Other than being slow and consuming a huge chunk of your RAM for nothing, those applications are more often than not not very stable, and can have undesired behavior such as random updates or invisible crashes. (Do you really need 5 unstable chrome processes to manage a mouse?)

As the author of Exo, I believe (and by now, have mostly proven 😄) that it is possible to do a much better job than this on all aspects.
Exo is designed and architected with this in mind, and aims to provide a Windows service that will detect and operate on your device by being mindful of system resources. (Currently about 30 MB and mostly no CPU usage)

# Supported features

Exo currently support the following features, provided that there is a custom driver to expose the features of your device in the service:

* Overlay notifications: Some of the other features will push notifications that will be displayed on screen.
* RGB Lighting: Setting hardware effects is supported, dynamic ARGB lighting not yet ready.
* Device battery status: The service will be aware of, and display the battery charge of your device, as well as show notifications for low battery, etc.
* Monitor control: Brightness, Contrast and Audio Volume.
* Keyboard backlighting: The service will observe keyboard backlight changes and push overlay notifications.
* Mouse: The service can observe and display DPI changes.
* GPU: Provide support for accessing connected monitors in non-interactive (service) mode.
* Sensors: For devices that provide data readings, expose sensors that can be read periodically and displayed in the UI.
* Coolers: For cooling devices, or devices equipped with a fan, expose controllable coolers and allow setting custom software cooling curves based on sensors.

# Supported devices

NB: Support of a device does not mean that all of its features will be exposed in the application. It will be recognized and connected to the application, but the bricks may not yet be in place to expose the features.

* Logitech
	* All HID++ devices (with some possible exceptions), including through USB receivers: Battery level and keyboard reporting features.
* Razer (NB: Requires the official *kernel* drivers from Razer to be installed)
	* DeathAdder V2 Pro (USB, Dongle): Battery Level, RGB lighting, DPI changes.
	* Mouse Dock Chroma: RGB
* Asus & Various RAM manufacturers:
	* Aura compatible RAM: RGB lighting (Provided that there is a SMBus driver for your motherboard or chipset, and that the RAM module ID is added to the list of supported module IDs is updated in the code)
		* G-Skill Trident Z Neo (F4-3600C18-32GTZN)
* NVIDIA
	* All GPUs: Support for the I2C interface to control connected monitors.
	* GeForce RTX 3090 FE and select other GPUs: RGB lighting
* Intel
	* WIP: Should at term be able to expose the I2C interface to control connected monitors, however the IGCL library support is currently very poor and doesn't work on many not-too-old configurations.
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
	* Generic monitor support (Currently works only for monitors connected to NVIDIA GPUs)

# Running Exo

## Prerequisites

### Mandatory

Exo relies on these runtimes, that you can download from the official websites:

* .NET 8.0 runtime: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
* Windows App SDK 1.5 Runtime: https://aka.ms/windowsappsdk/1.5/1.5.240607001/windowsappruntimeinstall-x64.exe

### Optional

Some devices comme with kernel drivers that are made by the manufacturer and help with providing support for the device features.
We can't bundle those drivers with Exo (at least not for now), but we do rely on them to provide the features as well.

* Razer: The simplest way to get the drivers is to actually rely on a Synapse 3 installation
	1. Install Razer Synapse 3
	2. Drivers will already be installed, but you can find the drivers in `C:\Program Files (x86)\Razer`.
	3. Save the drivers somewhere else.
	4. Either uninstall Razer Synapse (and reinstall the drivers if necessary) or disable and stop the Razer services (Setting them to manual startup is enough to disable them)

### ⚠️ Conflicts with other software

As is usually the case for this category of software, Exo can sometimes conflict with other software running to control a device.
This is not always the case and depends on the device, but it is advised to stop other softwares (apps and services) before running Exo, in order to avoid problems.

e.g.:
* Logitech software: There is generally no conflict, as far as I can tell. An application ID is used in the protocol to prevent problems.
* Razer Synapse (in case of a supported device): The Razer protocol is not really designed to avoid conflicts, so the softwares can run into problems if two are running simultaneously.
* Stream Deck: When accessing the device Exo can slightly disrupt the StreamDeck software, but this is probably an intentional behavior from Elgato in order to allow others to control the device. Distruption is instantly fixed by simply opening the main window of the Stream Deck software.
* RGB Fusion 2.0: As long as the software is not open and running any effects, it seems that there are generally no conflicts.

## Getting a binary release

You can a binary release either from the [Releases page](https://github.com/hexawyz/Exo/releases) or a build artifact from a recent [GitHub actions build](https://github.com/hexawyz/Exo/actions).

## Running Exo

Extract the release somewhere of your choice. e.g. create a directory `c:\tools\exo` and put everything ere. It is up to you to decide.

⚠️ It is important to not mix up executables from different releases. Newer versions will validate that the versions match before communicating with the service.

### As a command line application (Recommended for a first try)

* Run `Exo.Service.exe` from where it was extracted, and watch the service start. There can be some error messages appearing during startup for some non supported devices, this is normal.
* Run `Exo.Overlay.exe` from where it was extracted. This will start the overlay UI that exposes a notification icon and can display some overlay notifications.
* In order to access the settings UI, you can use the taskbar tray icon created by `Exo.Overlay.exe` or manually run `Exo.Settings.UI.exe` from where it was extracted.
  The UI windows will show up and present you with the supported devices that were detected on your system.

### As a Windows Service

If you know that Exo.Service is working on your system, you can instead start it as a Windows Service, which is the intended way of using it 🙂

Let's assume that you extracted the release in `C:\Tools\Exo` for this. (You can adapt depending on your installation)

#### Create the Service

If not yet created, you can create the service using the following PowerShell command:

````PowerShell
New-Service -Name "Exo" -BinaryPathName "C:\Tools\Exo\Exo.Service\Exo.Service.exe" -DisplayName "Exo" -Description "Exo the exoskeleton for your Windows PC and devices." -StartupType Manual
````

In the command above, I set the startup type to `Manual`, but you can set it to `Automatic` in order to have it start with Windows.

#### Starting the service

Still in PowerShell, run the following command:

````PowerShell
Start-Service Exo
````

Alternatively, you can control the service using the Windows Service Manager UI, or the Windows 11 Task Manager.

#### Running the UIs

The UI can be started manually in the same way as was explained earlier for running Exo in command line mode.

#### Stopping the service

Similarly, in PowerShell, run the following command:

````PowerShell
Stop-Service Exo
````

This can also be done from the Windows UI.

#### Removing the service

If you decide to remove the service from your system for any reason, this can be done as simply as you created it, using the PowerShell command:

````PowerShell
Remove-Service -Name "Exo"
````

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

From the VS developer command prompt, you can run the `Publish.ps1` script that is at the root of the repository:

````PowerShell
pwsh -ExecutionPolicy Bypass -File .\Publish.ps1
````

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
* Exo.Settings.UI:
  A modern WinUI 3 user interface that will communicate with the service and provide graphical view and controls over your devices.

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
