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

Other than being slow and consuming a huge chunk of your RAM for nothing (do you honestly believe you need 5 chrome processes to manage a mouse?), those applications are more often than not very stable, and can have undesired behavior such as random updates or invisible crashes.

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

# Supported devices

NB: Support of a device does not mean that all of its features will be exposed in the application. It will be recognized and connected to the application, but the bricks may not yet be in place to expose the features.

* Logitech
	* All HID++ devices (with some possible exceptions), including through USB receivers: Battery level and keyboard reporting features.
* Razer (NB: Requires the official *kernel* drivers from Razer to be installed)
	* DeathAdder V2 Pro (USB, Dongle): Battery Level, RGB lighting, DPI changes.
	* Mouse Dock Chroma: RGB
* NVIDIA
	* All GPUs: Support for the I2C interface to control connected monitors.
	* GeForce RTX 3090 FE and select other GPUs: RGB lighting
* Intel
	* WIP: Should at term be able to expose the I2C interface to control connected monitors, however the IGCL library support is currently very poor and doesn't work on many not-too-old configurations.
* Gigabyte
	* IT5702 RGB controller: RGB lighting
* LG
	* 27GP950 Monitor (USB): Standard Monitor control, RGB lighting.
	* 27GP950 Monitor (DP/HDMI): Standard Monitor control (if connected through supported GPU)
* Elgato
	* SteamDeck XL (Protocol is implemented and mostly tested, but features are not exposed)
* Other
	* Generic monitor support (Currently works only for monitors connected to NVIDIA GPUs)

# Running Exo

There is currently no official build available, but you can run and help develop Exo by installing the latest version of Visual Studio. You will need the C# and C++ workloads installed, as well as anything necessary to develop WinUI3 applications.

From within Visual Studio, you can start `Exo.Service` to run the service, `Exo.Overlay` for the overlay UI, and `Exo.Settings.UI` for the Settings UI.

You can generate a publish build of the service from the VS developer command prompt:
````
MSBuild Exo.Service\Exo.Service.csproj /t:Publish /p:Configuration=Release
````

And if you fancy trying to run it as a service, you can create a service using the following PowerShell command:

````PowerShell
New-Service -Name "Exo" -BinaryPathName "<PATH TO YOUR GIT REPOSITORY>Exo.Service\bin\Release\net8.0-windows\publish\Exo.Service.exe" -DisplayName "Exo" -Description "Exo the exoskeleton for your Windows PC and devices." -StartupType Manual
````

The command will create a manual startup service, which you can then start with `Start-Service Exo`.

NB: The `Publish.ps1` script can be used to generate a publish build of all the binaries under a `publish` directory at the root of the git repository.

# Architecture

Disclaimer: I can't promise that Exo is (will not be) not over-engineered in some parts, as this is also an opinion matter. However, I'll try to choose the most efficient design to provide the most generic features possible.

## Top Level View

Exo is written entirely in C# (with the exception of a small unavoidable C++ helper in DeviceTools), running on the latest version of .NET, which will provide great performance.

The product is currently split in three executables:

* Exo.Service:
  This executable is runnable as a Windows service or regular console application (useful for debugging).
  It centralizes the management of all devices and implements all the features.
* Exo.Overlay:
  A user-facing application that will display overlay notifications, and (in the future) provide means of interacting with the service, such as starting the settings UI.
* Exo.Settings.UI:
  A modern WinUI 3 user interface that will communicate with the service and provide graphical view and controls over your devices.

Splitting the application this way allows to further preserve system resources, as running a settings UI should not be necessary 100% of the time.

As a general principle, the service communicates with the user interfaces using GRPC through named pipes.
The GRPC interfacing does indeed incur an overhead within the service itself, as the service has to maintain an observable state, but the service would be quite useless without any form of interface.

## Core

At its core, Exo is written with, and on top of helper libraries named `DeviceTools`, which provide access to useful and necessary device APIs.

The service itself is split in different layers (this is where the over-engineering might be 😉):

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

### The orchestrator

This is where the discovery orchestrator and the various discovery subsystems step in.
The discovery orchestrator will identify all discovery subsystems and all component or driver factories and wire everything together, making sure to only load the required assemblies in memory.

This likely is the most over-engineered-looking piece of the software, but it provides the critical features necessary to manage components and drivers without loading everything in memory and duplicating a lot of code.

### The discovery subsystems

The discovery subsystems will listen for components using means appropriate for the kind of component they manage.

Specifically in the case of devices, they will watch for already connected devices, and device arrivals and removals.
They will then provide a consolidated view of the device information to the driver factories, so that these can focus only on the features specific to the device they are supporting.

Currently available discovery subsystems are:

* Root in `Exo.Discovery.Root` (NB: Only for pulling in mandatory components)
* HID in `Exo.Discovery.Hid`
* PCI in `Exo.Discovery.Pci` (NB: currently only supports GPUs)
* Monitor in `Exo.Discovery.Monitor`

NB: Except for the root discovery subsystem, which is used to pull all the other ones, all discovery subsystems are themselves implemented as discoverable components.
This means that adding a discovery subsystem into the mix is very easy.

Each discovery subsystem provides a set of parameters available to factory methods. Those parameters are defined as public properties of their Component/Driver creation context.

## Drivers

A driver for a device will derive from the `Driver` class, and implement `IDeviceDriver<>` for various kinds of features, such as `ILightingDeviceFeature`.

The drivers will be instantiated from one or more factories declared as static methods attaching themselves to one or more discovery subsystems using the `DiscoverySubsystem<>` attribute.

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
