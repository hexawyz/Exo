using Exo.PowerManagement;

namespace Exo.Services;

public interface IPowerNotificationService
{
	/// <summary>Registers for receiving power notifications.</summary>
	/// <remarks>
	/// <para>The returned registration must be kept alive until it is not needed anymore, then disposed.</para>
	/// <para>
	/// Registrations are cumulative. As such, power notifications may be received multiple times if a single sink is registered more than once.
	/// For ease of use, the method allows to subscribe to multiple power settings at once.
	/// Registering for power setting notifications is entirely optional, and standard power notifications will still be received.
	/// </para>
	/// </remarks>
	/// <param name="sink">The sink that will receive all power notifications.</param>
	/// <param name="powerSettings">The power settings for which to register.</param>
	/// <returns></returns>
	IDisposable Register(IPowerNotificationSink sink, PowerSettings powerSettings);
}

/// <summary>A sink for power notifications.</summary>
/// <remarks>
/// <para>All notifications have a default no-op implementation so that consumers can focus on the notifications they are interested in.</para>
/// <para>All notifications will be sent from the main thread. Consumers should <b>absolutely</b> avoid executing long-running code from the calling thread.</para>
/// </remarks>
public interface IPowerNotificationSink
{
	/// <summary>Called when power status has changed.</summary>
	/// <remarks>Consumer needs to check the power status of the system themselves.</remarks>
	void OnPowerStatusChange() { }
	/// <summary>Called when operation is resuming automatically from a low-power state. Called every time the system resumes.</summary>
	void OnResumeAutomatic() { }
	/// <summary>Called when system is suspending operation.</summary>
	void OnSuspend() { }
	/// <summary>Called when operation is resuming from a low-power state. This is called after <see cref="OnApmResumeAutomatic"/> if the resume is triggered by user input, such as pressing a key.</summary>
	void OnResumeSuspend() { }
	/// <summary>Called when the current power source has changed.</summary>
	/// <param name="powerSource">The current power source.</param>
	void OnPowerSourceChanged(SystemPowerCondition powerSource) { }
	/// <summary>Called when the remaining battery percentage has changed.</summary>
	/// <param name="percentage">The current battery percentage.</param>
	void OnRemainingBatteryPercentageChanged(byte percentage) { }
	/// <summary>Called when the current monitor's display state has changed.</summary>
	/// <param name="displayState">The monitor display state.</param>
	void OnConsoleDisplayStatusChanged(MonitorDisplayState displayState) { }
	/// <summary>Called when the display associated with the application's session has been powered on or off.</summary>
	/// <param name="displayState">The monitor display state.</param>
	void OnSessionDisplayStatusChanged(MonitorDisplayState displayState) { }
	/// <summary>Called when the user status associated with any session has changed.</summary>
	/// <remarks>This is sent only for non-interactive mode applications or services.</remarks>
	void OnGlobalUserPresenceChanged(UserActivityPresence presence) { }
	/// <summary>Called when the user status associated with the application's session has changed.</summary>
	void OnSessionUserPresenceChanged(UserActivityPresence presence) { }
	/// <summary>Called when the system is busy and the time is ideal to proceed to work that would prevent entering an idle state.</summary>
	void OnIdleBackgroundTask() { }
	/// <summary>Called when the state of the lid has changed, if there is a lid whose state is known.</summary>
	void OnLidSwitchStateChange(bool isOpened) { }
	/// <summary>Called when the primary system monitor has been powered on or off.</summary>
	void OnMonitorPowerOnChanged(bool isOn) { }
	/// <summary>Called when battery power saver has been turned on or off in response to changing power conditions.</summary>
	void OnPowerSavingStatusChanged(bool isOn) { }
	/// <summary>Called when energy saver status has changed.</summary>
	void OnEnergySaverStatusChanged(EnergySaverStatus status) { }
	/// <summary>Called when the active power scheme personality has changed.</summary>
	void OnPowerSchemePersonalityChanged(Guid scheme) { }
	/// <summary>Called when the system is entering or exiting away-mode.</summary>
	void OnAwayModeChanged(bool isAway) { }
}
