using System.Collections.Immutable;

namespace Exo.Features.UserInput;

/// <summary>Exposes the remappable buttons on the device.</summary>
public interface IRemappableButtonsFeature : IUserInputDeviceFeature
{
	ImmutableArray<RemappableButtonDefinition> Buttons { get; }
}

/// <summary>On devices supporting button interception, this provides button events for intercepted buttons.</summary>
public interface IInterceptedButtonsFeature : IUserInputDeviceFeature
{
	event ButtonEventHandler ButtonDown;
	event ButtonEventHandler ButtonUp;
}

public delegate void ButtonEventHandler(Driver driver, ushort button);

public readonly struct RemappableButtonDefinition
{
	/// <summary>The button ID.</summary>
	/// <remarks>
	/// <para>
	/// When possible, well-know IDs from <see cref="WellKnownInputButton"/> should be used.
	/// This will allow UIs to have more sensible defaults for displaying informations on screen.
	/// </para>
	/// <para>
	/// When it is necessary to define custom buttons, IDs above <c>0x1000</c> should be used.
	/// This allows preserving space for up to 4096 common button that will be able to use a shared definition.
	/// </para>
	/// </remarks>
	public ushort ButtonId { get; }

	/// <summary>The capabilities of the button.</summary>
	public ButtonCapabilities Capabilities { get; }

	/// <summary>If this button has a customizable screen, this is the embedded monitor ID associated with it through <see cref="EmbeddedMonitors.IEmbeddedMonitorControllerFeature"/>.</summary>
	public Guid EmbeddedMonitorId { get; }
}

/// <summary>Defines input keys.</summary>
/// <remarks>
/// This enum contains well-known, shared IDs for buttons.
/// Values starting at <c>0x1000</c> are to be used for custom, device-specific buttons.
/// </remarks>
public enum WellKnownInputButton : ushort
{
	// Most common mouse buttons. Those buttons all have a relatively well defined behavior on Windows a least.
	LeftMouseButton = 1,
	RightMouseButton,
	MiddleMouseButton,
	MouseButton4,
	MouseButton5,

	// Modifier Keys
	LeftShift,
	LeftControl,
	LeftAlt,
	LeftWindows,
	RightShift,
	RightControl,
	RightAlt,
	RightWindows,

	// Context Menu Key
	Application,

	// Standard Lock Keys
	ScrollLock,
	CapsLock,
	NuLock,

	Return,
	Escape,
	Delete,
	Tab,
	Spacebar,

	PrintScreen,
	Pause,
	Insert,
	Home,
	End,
	PageUp,
	PageDown,
	DeleteForward,
	RightArrow,
	LeftArrow,
	DownArrow,
	UpArrow,

	// Function keys. HID lists up to 24 Function Keys, so we will stop at there for now.
	// Most keyboards will stop at F12. Apple Keyboards usually go up to F19.
	// Programs generally don't have any shortcut associated by default after F12.
	Function1,
	Function2,
	Function3,
	Function4,
	Function5,
	Function6,
	Function7,
	Function8,
	Function9,
	Function10,
	Function11,
	Function12,
	Function13,
	Function14,
	Function15,
	Function16,
	Function17,
	Function18,
	Function19,
	Function20,
	Function21,
	Function22,
	Function23,
	Function24,

	// Standard alphabet keys.
	A,
	B,
	C,
	D,
	E,
	F,
	G,
	H,
	I,
	J,
	K,
	L,
	M,
	N,
	O,
	P,
	Q,
	R,
	S,
	T,
	U,
	V,
	W,
	X,
	Y,
	Z,

	// Number keys
	Number0,
	Number1,
	Number2,
	Number3,
	Number4,
	Number5,
	Number6,
	Number7,
	Number8,
	Number9,

	// Equivalent to Windows VK_OEM_ codes. With addition of the HID-defined alternative definition for OEM 7.
	Oem1,
	Oem2,
	Oem3,
	Oem4,
	Oem5,
	Oem6,
	Oem7,
	Oem7NonUs,
	Oem8,
	Oem102,

	// Numpad Keys
	Numpad0,
	Numpad1,
	Numpad2,
	Numpad3,
	Numpad4,
	Numpad5,
	Numpad6,
	Numpad7,
	Numpad8,
	Numpad9,
	NumpadDecimalSeparator,
	NumpadAdd,
	NumpadSubtract,
	NumpadMultiply,
	NumpadDivide,
	NumpadEqual,
	NumpadDelete,
	NumpadEnter,

	// Extra mouse buttons up to 20.
	// As far as I know, it is unlikely for a mouse to have more than 18 buttons. Hopefully, 20 is large enough of a limit.
	MouseButton6,
	MouseButton7,
	MouseButton8,
	MouseButton9,
	MouseButton10,
	MouseButton11,
	MouseButton12,
	MouseButton13,
	MouseButton14,
	MouseButton15,
	MouseButton16,
	MouseButton17,
	MouseButton18,
	MouseButton19,
	MouseButton20,
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
	HasDefaultFunction = 0b_00000000_00000001,
	IsInterceptable = 0b_00000000_00000010,
	HasMonitor = 0b_00000000_00000100,
}
