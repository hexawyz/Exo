namespace Exo.Discovery;

/// <summary>This manages the lifetime of a DNS-SD device.</summary>
/// <remarks>
/// <para>
/// Because the service discovery itself has no means to detect when a device gets offline, instances of this type <b>must</b> be used by drivers to notify when the device is offline.
/// </para>
/// <para>When the discovery subsystem is notified that the device is offline, the driver will be unregistered and disposed with the regular process.</para>
/// <para>
/// It is possible that a device will become offline then online without a driver noticing.
/// When the device is connected, the <see cref="DeviceUpdated"/> event will periodically be raised.
/// That event being raised in itself does not imply any change on the device, but it is an opportunity for drivers to check on the state of the device if necessary.
/// </para>
/// </remarks>
public sealed class DnsSdDeviceLifetime : IDisposable, IAsyncDisposable
{
	private readonly DnsSdDiscoverySubsystem _subsystem;
	private readonly string _instanceId;
	private bool _isNotified;

	internal string InstanceId => _instanceId;

	/// <summary>This event will be used to notify of a state update.</summary>
	/// <remarks>Drivers can rely on this event to update the state of the device if it requires polling.</remarks>
	public event EventHandler? DeviceUpdated;

	internal DnsSdDeviceLifetime(DnsSdDiscoverySubsystem subsystem, string instanceId)
	{
		_subsystem = subsystem;
		_instanceId = instanceId;
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	public void Dispose()
	{
		if (!Interlocked.Exchange(ref _isNotified, true))
		{
			_subsystem.ReleaseLifetime(this);
		}
	}

	internal void MarkDisposed() => Volatile.Write(ref _isNotified, true);

	public void NotifyDeviceOffline()
	{
		if (!Interlocked.Exchange(ref _isNotified, true))
		{
			_subsystem.OnRemoval(this);
		}
	}

	internal void NotifyDeviceUpdated() => DeviceUpdated?.Invoke(this, EventArgs.Empty);
}
