using System.ComponentModel;
using System.Runtime.InteropServices;
using Exo.PowerManagement;
using Exo.Services;

namespace Exo.PowerNotifications;

public sealed partial class PowerNotificationEngine : IPowerNotificationService, IDisposable
{
	private IPowerNotificationSink[]? _powerNotificationSinks;
	private IPowerNotificationSink[]? _powerSettingAcDcPowerSourceSinks;
	private IPowerNotificationSink[]? _powerSettingBatteryPercentageRemainingSinks;
	private IPowerNotificationSink[]? _powerSettingConsoleDisplayStateSinks;
	private IPowerNotificationSink[]? _powerSettingSessionDisplayStatusSinks;
	private IPowerNotificationSink[]? _powerSettingGlobalUserPresenceSinks;
	private IPowerNotificationSink[]? _powerSettingSessionUserPresenceSinks;
	private IPowerNotificationSink[]? _powerSettingIdleBackgroundTaskSinks;
	private IPowerNotificationSink[]? _powerSettingLidSwitchStateChangeSinks;
	private IPowerNotificationSink[]? _powerSettingMonitorPowerOnSinks;
	private IPowerNotificationSink[]? _powerSettingPowerSavingStatusSinks;
	private IPowerNotificationSink[]? _powerSettingEnergySaverStatusSinks;
	private IPowerNotificationSink[]? _powerSettingPowerSchemePersonalitySinks;
	private IPowerNotificationSink[]? _powerSettingSystemAwayModeSinks;
	// NB: The Safe Resume handle should only be needed when we are not in the context of a service.
	private SafeSuspendResumeNotificationHandle? _suspendResumeNotificationHandle;
	private SafePowerSettingNotificationHandle? _powerSettingAcDcPowerSourceHandle;
	private SafePowerSettingNotificationHandle? _powerSettingBatteryPercentageRemainingHandle;
	private SafePowerSettingNotificationHandle? _powerSettingConsoleDisplayStateHandle;
	private SafePowerSettingNotificationHandle? _powerSettingSessionDisplayStatusHandle;
	private SafePowerSettingNotificationHandle? _powerSettingGlobalUserPresenceHandle;
	private SafePowerSettingNotificationHandle? _powerSettingSessionUserPresenceHandle;
	private SafePowerSettingNotificationHandle? _powerSettingIdleBackgroundTaskHandle;
	private SafePowerSettingNotificationHandle? _powerSettingLidSwitchStateChangeHandle;
	private SafePowerSettingNotificationHandle? _powerSettingMonitorPowerOnHandle;
	private SafePowerSettingNotificationHandle? _powerSettingPowerSavingStatusHandle;
	private SafePowerSettingNotificationHandle? _powerSettingEnergySaverStatusHandle;
	private SafePowerSettingNotificationHandle? _powerSettingPowerSchemePersonalityHandle;
	private SafePowerSettingNotificationHandle? _powerSettingSystemAwayModeHandle;
	private Lock? _stateLock;

	// These are needed when we need to register notifications, which should not occur that often, hopefully.
	private readonly IntPtr _targetHandle;
	private readonly bool _isServiceHandle;

	// Documentation says to return 1 for WM_POWERBROADCAST 🤷
	private int DefaultResult => _isServiceHandle ? 0 : 1;

	public static PowerNotificationEngine CreateForWindow(IntPtr handle)
		=> new(handle, false);

	public static PowerNotificationEngine CreateForService(IntPtr handle)
		=> new(handle, true);

	private PowerNotificationEngine(IntPtr targetHandle, bool isServiceHandle)
	{
		_stateLock = new();
		_targetHandle = targetHandle;
		_isServiceHandle = isServiceHandle;
	}

