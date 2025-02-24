using System.Collections.Immutable;
using System.Runtime.InteropServices;
using DeviceTools.HumanInterfaceDevices;

namespace Exo.Features.UserInput;

/// <summary>Exposes the remappable buttons on the device.</summary>
public interface IRemappableButtonsFeature : IUserInputDeviceFeature
{
	ImmutableArray<RemappableButtonDefinition> Buttons { get; }
	// TODO: Actions should have parameters. Maybe GUID is not the best way to represent them? (Main point of GUID is that they the ID space is large and they can be shared for reuse when needed.)
	void SetAction(ButtonId buttonId, Guid actionId);
	void ResetButton(ButtonId buttonId);
	ValueTask ApplyChangesAsync(CancellationToken cancellationToken);
}

/// <summary>On devices supporting button interception, this provides button events for intercepted buttons.</summary>
public interface IInterceptedButtonsFeature : IUserInputDeviceFeature
{
	event ButtonEventHandler ButtonDown;
	event ButtonEventHandler ButtonUp;
}

public delegate void ButtonEventHandler(Driver driver, ButtonId button);

public readonly struct RemappableButtonDefinition(ButtonId buttonId, ButtonCapabilities capabilities, Guid embeddedMonitorId, ImmutableArray<Guid> allowedActions)
{
	/// <summary>The ID used to reference the button.</summary>
	public ButtonId ButtonId { get; } = buttonId;

	/// <summary>The capabilities of the button.</summary>
	public ButtonCapabilities Capabilities { get; } = capabilities;

	/// <summary>If this button has a customizable screen, this is the embedded monitor ID associated with it through <see cref="EmbeddedMonitors.IEmbeddedMonitorControllerFeature"/>.</summary>
	public Guid EmbeddedMonitorId { get; } = embeddedMonitorId;

	/// <summary>Gets the (custom) actions that are allowed to be bound to the button.</summary>
	public ImmutableArray<Guid> AllowedActions { get; } = allowedActions;
}

/// <summary>Defines actions that can be common to multiple devices.</summary>
public static class WellKnownActions
{
	public static Guid HardwareDefault = default;
	public static Guid Intercept = new(0x3F77FBF7, 0x5EF2, 0x4CA4, 0x96, 0x53, 0x84, 0x5F, 0xFA, 0xCC, 0xDC, 0x3E);
}

[Flags]
public enum ButtonCapabilities : ushort
{
	None = 0b_00000000_00000000,
	/// <summary>Indicates that the button has a default behavior.</summary>
	/// <remarks>
	/// <para>
	/// A default behavior can be to do nothing.
	/// This flag is mainly used to indicate that this button is not intercepted by default,
	/// meaning that it either does not send anything to the OS or that it will send some predefined well-known events to the host.
	/// </para>
	/// <para>
	/// For example:
	/// <list type="bullet">
	/// <item>
	/// <term>A OS-visible default behavior</term>
	/// <description>The key <c>A</c> on a HID keyboard would typically always transmit keypresses to the OS for the HID Usage Keyboard/A.</description>
	/// </item>
	/// <item>
	/// <term>A OS-invisible default behavior</term>
	/// <description>Some logitech keyboards have buttons that will be used to switch host. By default, these buttons will not report to the OS, but they will do something on the device.</description>
	/// </item>
	/// <item>
	/// <term>A no-operation behavior</term>
	/// <description>A device can have a button that will by default do nothing at all, but can be <b>enabled</b> for custom processing by software.</description>
	/// </item>
	/// <item>
	/// <term>Absence of a default behavior</term>
	/// <description>A StreamDeck device will exposing a set of buttons that will always forward generic events to the host through custom HID descriptors.</description>
	/// </item>
	/// </list>
	/// </para>
	/// </remarks>
	HasDefaultBehavior = 0b_00000000_00000001,
	/// <summary>Indicates that the button can be intercepted.</summary>
	/// <remarks>A button that has no default behavior is considered to be intercepted all the time.</remarks>
	IsInterceptable = 0b_00000000_00000010,
	/// <summary>Indicates that the button has a customizable graphical display.</summary>
	HasCustomDisplay = 0b_00000000_00000100,
}

// Instead of defining yet another lis tof IDs, we will just rely on HID usages to define buttons.
// The only caveat is that we need to support devices having multiple buttons mapped to the same HID usage. (Which should be pretty rare but NOT impossible)
// For that, an easy solution is to add an extra instance ID to discriminate among buttons.
// For buttons that are 100% custom, with no default meaning associated with them, drivers can just assign arbitrary IDs in the vendor-defined HID Usage Pages (0xFF00 to 0xFFFF).
// ⚠️ We don't care at if the HID usages referenced here do not map to actual hardware implementation. HID usages are just a means to an end, which is to know what is the intended purpose of a button.
[StructLayout(LayoutKind.Sequential, Pack = 2, Size = 12)]
public readonly struct ButtonId(HidUsagePage buttonUsagePage, ushort buttonUsage, ushort buttonUsageInstanceIndex = 0)
{
	/// <summary>Gets the HID usage page associated with this button.</summary>
	public HidUsagePage ButtonUsagePage { get; } = buttonUsagePage;
	/// <summary>Gets the HID usage associated with this button in the page specified by <see cref="ButtonUsagePage"/>.</summary>
	/// <remarks>
	/// <para>
	/// The usage represented by <see cref="ButtonUsagePage"/> and <see cref="ButtonUsage"/> must reflect the standard behavior of the button on the device, if there is any.
	/// Otherwise, an arbitrary custom ID from any of the vendor-defined pages must be used.
	/// </para>
	/// <para>
	/// In some situations, multiple buttons may be associated with the same HID usage.
	/// This scenario is handled by assigning increasing instance indices to each button.
	/// </para>
	/// </remarks>
	public ushort ButtonUsage { get; } = buttonUsage;
	/// <summary>Gets an index associated with the button for the specified usage.</summary>
	/// <remarks>
	/// In most cases, the value returned must be <c>0</c>.
	/// If two or more buttons are associated with the same usage, each button must have a different index assigned, starting at 0.
	/// </remarks>
	public ushort ButtonUsageInstanceIndex { get; } = buttonUsageInstanceIndex;
}
