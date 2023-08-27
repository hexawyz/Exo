using System.Runtime.Serialization;

namespace Exo.Overlay.Contracts;

[DataContract]
public enum OverlayNotificationKind
{
	/// <summary>Indicates a notification that has no predefined meaning.</summary>
	[EnumMember]
	Custom = 0,

	/// <summary>Indicates that the caps lock was disabled.</summary>
	/// <remarks>This notification can optionally be device-specific, but always implies a system-wide change.</remarks>
	[EnumMember]
	CapsLockOff,

	/// <summary>Indicates that the caps lock was enabled.</summary>
	/// <remarks>This notification can optionally be device-specific, but always implies a system-wide change.</remarks>
	[EnumMember]
	CapsLockOn,

	/// <summary>Indicates that the num lock was disabled.</summary>
	/// <remarks>This notification can optionally be device-specific, but always implies a system-wide change.</remarks>
	[EnumMember]
	NumLockOff,

	/// <summary>Indicates that the num lock was enabled.</summary>
	/// <remarks>This notification can optionally be device-specific, but always implies a system-wide change.</remarks>
	[EnumMember]
	NumLockOn,

	/// <summary>Indicates that the scroll lock was disabled.</summary>
	/// <remarks>This notification can optionally be device-specific, but always implies a system-wide change.</remarks>
	[EnumMember]
	ScrollLockOff,

	/// <summary>Indicates that the scroll lock was enabled.</summary>
	/// <remarks>This notification can optionally be device-specific, but always implies a system-wide change.</remarks>
	[EnumMember]
	ScrollLockOn,

	/// <summary>Indicates that the fn lock has been enabled on a keyboard.</summary>
	/// <remarks>This notification is always device-specific.</remarks>
	[EnumMember]
	FnLockOff,

	/// <summary>Indicates that the fn lock has been disabled on a keyboard.</summary>
	/// <remarks>This notification is always device-specific.</remarks>
	[EnumMember]
	FnLockOn,

	/// <summary>Indicates that the brightness of a monitor was decreased.</summary>
	/// <remarks>This notification can be provided with an level and a scale indicating the approximate brightness level.</remarks>
	[EnumMember]
	MonitorBrightnessDown,

	/// <summary>Indicates that the brightness of a monitor was increased.</summary>
	/// <remarks>This notification can be provided with an level and a scale indicating the approximate brightness level.</remarks>
	[EnumMember]
	MonitorBrightnessUp,

	/// <summary>Indicates that the keyboard backlight brightness has been decreased.</summary>
	/// <remarks>This notification can be provided with an level and a scale indicating the approximate brightness level.</remarks>
	[EnumMember]
	KeyboardBacklightDown,

	/// <summary>Indicates that the keyboard backlight brightness has been increased.</summary>
	/// <remarks>This notification can be provided with an level and a scale indicating the approximate brightness level.</remarks>
	[EnumMember]
	KeyboardBacklightUp,

	/// <summary>Indicates that a battery has been connected to external power and will charge.</summary>
	/// <remarks>This status is similar to <see cref="BatteryDischarging"/>, but can display information without being provided the current level.</remarks>
	[EnumMember]
	BatteryExternalPowerDisconnected,

	/// <summary>Indicates that a battery has disconnected from external power and will discharge.</summary>
	/// <remarks>This status is similar to <see cref="BatteryCharging"/>, but can display information without being provided the current level.</remarks>
	[EnumMember]
	BatteryExternalPowerConnected,

	/// <summary>Indicates the current status of a discharging battery.</summary>
	/// <remarks>This notification requires to be provided with the battery level information, from <c>0</c> to <c>10</c>.</remarks>
	[EnumMember]
	BatteryDischarging,

	/// <summary>Indicates the current status of a charging battery.</summary>
	/// <remarks>This notification requires to be provided with the battery level information, from <c>0</c> to <c>10</c>.</remarks>
	[EnumMember]
	BatteryCharging,

	/// <summary>Indicates that a discharging battery is low on power.</summary>
	/// <remarks>This notification will assume a battery level of <c>1</c> on the <c>10</c> levels scale if the current level is not provided.</remarks>
	[EnumMember]
	BatteryLow,

	/// <summary>Indicates that a charging battery is completely charged.</summary>
	/// <remarks>This notification does not need to be provided the battery level and will assume <c>10</c> on the <c>10</c> levels scale.</remarks>
	[EnumMember]
	BatteryFullyCharged,

	/// <summary>Indicates an error with a battery.</summary>
	/// <remarks>This notification does not need to be provided the battery level.</remarks>
	[EnumMember]
	BatteryError,

	/// <summary>Indicates a DPI increase.</summary>
	[EnumMember]
	MouseDpiDown,

	/// <summary>Indicates a DPI decrease.</summary>
	[EnumMember]
	MouseDpiUp,
}
