using System.Collections.Immutable;
using DeviceTools;

namespace Exo.Features;

[TypeId(0x7D989093, 0xB4F6, 0x4D41, 0x8E, 0xE8, 0x56, 0x5E, 0x37, 0xA4, 0x15, 0x37)]
public interface IGenericDeviceFeature : IDeviceFeature
{
}

[TypeId(0x207EC5E4, 0x42DF, 0x4ACD, 0x8C, 0xA6, 0x05, 0xE4, 0xDF, 0xA3, 0x46, 0xAB)]
public interface IKeyboardDeviceFeature : IDeviceFeature
{
}

[TypeId(0xC3B7ED20, 0x9E91, 0x4BC9, 0xB8, 0x69, 0x1E, 0xEF, 0xE7, 0xBF, 0xAD, 0xC5)]
public interface IMouseDeviceFeature : IDeviceFeature
{
}

[TypeId(0x8B06210F, 0x8EDA, 0x483D, 0xB3, 0xBD, 0x30, 0xB5, 0xFA, 0xC7, 0x3E, 0x90)]
public interface IMonitorDeviceFeature : IDeviceFeature
{
}

[TypeId(0x58D73E98, 0xE202, 0x4F40, 0xBF, 0x69, 0x45, 0x1B, 0x82, 0x44, 0xF0, 0xAD)]
public interface IDisplayAdapterDeviceFeature : IDeviceFeature
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
[TypeId(0x71BBF8D6, 0x9BA0, 0x4A5E, 0x93, 0x08, 0x6C, 0xD5, 0x32, 0x66, 0x21, 0x81)]
public interface ILightingDeviceFeature : IDeviceFeature
{
}

/// <summary>Devices can allow access to their serial number by providing this feature.</summary>
public interface IDeviceSerialNumberFeature : IGenericDeviceFeature
{
	/// <summary>Gets the serial number of this device.</summary>
	string SerialNumber { get; }
}

/// <summary>Devices can allow access to their battery level by providing this feature.</summary>
public interface IBatteryStateDeviceFeature : IGenericDeviceFeature
{
	/// <summary>This event is raised when the battery level of the device has changed.</summary>
	event Action<Driver, BatteryState> BatteryStateChanged;

	/// <summary>Gets the current battery level.</summary>
	BatteryState BatteryState { get; }
}

/// <summary>Devices can expose their standard device ID by providing this feature.</summary>
/// <remarks>
/// <para>
/// Many devices will have a standardized device ID, such as PCI, USB and Bluetooth devices.
/// Some devices not connected through these means may still have a way to communicate their standard ID in one of the known namespaces.
/// If the device ID is known, it should be exposed through this feature.
/// </para>
/// <para>
/// If a device has multiple device IDs, this feature should only expose the main device ID, which can be reliably retrieved and used to uniquely identify the hardware.
/// It is possible for the version number to not always be available, though.
/// </para>
/// </remarks>
public interface IDeviceIdFeature : IGenericDeviceFeature
{
	DeviceId DeviceId { get; }
}

/// <summary>Devices can expose their standard device IDs by providing this feature.</summary>
/// <remarks>
/// <para>
/// This feature is very similar to the <see cref="IDeviceIdFeature"/> but allows devices to return multiple device IDs.
/// This feature is mostly useful for devices that can connect through multiple buses using different IDs, but it can always be provided in addition to or instead of <see cref="IDeviceIdFeature"/>.
/// </para>
/// <para>
/// If a driver exposes both interfaces, the ID returned by <see cref="IDeviceIdFeature"/> must be constant and represent the device ID that can be used for uniquely identifying the hardware.
/// The ID exposed by <see cref="IDeviceIdFeature"/> must always be contained in <see cref="DeviceIds"/>, and must also exactly match the <see cref="MainDeviceIdIndex"/> property.
/// </para>
/// <para>
/// e.g. HID++ devices can use different product IDs depending on how they're connected, but will generally provide easier access to the ID they use for communication with an USB receiver.
/// In that case, they should expose that product ID as the main ID instead of other ones such as Bluetooth.
/// </para>
/// <para>
/// Providing multiple IDs will also be useful for devices connected through multiple protocols at the same time, such as monitor with an USB connection.
/// In that case, though, it might be more difficult to settle on a main ID, as both the EDID and the USB could be seen as equally important in their own way.
/// Drivers would generally need to maintain their own mapping table to provide access to both IDs and decide on which one to expose.
/// In many cases, it might be wiser to expose the PNP ID from EDID as the main one.
/// </para>
/// </remarks>
public interface IDeviceIdsFeature : IGenericDeviceFeature
{
	ImmutableArray<DeviceId> DeviceIds { get; }
	int? MainDeviceIdIndex { get; }
}