	public void Dispose()
	{
		// Using the lock reference as the "disposed" flag should be good enough to detect and avoid problem: if the lock is disposed, you can't register new things anymore.
		// (You could still momentarily successfully add a registration to something already registered, but it will be cleared up)
		if (Interlocked.Exchange(ref _stateLock, null) is { } @lock)
		{
			lock (@lock)
			{
				Dispose(ref _suspendResumeNotificationHandle);
				Dispose(ref _powerSettingAcDcPowerSourceHandle);
				Dispose(ref _powerSettingBatteryPercentageRemainingHandle);
				Dispose(ref _powerSettingConsoleDisplayStateHandle);
				Dispose(ref _powerSettingSessionDisplayStatusHandle);
				Dispose(ref _powerSettingGlobalUserPresenceHandle);
				Dispose(ref _powerSettingSessionUserPresenceHandle);
				Dispose(ref _powerSettingIdleBackgroundTaskHandle);
				Dispose(ref _powerSettingLidSwitchStateChangeHandle);
				Dispose(ref _powerSettingMonitorPowerOnHandle);
				Dispose(ref _powerSettingPowerSavingStatusHandle);
				Dispose(ref _powerSettingEnergySaverStatusHandle);
				Dispose(ref _powerSettingPowerSchemePersonalityHandle);
				Dispose(ref _powerSettingSystemAwayModeHandle);
			}
			Interlocked.Exchange(ref _powerNotificationSinks, null);
			Interlocked.Exchange(ref _powerSettingAcDcPowerSourceSinks, null);
			Interlocked.Exchange(ref _powerSettingBatteryPercentageRemainingSinks, null);
			Interlocked.Exchange(ref _powerSettingConsoleDisplayStateSinks, null);
			Interlocked.Exchange(ref _powerSettingSessionDisplayStatusSinks, null);
			Interlocked.Exchange(ref _powerSettingGlobalUserPresenceSinks, null);
			Interlocked.Exchange(ref _powerSettingSessionUserPresenceSinks, null);
			Interlocked.Exchange(ref _powerSettingIdleBackgroundTaskSinks, null);
			Interlocked.Exchange(ref _powerSettingLidSwitchStateChangeSinks, null);
			Interlocked.Exchange(ref _powerSettingMonitorPowerOnSinks, null);
			Interlocked.Exchange(ref _powerSettingPowerSavingStatusSinks, null);
			Interlocked.Exchange(ref _powerSettingEnergySaverStatusSinks, null);
			Interlocked.Exchange(ref _powerSettingPowerSchemePersonalitySinks, null);
			Interlocked.Exchange(ref _powerSettingSystemAwayModeSinks, null);
		}
	}

	private static void Dispose<THandle>(ref THandle? handle) where THandle : SafeHandle => Interlocked.Exchange(ref handle, null)?.Dispose();

	public IDisposable Register(IPowerNotificationSink sink, PowerSettings powerSettings)
	{
		ArgumentNullException.ThrowIfNull(sink);
		return new PowerNotificationRegistration(this, sink, powerSettings);
	}

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public int HandleNotification(int eventType, nint eventData)
	{
		return (PowerBroadcastStatus)eventType switch
		{
			PowerBroadcastStatus.Suspend => OnSuspend(),
			PowerBroadcastStatus.ResumeSuspend => OnResumeSuspend(),
			PowerBroadcastStatus.PowerStatusChange => OnPowerStatusChange(),
			PowerBroadcastStatus.ResumeAutomatic => OnResumeAutomatic(),
			PowerBroadcastStatus.PowerSettingChange => OnPowerSettingChange(eventData),
			_ => 0,
		};
	}

	private int OnSuspend()
	{
		if (Volatile.Read(ref _powerNotificationSinks) is { } sinks)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnSuspend();
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
		return DefaultResult;
	}

	private int OnResumeSuspend()
	{
		if (Volatile.Read(ref _powerNotificationSinks) is { } sinks)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnResumeSuspend();
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
		return DefaultResult;
	}

	private int OnPowerStatusChange()
	{
		if (Volatile.Read(ref _powerNotificationSinks) is { } sinks)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnPowerStatusChange();
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
		return DefaultResult;
	}

	private int OnResumeAutomatic()
	{
		if (Volatile.Read(ref _powerNotificationSinks) is { } sinks)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnResumeAutomatic();
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
		return DefaultResult;
	}

