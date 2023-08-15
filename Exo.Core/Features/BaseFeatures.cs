using DeviceTools;

namespace Exo.Features;

/// <summary>Composite drivers must implement <see cref="IDeviceDriver{TFeature}"/> with <see cref="ICompositeDeviceFeature"/>.</summary>
/// <remarks>
/// <para>A composite driver is a driver which expose multiple dependent drivers.</para>
/// <para>
/// When possible, the recommended setup is for drivers to expose multiple <see cref="IDeviceDriver{TFeature}"/> implementations for each fo their respective facets.
/// Windows would typically expose a device object for each HID collection, but it is not such a good model for our use case, as we want to reason as close as possible to the concept of physical
/// devices. It also helps on the device management side, to have a single (top-level) driver object associated to a device.
/// As such, a driver for an HID device would typically connect to all the (relevant) HID collections corresponding to the same physical device, and expose their feature as different
/// <see cref="IDeviceFeature"/> sets.
/// For example, a keyboard device may typically provide backlighting or even RGB backlighting, both exposed under different HID collections, and different HID devices under Windows, but the two
/// collections/devices would still map to that same physical keyboard.
/// </para>
/// <para>
/// However, more advanced setups may be needed, for example in the case where a driver would be able to expose multiple devices of the same kind.
/// This would be the case for Logitech USB Unifying or Bolt receivers, which merge multiple keyboards and mouse into a single logical one, while still allowing independent access to each of them.
/// Given that most feature sets wouldn't intersect each other, we can expect the need for this to be quite niche. However, it exists.
/// </para>
/// </remarks>
public interface ICompositeDeviceFeature : IDeviceFeature
{
}

public interface IKeyboardDeviceFeature : IDeviceFeature
{
}

public interface IMouseDeviceFeature : IDeviceFeature
{
}

public interface IMonitorDeviceFeature : IDeviceFeature
{
}

/// <summary>Defines features related to lighting.</summary>
/// <remarks>
/// <para>Devices providing controllable lights control should expose features based on <see cref="ILightingDeviceFeature"/> to allow client components to control the lighting.</para>
/// <para>
/// Lighting capabilities can be very uneven across devices.
/// As such, we try to provide a realistic abstraction here so that drivers can expose their supported lighting modes in the most direct way possible.
/// </para>
/// <para>
/// The lighting model is built around the idea of lighting zones.
/// A lighting zone is a grouping of one or more lights that can be controlled by applying various effects at the same time.
/// Except for the <see cref="IUnifiedLightingFeature"/> that allows controlling a whole device as one single light zone,
/// drivers should expose light zones in a way that is as close as possible to the real hardware. i.e. The embedded lighting controller in the device.
/// </para>
/// <para>
/// In theory, any device could be generalized into an array of addressable RGB colors, mapping to a single light zone.
/// However, that would not be an appropriate representation for many devices, as some light effects can only be applied to certain physical zones on some devices.
/// Leveraging the intrinsic effects of the embedded RGB controllers is an important feature to have, as controlling lighting animations manually can be costly on the software side.
/// As such, the abstraction provided here intends to expose the features supported by the RGB controller with as much fidelity as possible.
/// More advanced features, such as controlling various light zones, even across multiple devices, in a synchronized way, can be left to other more generic components.
/// </para>
/// <para>
/// All devices should implement at least one of <see cref="ILightingControllerFeature"/> or <see cref="IUnifiedLightingFeature"/>.
/// While only one of those implementations should be used at the same time, most devices should implement both features.
/// It is important to note that some lighting controllers can benefit from more efficient global control of all lighting zones, and even provide specific effects when controlling all lights at once.
/// In these case, the <see cref="IUnifiedLightingFeature"/> is more than just a helpful shortcut to control device lighting.
/// </para>
/// <para>
/// All lighting device drivers should buffer effect changes and apply them only once <see cref="ILightingControllerFeature.ApplyChanges"/> (or the equivalent <see cref="IUnifiedLightingFeature"/>)
/// is called. This ensures that multiple lighting zones on the same device can be updated close to simultaneously, as efficiently as possible, and provides consistency between lighting mode updates.
/// </para>
/// <para>
/// Dynamic addressable lighting effects still need to be applied in the same way as other effects, but further updates to the colors must be processed by the effect itself, likely by the means of
/// a specific <c>ApplyChanges</c> or <c>Flush</c> method implemented on the effect.
/// </para>
/// </remarks>
public interface ILightingDeviceFeature : IDeviceFeature
{
}

/// <summary>Devices can allow access to their serial number by providing this feature.</summary>
public interface ISerialNumberDeviceFeature : IDeviceFeature
{
	/// <summary>Gets the serial number of this device.</summary>
	string SerialNumber { get; }
}

/// <summary>Devices can allow access to their battery level by providing this feature.</summary>
public interface IBatteryLevelDeviceFeature : IDeviceFeature
{
	/// <summary>This event is raised when the battery level of the device has changed.</summary>
	event Action<Driver, float> BatteryLevelChanged;

	/// <summary>Gets the current battery level.</summary>
	float BatteryLevel { get; }
}

/// <summary>Devices can expose their standard device ID by providing this feature.</summary>
/// <remarks>
/// Many devices will have a standardized device ID, such as PCI, USB and Bluetooth devices.
/// Some devices not connected through these means may still have a way to communicate their standard ID in one of the known namespaces.
/// If the device ID is known, it should be exposed through this feature.
/// </remarks>
public interface IDeviceIdDeviceFeature : IDeviceFeature
{
	DeviceId DeviceId { get; }
}