/// <summary>Devices can expose their connection means using this feature.</summary>
/// <remarks>
/// <para>This feature should generally be provided by all drivers, as the connection mean is generally easily identifiable, and often fixed.</para>
/// <para>
/// Because some devices can support different connection means to the same computer, the information should only be used for informational purposes, such as displaying it to the user.
/// It cannot be used to identify the device in itself, but it is appropriate to cache the last value between device connections.
/// </para>
/// <para>
/// The values exposed by the feature should be read with the understanding that some devices can be connected to the computer through multiple channels at the same time.
/// This would be the case of a monitor connected both through DisplayPort and USB.
/// </para>
/// </remarks>
public interface IDeviceConnectionType : IGenericDeviceFeature
{
	DeviceConnectionTypes CurrentConnectionTypes { get; }
	DeviceConnectionTypes SupportedConnectionTypes { get; }

	/// <summary>Notifies that the current connection types changed.</summary>
	/// <remarks>
	/// The default implementation does never raise this event.
	/// Checking the value of <see cref="CurrentConnectionTypesCanChange"/> can be useful in order to avoid allocating a delegate.
	/// </remarks>
	event Action<Driver, DeviceConnectionTypes> CurrentConnectionTypesChanged
	{
		add { }
		remove { }
	}

	/// <summary>Indicates if <see cref="CurrentConnectionTypes"/> can change.</summary>
	/// <remarks>
	/// When this property is <see langword="false" />, <see cref="CurrentConnectionTypes"/> has a constant value, and <see cref="CurrentConnectionTypesChanged"/> is never raised.
	/// </remarks>
	bool CurrentConnectionTypesCanChange => false;
}

/// <summary>Devices can notify feature sets availability using this feature.</summary>
/// <remarks>
/// <para>
/// For some devices, the feature set might not always be constant and could change over time, with some feature groups becoming available or unavailable depending on the situation.
/// This feature, exposed as a base feature, allows not relying on the usual mechanism of exposing features through implementing <see cref="IDeviceDriver{TFeature}"/>, and instead rely on features
/// described by <see cref="Driver.FeatureSets"/>.
/// </para>
/// <para>
/// It is always assumed that for a given device, features available within a feature set will always be constant between availability changes.
/// This means that services can only watch for the availability changes to determine if a given feature is available, without having to re-examine the whole feature set.
/// If there is a case for a device to have truly dynamic features, this can be further improved, but it does not look like a very realistic scenario.
/// </para>
/// </remarks>
public interface IVariableFeatureSetDeviceFeature : IDeviceFeature
{
	/// <summary>Notifies of a change in availability of a given feature set.</summary>
	/// <remarks>
	/// <para>
	/// Change notifications will indicate a change from available to unavailable or the opposite, for each feature whose availability changed.
	/// For consistency, the associated feature collection will always be passed, in order to avoid inconsistencies when accessing <see cref="Driver.FeatureSets"/> at a different time.
	/// </para>
	/// <para>
	/// When a feature set becomes unavailable, the feature collection provided will be empty.
	/// </para>
	/// </remarks>
	public event FeatureSetEventHandler FeatureAvailabilityChanged;
}
