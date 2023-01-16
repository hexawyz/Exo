using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeviceTools.HumanInterfaceDevices;

namespace Exo.Core;

// TODO: Make the key into a builder pattern providing priority rules for comaptibility keys ?
// Whatever, the idea is to always have the main key be <Driver>:<DeviceName>, and other keys being indirections towards that key.
// So, when a non-main key is used, it will move the configuration to another main key.
// What if a device is very changing ports then ? Do we agree the configuration is updated more often than expected ?
// That's maybe where the priority mechanism can take action

/// <summary>A device configuration key contains various parameters helping resolve the configuration for a specified device.</summary>
/// <remarks>
/// <para>
/// All device should have an unique device ID on the system, but drivers can manage a composite device and hence use that ID as a recongnizable key.
/// For drivers relating to native Windows devices, this should always be a device instance ID or container ID, which are guaranteed unicity on the system.
/// For drivers managing devices that are not directly recognized by Windows, this should be a string with equivalent unicity as a device instance ID. 
/// </para>
/// <para>
/// The driver key should be a string uniquely identifying the driver in order to avoid name collisions.
/// No need to be over-specific by having the exact driver type name, as it could change after a refactoring. A quick identifyiong key would be enough.
/// </para>
/// <para>
/// The movable device key is a fallback configuration key that can be used to reattach configuration to a device when it moved ports or when it is replaced by an identical one.
/// It only needs to be specific enough to be able to distinguish between incompatible devices. For many drivers, this would be a constant string.
/// All movable keys will be suffixed with an index, in order to support multiple instances of a similar hardware device.
/// </para>
/// <para>
/// Some device instances can be identified with a unique ID such as a MAC address or a serial number.
/// When available, this allows tracking the device more precisely when it is moved across ports.
/// </para>
/// </remarks>
/// <param name="DriverKey">A string uniquely identifying the driver.</param>
/// <param name="DeviceMainId">The main device ID (or device instance ID) for the driver.</param>
/// <param name="MovableDeviceKey">A more generic ID for the device, that can match all sufficiently similar devices</param>
/// <param name="SerialNumber">When available, a string that can serve as a serial number to uniquely identify the physical device.</param>
public record struct DeviceConfigurationKey(string DriverKey, string DeviceMainId, string MovableDeviceKey, string? SerialNumber);
