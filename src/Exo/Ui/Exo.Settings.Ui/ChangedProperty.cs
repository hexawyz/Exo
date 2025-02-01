using System.ComponentModel;

namespace Exo.Settings.Ui;

// This is a cache of PropertyChangedEventArgs values used to reduce allocations.
// While this is more useful for properties that are updated often, all properties can be registered in there with the only overhead of a permanent object instance.
internal static class ChangedProperty
{
	public static readonly PropertyChangedEventArgs RasterizationScale = new(nameof(RasterizationScale));
	public static readonly PropertyChangedEventArgs SelectedNavigationPage = new(nameof(SelectedNavigationPage));
	public static readonly PropertyChangedEventArgs CurrentPage = new(nameof(CurrentPage));
	public static readonly PropertyChangedEventArgs CanNavigateBack = new(nameof(CanNavigateBack));
	public static readonly PropertyChangedEventArgs Text = new(nameof(Text));
	public static readonly PropertyChangedEventArgs Color = new(nameof(Color));
	public static readonly PropertyChangedEventArgs ShowToolbar = new(nameof(ShowToolbar));
	public static readonly PropertyChangedEventArgs Value = new(nameof(Value));
	public static readonly PropertyChangedEventArgs InitialValue = new(nameof(InitialValue));
	public static readonly PropertyChangedEventArgs MinimumValue = new(nameof(MinimumValue));
	public static readonly PropertyChangedEventArgs MaximumValue = new(nameof(MaximumValue));
	public static readonly PropertyChangedEventArgs SupportedValues = new(nameof(SupportedValues));
	public static readonly PropertyChangedEventArgs IsChanged = new(nameof(IsChanged));
	public static readonly PropertyChangedEventArgs IsNotBusy = new(nameof(IsNotBusy));
	public static readonly PropertyChangedEventArgs IsReady = new(nameof(IsReady));
	public static readonly PropertyChangedEventArgs ConnectionStatus = new(nameof(ConnectionStatus));
	public static readonly PropertyChangedEventArgs Properties = new(nameof(Properties));
	public static readonly PropertyChangedEventArgs CurrentEffect = new(nameof(CurrentEffect));
	public static readonly PropertyChangedEventArgs UseUnifiedLighting = new(nameof(UseUnifiedLighting));
	public static readonly PropertyChangedEventArgs FriendlyName = new(nameof(FriendlyName));
	public static readonly PropertyChangedEventArgs Category = new(nameof(Category));
	public static readonly PropertyChangedEventArgs IsAvailable = new(nameof(IsAvailable));
	public static readonly PropertyChangedEventArgs IsExpanded = new(nameof(IsExpanded));
	public static readonly PropertyChangedEventArgs SerialNumber = new(nameof(SerialNumber));
	public static readonly PropertyChangedEventArgs BatteryState = new(nameof(BatteryState));
	public static readonly PropertyChangedEventArgs DeviceIds = new(nameof(DeviceIds));
	public static readonly PropertyChangedEventArgs Capabilities = new(nameof(Capabilities));
	public static readonly PropertyChangedEventArgs DataType = new(nameof(DataType));
	public static readonly PropertyChangedEventArgs Unit = new(nameof(Unit));
	public static readonly PropertyChangedEventArgs LiveDetails = new(nameof(LiveDetails));
	public static readonly PropertyChangedEventArgs CurrentValue = new(nameof(CurrentValue));
	public static readonly PropertyChangedEventArgs SpeedSensor = new(nameof(SpeedSensor));
	public static readonly PropertyChangedEventArgs CoolingModes = new(nameof(CoolingModes));
	public static readonly PropertyChangedEventArgs CurrentCoolingMode = new(nameof(CurrentCoolingMode));
	public static readonly PropertyChangedEventArgs InputSensor = new(nameof(InputSensor));
	public static readonly PropertyChangedEventArgs Points = new(nameof(Points));
	public static readonly PropertyChangedEventArgs Power = new(nameof(Power));
	public static readonly PropertyChangedEventArgs FallbackPower = new(nameof(FallbackPower));
	public static readonly PropertyChangedEventArgs Vertical = new(nameof(Vertical));
	public static readonly PropertyChangedEventArgs Horizontal = new(nameof(Horizontal));
	public static readonly PropertyChangedEventArgs VerticalInitialValue = new(nameof(VerticalInitialValue));
	public static readonly PropertyChangedEventArgs HorizontalInitialValue = new(nameof(HorizontalInitialValue));
	public static readonly PropertyChangedEventArgs IsIndependent = new(nameof(IsIndependent));
	public static readonly PropertyChangedEventArgs CurrentDpi = new (nameof(CurrentDpi));
	public static readonly PropertyChangedEventArgs MaximumDpi = new (nameof(MaximumDpi));
	public static readonly PropertyChangedEventArgs SelectedDpiPreset = new (nameof(SelectedDpiPreset));
	public static readonly PropertyChangedEventArgs SelectedDpiPresetIndex = new (nameof(SelectedDpiPresetIndex));
	public static readonly PropertyChangedEventArgs CanChangePollingFrequency = new (nameof(CanChangePollingFrequency));
	public static readonly PropertyChangedEventArgs SelectedPollingFrequency = new (nameof(SelectedPollingFrequency));
	public static readonly PropertyChangedEventArgs SupportedPollingFrequencies = new (nameof(SupportedPollingFrequencies));
	public static readonly PropertyChangedEventArgs IdleSleepDelay = new (nameof(IdleSleepDelay));
	public static readonly PropertyChangedEventArgs MinimumIdleSleepDelay = new (nameof(MinimumIdleSleepDelay));
	public static readonly PropertyChangedEventArgs MaximumIdleSleepDelay = new (nameof(MaximumIdleSleepDelay));
	public static readonly PropertyChangedEventArgs LowPowerModeBatteryThreshold = new (nameof(LowPowerModeBatteryThreshold));
	public static readonly PropertyChangedEventArgs WirelessBrightness = new (nameof(WirelessBrightness));
	public static readonly PropertyChangedEventArgs HasLowPowerBatteryThreshold = new (nameof(HasLowPowerBatteryThreshold));
	public static readonly PropertyChangedEventArgs HasIdleTimer = new (nameof(HasIdleTimer));
	public static readonly PropertyChangedEventArgs HasWirelessBrightness = new (nameof(HasWirelessBrightness));
	public static readonly PropertyChangedEventArgs LoadedImageName = new (nameof(LoadedImageName));
	public static readonly PropertyChangedEventArgs LoadedImageData = new (nameof(LoadedImageData));
	public static readonly PropertyChangedEventArgs Shape = new (nameof(Shape));
	public static readonly PropertyChangedEventArgs Image = new (nameof(Image));
	public static readonly PropertyChangedEventArgs ImageSize = new (nameof(ImageSize));
	public static readonly PropertyChangedEventArgs DisplayWidth = new (nameof(DisplayWidth));
	public static readonly PropertyChangedEventArgs DisplayHeight = new (nameof(DisplayHeight));
	public static readonly PropertyChangedEventArgs HasBuiltInGraphics = new (nameof(HasBuiltInGraphics));
	public static readonly PropertyChangedEventArgs CurrentGraphics = new (nameof(CurrentGraphics));
}
