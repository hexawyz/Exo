using System.Threading.Channels;
using Exo.Features;
using Exo.Programming;
using Exo.Programming.Annotations;
using Exo.Service.Events;

namespace Exo.Service;

[Module("Battery")]
[TypeId(0x645A580F, 0xECE8, 0x44DA, 0x80, 0x71, 0xCE, 0xA8, 0xDF, 0xE3, 0x69, 0xDF)]
[Event<BatteryEventParameters>("DeviceConnected", 0x51BCE224, 0x0DA2, 0x4965, 0xB5, 0xBD, 0xAD, 0x71, 0x28, 0xD6, 0xA4, 0xE4)]
[Event<BatteryEventParameters>("ExternalPowerConnected", 0xFA10C2ED, 0x2842, 0x4AE2, 0x8F, 0x3F, 0x18, 0xCC, 0x1C, 0x05, 0x16, 0x75)]
[Event<BatteryEventParameters>("ExternalPowerDisconnected", 0xF8E9D6E6, 0xA21B, 0x45EC, 0x8E, 0xF4, 0xE5, 0x3A, 0x5A, 0x54, 0xEA, 0xF7)]
[Event<BatteryEventParameters>("ChargingComplete", 0x2B75EB8F, 0x8393, 0x43A4, 0xB4, 0xA4, 0x58, 0x35, 0x90, 0x4A, 0x84, 0xCF)]
[Event<BatteryEventParameters>("Error", 0x1D4EE59D, 0x3FE0, 0x45BC, 0x8F, 0xEB, 0x82, 0xE2, 0x45, 0x89, 0x32, 0x1B)]
[Event<BatteryEventParameters>("Discharging", 0x889E49AD, 0x2D35, 0x4D8A, 0xBE, 0x0E, 0xAD, 0x2A, 0x21, 0xB7, 0xF1, 0xB8)]
[Event<BatteryEventParameters>("Charging", 0x19687F99, 0x6A9B, 0x41FA, 0xAC, 0x91, 0xDF, 0xDA, 0x0A, 0xD7, 0xF7, 0xD3)]
public sealed class BatteryService : IAsyncDisposable
{
	public static readonly Guid BatteryDeviceConnectedEventGuid = new(0x51BCE224, 0x0DA2, 0x4965, 0xB5, 0xBD, 0xAD, 0x71, 0x28, 0xD6, 0xA4, 0xE4);
	public static readonly Guid BatteryExternalPowerConnectedEventGuid = new(0xFA10C2ED, 0x2842, 0x4AE2, 0x8F, 0x3F, 0x18, 0xCC, 0x1C, 0x05, 0x16, 0x75);
	public static readonly Guid BatteryExternalPowerDisconnectedEventGuid = new(0xF8E9D6E6, 0xA21B, 0x45EC, 0x8E, 0xF4, 0xE5, 0x3A, 0x5A, 0x54, 0xEA, 0xF7);
	public static readonly Guid BatteryChargingCompleteEventGuid = new(0x2B75EB8F, 0x8393, 0x43A4, 0xB4, 0xA4, 0x58, 0x35, 0x90, 0x4A, 0x84, 0xCF);
	public static readonly Guid BatteryErrorEventGuid = new(0x1D4EE59D, 0x3FE0, 0x45BC, 0x8F, 0xEB, 0x82, 0xE2, 0x45, 0x89, 0x32, 0x1B);
	public static readonly Guid BatteryChargingEventGuid = new(0x19687F99, 0x6A9B, 0x41FA, 0xAC, 0x91, 0xDF, 0xDA, 0x0A, 0xD7, 0xF7, 0xD3);
	public static readonly Guid BatteryDischargingEventGuid = new(0x889E49AD, 0x2D35, 0x4D8A, 0xBE, 0x0E, 0xAD, 0x2A, 0x21, 0xB7, 0xF1, 0xB8);

	private readonly BatteryWatcher _batteryWatcher;
	private readonly ChannelWriter<Event> _eventWriter;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;

	public BatteryService(BatteryWatcher batteryWatcher, ChannelWriter<Event> eventWriter)
	{
		_batteryWatcher = batteryWatcher;
		_eventWriter = eventWriter;
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (_watchTask.IsCompleted) return;

		_cancellationTokenSource.Cancel();
		await _watchTask.ConfigureAwait(false);
		_cancellationTokenSource.Dispose();
	}