	private unsafe int OnPowerSettingChange(nint eventData)
	{
		Guid powerSettingGuid = ((PowerBroadcastSetting*)eventData)->PowerSetting;
		uint dataLength = ((PowerBroadcastSetting*)eventData)->DataLength;

		if (powerSettingGuid == PowerSettingGuids.AcDcPowerSource)
		{
			if (dataLength == 4)
			{
				OnPowerSourceChanged(_powerSettingAcDcPowerSourceSinks, (SystemPowerCondition)(*(int*)&((PowerBroadcastSetting*)eventData)->Data));
			}
		}
		else if (powerSettingGuid == PowerSettingGuids.BatteryPercentageRemaining)
		{
			if (dataLength == 4)
			{
				OnRemainingBatteryPercentageChanged(_powerSettingBatteryPercentageRemainingSinks, (byte)(*(int*)&((PowerBroadcastSetting*)eventData)->Data));
			}
		}
		else if (powerSettingGuid == PowerSettingGuids.ConsoleDisplayState)
		{
			if (dataLength == 4)
			{
				OnConsoleDisplayStatusChanged(_powerSettingConsoleDisplayStateSinks, (MonitorDisplayState)(*(int*)&((PowerBroadcastSetting*)eventData)->Data));
			}
		}
		else if (powerSettingGuid == PowerSettingGuids.SessionDisplayStatus)
		{
			if (dataLength == 4)
			{
				OnSessionDisplayStatusChanged(_powerSettingSessionDisplayStatusSinks, (MonitorDisplayState)(*(int*)&((PowerBroadcastSetting*)eventData)->Data));
			}
		}
		else if (powerSettingGuid == PowerSettingGuids.GlobalUserPresence)
		{
			if (dataLength == 4)
			{
				OnGlobalUserPresenceChanged(_powerSettingGlobalUserPresenceSinks, (UserActivityPresence)(*(int*)&((PowerBroadcastSetting*)eventData)->Data));
			}
		}
		else if (powerSettingGuid == PowerSettingGuids.SessionUserPresence)
		{
			if (dataLength == 4)
			{
				OnSessionUserPresenceChanged(_powerSettingSessionUserPresenceSinks, (UserActivityPresence)(*(int*)&((PowerBroadcastSetting*)eventData)->Data));
			}
		}
		else if (powerSettingGuid == PowerSettingGuids.IdleBackgroundTask)
		{
			OnIdleBackgroundTask(_powerSettingIdleBackgroundTaskSinks);
		}
		else if (powerSettingGuid == PowerSettingGuids.LidSwitchStateChange)
		{
			if (dataLength == 4)
			{
				OnLidSwitchStateChange(_powerSettingLidSwitchStateChangeSinks, *(int*)&((PowerBroadcastSetting*)eventData)->Data == 1);
			}
		}
		else if (powerSettingGuid == PowerSettingGuids.MonitorPowerOn)
		{
			if (dataLength == 4)
			{
				OnMonitorPowerOnChanged(_powerSettingMonitorPowerOnSinks, *(int*)&((PowerBroadcastSetting*)eventData)->Data == 1);
			}
		}
		else if (powerSettingGuid == PowerSettingGuids.PowerSavingStatus)
		{
			if (dataLength == 4)
			{
				OnPowerSavingStatusChanged(_powerSettingPowerSavingStatusSinks, *(int*)&((PowerBroadcastSetting*)eventData)->Data == 1);
			}
		}
		else if (powerSettingGuid == PowerSettingGuids.EnergySaverStatus)
		{
			if (dataLength == 4)
			{
				OnPowerSavingStatusChanged(_powerSettingEnergySaverStatusSinks, *(int*)&((PowerBroadcastSetting*)eventData)->Data == 1);
			}
		}
		else if (powerSettingGuid == PowerSettingGuids.PowerSchemePersonality)
		{
			if (dataLength == 16)
			{
				OnPowerSchemePersonalityChanged(_powerSettingPowerSchemePersonalitySinks, *(Guid*)&((PowerBroadcastSetting*)eventData)->Data);
			}
		}
		else if (powerSettingGuid == PowerSettingGuids.SystemAwayMode)
		{
			if (dataLength == 4)
			{
				OnAwayModeChanged(_powerSettingSystemAwayModeSinks, *(int*)&((PowerBroadcastSetting*)eventData)->Data == 1);
			}
		}
		return DefaultResult;
	}

