using System;
using System.Collections.Generic;

namespace Exo.Features.CompositeFeatures;

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
/// This would be the case for Logitech USB Unifying or Bolt receivers, which merge multiple keyboards and mouse into a single logical one, while still allowing independant access to each of them.
/// Given that most feature sets wouldn't intersect each other, we can expect the need for this to be quite niche. However, it exists.
/// </para>
/// </remarks>
public interface ICompositeDeviceFeature : IDeviceFeature
{
}

/// <summary>Exposes the child device drivers of a composite device.</summary>
/// <remarks>The presence of this feature is mandatory for a composite driver.</remarks>
public interface ICompositeDeviceChildrenFeature : ICompositeDeviceFeature
{
	IReadOnlyList<Driver> Drivers { get; }
}

/// <summary>Exposes a notification for an update to the children of a composite driver.</summary>
/// <remarks>The presence of this feature is not mandatory, but indicates that the list of drivers returned by <see cref="ICompositeDeviceChildrenFeature"/> can change over time.</remarks>
public interface INotifyChildrenChange : ICompositeDeviceFeature
{
	event EventHandler ChildrenChanged;
}