	// In this part of the code, we map device arrivals and status updates to sensible events in a way that hopefully leaves enough information for the handlers to make useful decisions.
	// This means that some of the filtering logic on what to display has to be done on the event handler side.
	// Usages that we want to guarantee here is that of notifications and also an accurate display of the battery level is the user wishes to display it somewhere.
	// Generally, we want to avoid flooding the user with too many notifications, so for those, the handlers will have to compare battery levels and act upon this.
	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _batteryWatcher.WatchAsync(cancellationToken))
			{
				switch (notification.NotificationKind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Addition:
					_eventWriter.TryWrite
					(
						Event.Create
						(
							BatteryDeviceConnectedEventGuid,
							new BatteryEventParameters
							(
								(DeviceId)notification.Key,
								notification.NewValue.Level,
								notification.OldValue.Level,
								notification.NewValue.BatteryStatus,
								notification.NewValue.ExternalPowerStatus
							)
						)
					);
					break;
				case WatchNotificationKind.Update:
					// Detects if the external power status connection state has changed.
					if (((notification.NewValue.ExternalPowerStatus ^ notification.OldValue.ExternalPowerStatus) & ExternalPowerStatus.IsConnected) != 0)
					{
						var eventGuid = (notification.NewValue.ExternalPowerStatus & ExternalPowerStatus.IsConnected) != 0 ?
							BatteryExternalPowerConnectedEventGuid :
							BatteryExternalPowerDisconnectedEventGuid;
						_eventWriter.TryWrite
						(
							Event.Create
							(
								eventGuid,
								new BatteryEventParameters
								(
									(DeviceId)notification.Key,
									notification.NewValue.Level,
									notification.OldValue.Level,
									notification.NewValue.BatteryStatus,
									notification.NewValue.ExternalPowerStatus
								)
							)
						);
					}
					else if (notification.NewValue.BatteryStatus != notification.OldValue.BatteryStatus)
					{
						switch (notification.NewValue.BatteryStatus)
						{
						case BatteryStatus.ChargingComplete:
							_eventWriter.TryWrite
							(
								Event.Create
								(
									BatteryChargingCompleteEventGuid,
									new BatteryEventParameters
									(
										(DeviceId)notification.Key,
										1f,
										notification.OldValue.Level,
										notification.NewValue.BatteryStatus,
										notification.NewValue.ExternalPowerStatus
									)
								)
							);
							break;
						case BatteryStatus.Error:
						case BatteryStatus.TooHot:
						case BatteryStatus.Missing:
						case BatteryStatus.Invalid:
							_eventWriter.TryWrite
							(
								Event.Create
								(
									BatteryErrorEventGuid,
									new BatteryEventParameters
									(
										(DeviceId)notification.Key,
										notification.NewValue.Level,
										notification.OldValue.Level,
										notification.NewValue.BatteryStatus,
										notification.NewValue.ExternalPowerStatus
									)
								)
							);
							break;
						}
					}
					else if (notification.NewValue.BatteryStatus == BatteryStatus.Charging)
					{
						_eventWriter.TryWrite
						(
							Event.Create
							(
								BatteryChargingEventGuid,
								new BatteryEventParameters
								(
									(DeviceId)notification.Key,
									notification.NewValue.Level,
									notification.OldValue.Level,
									notification.NewValue.BatteryStatus,
									notification.NewValue.ExternalPowerStatus
								)
							)
						);
					}
					else if (notification.NewValue.BatteryStatus == BatteryStatus.Discharging)
					{
						_eventWriter.TryWrite
						(
							Event.Create
							(
								BatteryDischargingEventGuid,
								new BatteryEventParameters
								(
									(DeviceId)notification.Key,
									notification.NewValue.Level,
									notification.OldValue.Level,
									notification.NewValue.BatteryStatus,
									notification.NewValue.ExternalPowerStatus
								)
							)
						);
					}
					break;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	public IAsyncEnumerable<ChangeWatchNotification<Guid, BatteryState>> WatchChangesAsync(CancellationToken cancellationToken)
		=> _batteryWatcher.WatchAsync(cancellationToken);
}
