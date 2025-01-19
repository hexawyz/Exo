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

[TypeId(0xA6A121D7, 0xE5A3, 0x49A6, 0x88, 0xBE, 0xE7, 0x52, 0x39, 0xED, 0x9E, 0x3A )]
public interface IMotherboardDeviceFeature : IDeviceFeature
{
}

/// <summary>Defines features for devices surfacing sensor data.</summary>
[TypeId(0x3794C9DA, 0x9943, 0x4E9E, 0xB6, 0x86, 0x84, 0x8E, 0x31, 0xA5, 0x49, 0x49)]
public interface ISensorDeviceFeature : IDeviceFeature
{
}

/// <summary>Defines features for cooling devices.</summary>
/// <remarks>Cooling devices are expected to provide way to configure fans and pumps.</remarks>
[TypeId(0x52F4D370, 0xCF98, 0x430D, 0xA1, 0x54, 0xF5, 0x6B, 0x89, 0x2C, 0xA8, 0xB3)]
public interface ICoolingDeviceFeature : IDeviceFeature
{
}

/// <summary>Defines features for devices with power management features.</summary>
/// <remarks>
/// This feature class will be mostly useful for wireless and battery powered devices, but its usage is not limited to those.
/// </remarks>
[TypeId(0x25409C72, 0xFDCA, 0x48E9, 0x96, 0xF7, 0xA5, 0x00, 0xA6, 0x6D, 0xDE, 0xC0)]
public interface IPowerManagementDeviceFeature : IDeviceFeature
{
}

/// <summary>Defines features for devices with embedded monitors.</summary>
/// <remarks>Devices providing embedded monitors of any form should expose this feature to allow the software to control them.</remarks>
[TypeId(0x7A02A05E, 0x958B, 0x4039, 0xAF, 0xBA, 0xC5, 0xD2, 0xF7, 0x87, 0x71, 0x4B)]
public interface IEmbeddedMonitorDeviceFeature : IDeviceFeature
{
}

