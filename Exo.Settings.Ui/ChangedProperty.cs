using System.ComponentModel;

namespace Exo.Settings.Ui;

// This is a cache of PropertyChangedEventArgs values used to reduce allocations.
// While this is more useful for properties that are updated often, all properties can be registered in there with the only overhead of a permanent object instance.
internal static class ChangedProperty
{
	public static readonly PropertyChangedEventArgs Color = new(nameof(Color));
	public static readonly PropertyChangedEventArgs ShowToolbar = new(nameof(ShowToolbar));
	public static readonly PropertyChangedEventArgs Value = new(nameof(Value));
	public static readonly PropertyChangedEventArgs InitialValue = new(nameof(InitialValue));
	public static readonly PropertyChangedEventArgs MinimumValue = new(nameof(MinimumValue));
	public static readonly PropertyChangedEventArgs MaximumValue = new(nameof(MaximumValue));
	public static readonly PropertyChangedEventArgs IsChanged = new(nameof(IsChanged));
	public static readonly PropertyChangedEventArgs IsNotBusy = new(nameof(IsNotBusy));
	public static readonly PropertyChangedEventArgs IsReady = new(nameof(IsReady));
	public static readonly PropertyChangedEventArgs Properties = new(nameof(Properties));
	public static readonly PropertyChangedEventArgs CurrentEffect = new(nameof(CurrentEffect));
	public static readonly PropertyChangedEventArgs FriendlyName = new(nameof(FriendlyName));
	public static readonly PropertyChangedEventArgs Category = new(nameof(Category));
	public static readonly PropertyChangedEventArgs IsAvailable = new(nameof(IsAvailable));
	public static readonly PropertyChangedEventArgs SerialNumber = new(nameof(SerialNumber));
	public static readonly PropertyChangedEventArgs BatteryState = new(nameof(BatteryState));
	public static readonly PropertyChangedEventArgs DeviceIds = new(nameof(DeviceIds));
}
