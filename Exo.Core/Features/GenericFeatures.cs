using System.Collections.Immutable;
using DeviceTools;

namespace Exo.Features;

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
public interface IVariableFeatureSetDeviceFeature : IGenericDeviceFeature
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
