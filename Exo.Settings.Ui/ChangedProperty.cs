using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Exo.Settings.Ui;

internal static class ChangedProperty
{
	public static readonly PropertyChangedEventArgs Color = new(nameof(Color));
	public static readonly PropertyChangedEventArgs Value = new(nameof(Value));
	public static readonly PropertyChangedEventArgs InitialValue = new(nameof(InitialValue));
	public static readonly PropertyChangedEventArgs IsChanged = new(nameof(IsChanged));
	public static readonly PropertyChangedEventArgs IsNotBusy = new(nameof(IsNotBusy));
	public static readonly PropertyChangedEventArgs Properties = new(nameof(Properties));
	public static readonly PropertyChangedEventArgs CurrentEffect = new(nameof(CurrentEffect));
	public static readonly PropertyChangedEventArgs BatteryState = new(nameof(BatteryState));
}