	private static void OnPowerSourceChanged(IPowerNotificationSink[]? sinks, SystemPowerCondition powerSource)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnPowerSourceChanged(powerSource);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnRemainingBatteryPercentageChanged(IPowerNotificationSink[]? sinks, byte percentage)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnRemainingBatteryPercentageChanged(percentage);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnConsoleDisplayStatusChanged(IPowerNotificationSink[]? sinks, MonitorDisplayState displayState)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnConsoleDisplayStatusChanged(displayState);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnSessionDisplayStatusChanged(IPowerNotificationSink[]? sinks, MonitorDisplayState displayState)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnSessionDisplayStatusChanged(displayState);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnGlobalUserPresenceChanged(IPowerNotificationSink[]? sinks, UserActivityPresence presence)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnGlobalUserPresenceChanged(presence);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnSessionUserPresenceChanged(IPowerNotificationSink[]? sinks, UserActivityPresence presence)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnSessionUserPresenceChanged(presence);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnIdleBackgroundTask(IPowerNotificationSink[]? sinks)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnIdleBackgroundTask();
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnLidSwitchStateChange(IPowerNotificationSink[]? sinks, bool isOpened)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnLidSwitchStateChange(isOpened);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnMonitorPowerOnChanged(IPowerNotificationSink[]? sinks, bool isOn)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnMonitorPowerOnChanged(isOn);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnPowerSavingStatusChanged(IPowerNotificationSink[]? sinks, bool isOn)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnPowerSavingStatusChanged(isOn);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnEnergySaverStatusChanged(IPowerNotificationSink[]? sinks, EnergySaverStatus status)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnEnergySaverStatusChanged(status);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnPowerSchemePersonalityChanged(IPowerNotificationSink[]? sinks, Guid scheme)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnPowerSchemePersonalityChanged(scheme);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private static void OnAwayModeChanged(IPowerNotificationSink[]? sinks, bool isAway)
	{
		if (sinks is not null)
		{
			foreach (var sink in sinks)
			{
				try
				{
					sink.OnAwayModeChanged(isAway);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	private void RegisterSuspendResumeNotification(IPowerNotificationSink sink)
	{
		if (ArrayExtensions.InterlockedAdd(ref _powerNotificationSinks, sink) == 1)
		{
			if (_stateLock is not { } @lock) goto Disposed;
			if (!_isServiceHandle)
			{
				lock (@lock)
				{
					if (_stateLock is null) goto Disposed;
					if (_suspendResumeNotificationHandle is null)
					{
						Volatile.Write(ref _suspendResumeNotificationHandle, new(NativeMethods.RegisterSuspendResumeNotification(_targetHandle, NativeMethods.DeviceNotificationFlags.WindowHandle)));
					}
				}
			}
		}
		return;
	Disposed:;
		Volatile.Write(ref _powerNotificationSinks, null);
		throw new ObjectDisposedException(nameof(PowerNotificationEngine));
	}

	private void UnregisterSuspendResumeNotification(IPowerNotificationSink sink)
	{
		if (ArrayExtensions.InterlockedRemove(ref _powerNotificationSinks, sink) == 0)
		{
			if (_stateLock is { } @lock)
			{
				if (!_isServiceHandle)
				{
					lock (@lock)
					{
						if (Volatile.Read(ref _powerNotificationSinks) is not { Length: > 0 })
						{
							Interlocked.Exchange(ref _suspendResumeNotificationHandle, null)?.Dispose();
						}
					}
				}
			}
			else
			{
				Volatile.Write(ref _powerNotificationSinks, null);
				throw new ObjectDisposedException(nameof(PowerNotificationEngine));
			}
		}
	}

	private static SafePowerSettingNotificationHandle RegisterPowerSettingNotification(IntPtr handle, bool isServiceHandle, Guid powerSettingGuid)
	{
		var flags = isServiceHandle ? NativeMethods.DeviceNotificationFlags.ServiceHandle : NativeMethods.DeviceNotificationFlags.WindowHandle;

		var notificationHandle = NativeMethods.RegisterPowerSettingNotification(handle, powerSettingGuid, flags);

		if (notificationHandle == IntPtr.Zero)
		{
			throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error())!;
		}

		return new SafePowerSettingNotificationHandle(notificationHandle);
	}

	private void RegisterPowerSettingNotification(ref IPowerNotificationSink[]? sinks, ref SafePowerSettingNotificationHandle? handle, IPowerNotificationSink sink, Guid powerSettingGuid)
	{
		if (ArrayExtensions.InterlockedAdd(ref sinks, sink) == 1)
		{
			if (_stateLock is not { } @lock) goto Disposed;
			lock (@lock)
			{
				if (_stateLock is null) goto Disposed;
				if (handle is null)
				{
					Volatile.Write(ref handle, new(NativeMethods.RegisterPowerSettingNotification(_targetHandle, powerSettingGuid, _isServiceHandle ? NativeMethods.DeviceNotificationFlags.ServiceHandle : NativeMethods.DeviceNotificationFlags.WindowHandle)));
				}
			}
		}
		return;
	Disposed:;
		Volatile.Write(ref sinks, null);
		throw new ObjectDisposedException(nameof(PowerNotificationEngine));
	}

	private void UnregisterPowerSettingNotification(ref IPowerNotificationSink[]? sinks, ref SafePowerSettingNotificationHandle? handle, IPowerNotificationSink sink)
	{
		if (ArrayExtensions.InterlockedRemove(ref sinks, sink) == 0)
		{
			if (_stateLock is { } @lock)
			{
				lock (@lock)
				{
					if (Volatile.Read(ref sinks) is not { Length: > 0 })
					{
						Interlocked.Exchange(ref handle, null)?.Dispose();
					}
				}
			}
			else
			{
				Volatile.Write(ref sinks, null);
				throw new ObjectDisposedException(nameof(PowerNotificationEngine));
			}
		}
	}

	private sealed class PowerNotificationRegistration : IDisposable
	{
		private readonly PowerNotificationEngine _engine;
		private IPowerNotificationSink? _sink;
		private readonly PowerSettings _powerSettings;

		public PowerNotificationRegistration(PowerNotificationEngine engine, IPowerNotificationSink sink, PowerSettings powerSettings)
		{
			_engine = engine;
			_sink = sink;
			engine.RegisterSuspendResumeNotification(sink);
			if (powerSettings != PowerSettings.None)
			{
				if ((powerSettings & PowerSettings.AcDcPowerSource) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingAcDcPowerSourceSinks, ref engine._powerSettingAcDcPowerSourceHandle, sink, PowerSettingGuids.AcDcPowerSource);
				if ((powerSettings & PowerSettings.BatteryPercentageRemaining) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingBatteryPercentageRemainingSinks, ref engine._powerSettingBatteryPercentageRemainingHandle, sink, PowerSettingGuids.BatteryPercentageRemaining);
				if ((powerSettings & PowerSettings.ConsoleDisplayState) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingConsoleDisplayStateSinks, ref engine._powerSettingConsoleDisplayStateHandle, sink, PowerSettingGuids.ConsoleDisplayState);
				if ((powerSettings & PowerSettings.SessionDisplayStatus) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingSessionDisplayStatusSinks, ref engine._powerSettingSessionDisplayStatusHandle, sink, PowerSettingGuids.SessionDisplayStatus);
				if ((powerSettings & PowerSettings.GlobalUserPresence) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingGlobalUserPresenceSinks, ref engine._powerSettingGlobalUserPresenceHandle, sink, PowerSettingGuids.GlobalUserPresence);
				if ((powerSettings & PowerSettings.SessionUserPresence) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingSessionUserPresenceSinks, ref engine._powerSettingSessionUserPresenceHandle, sink, PowerSettingGuids.SessionUserPresence);
				if ((powerSettings & PowerSettings.IdleBackgroundTask) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingIdleBackgroundTaskSinks, ref engine._powerSettingIdleBackgroundTaskHandle, sink, PowerSettingGuids.IdleBackgroundTask);
				if ((powerSettings & PowerSettings.LidSwitchStateChange) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingLidSwitchStateChangeSinks, ref engine._powerSettingLidSwitchStateChangeHandle, sink, PowerSettingGuids.LidSwitchStateChange);
				if ((powerSettings & PowerSettings.MonitorPowerOn) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingMonitorPowerOnSinks, ref engine._powerSettingMonitorPowerOnHandle, sink, PowerSettingGuids.MonitorPowerOn);
				if ((powerSettings & PowerSettings.PowerSavingStatus) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingPowerSavingStatusSinks, ref engine._powerSettingPowerSavingStatusHandle, sink, PowerSettingGuids.PowerSavingStatus);
				if ((powerSettings & PowerSettings.EnergySaverStatus) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingEnergySaverStatusSinks, ref engine._powerSettingEnergySaverStatusHandle, sink, PowerSettingGuids.EnergySaverStatus);
				if ((powerSettings & PowerSettings.PowerSchemePersonality) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingPowerSchemePersonalitySinks, ref engine._powerSettingPowerSchemePersonalityHandle, sink, PowerSettingGuids.PowerSchemePersonality);
				if ((powerSettings & PowerSettings.SystemAwayMode) != 0) engine.RegisterPowerSettingNotification(ref engine._powerSettingSystemAwayModeSinks, ref engine._powerSettingSystemAwayModeHandle, sink, PowerSettingGuids.SystemAwayMode);
			}
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _sink, null) is { } sink)
			{
				var engine = _engine;
				engine.UnregisterSuspendResumeNotification(sink);
				if (_powerSettings is not PowerSettings.None and var powerSettings)
				{
					if ((powerSettings & PowerSettings.AcDcPowerSource) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingAcDcPowerSourceSinks, ref engine._powerSettingAcDcPowerSourceHandle, sink);
					if ((powerSettings & PowerSettings.BatteryPercentageRemaining) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingBatteryPercentageRemainingSinks, ref engine._powerSettingBatteryPercentageRemainingHandle, sink);
					if ((powerSettings & PowerSettings.ConsoleDisplayState) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingConsoleDisplayStateSinks, ref engine._powerSettingConsoleDisplayStateHandle, sink);
					if ((powerSettings & PowerSettings.SessionDisplayStatus) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingSessionDisplayStatusSinks, ref engine._powerSettingSessionDisplayStatusHandle, sink);
					if ((powerSettings & PowerSettings.GlobalUserPresence) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingGlobalUserPresenceSinks, ref engine._powerSettingGlobalUserPresenceHandle, sink);
					if ((powerSettings & PowerSettings.SessionUserPresence) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingSessionUserPresenceSinks, ref engine._powerSettingSessionUserPresenceHandle, sink);
					if ((powerSettings & PowerSettings.IdleBackgroundTask) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingIdleBackgroundTaskSinks, ref engine._powerSettingIdleBackgroundTaskHandle, sink);
					if ((powerSettings & PowerSettings.LidSwitchStateChange) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingLidSwitchStateChangeSinks, ref engine._powerSettingLidSwitchStateChangeHandle, sink);
					if ((powerSettings & PowerSettings.MonitorPowerOn) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingMonitorPowerOnSinks, ref engine._powerSettingMonitorPowerOnHandle, sink);
					if ((powerSettings & PowerSettings.PowerSavingStatus) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingPowerSavingStatusSinks, ref engine._powerSettingPowerSavingStatusHandle, sink);
					if ((powerSettings & PowerSettings.EnergySaverStatus) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingEnergySaverStatusSinks, ref engine._powerSettingEnergySaverStatusHandle, sink);
					if ((powerSettings & PowerSettings.PowerSchemePersonality) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingPowerSchemePersonalitySinks, ref engine._powerSettingPowerSchemePersonalityHandle, sink);
					if ((powerSettings & PowerSettings.SystemAwayMode) != 0) engine.UnregisterPowerSettingNotification(ref engine._powerSettingSystemAwayModeSinks, ref engine._powerSettingSystemAwayModeHandle, sink);
				}
			}
		}
	}
}

internal readonly struct PowerBroadcastSetting
{
	public readonly Guid PowerSetting;
	public readonly uint DataLength;
	public readonly byte Data;
}
