namespace DeviceTools.HumanInterfaceDevices;

// As opposed to Raw
internal sealed class GenericHidDevice : HidDevice
{
	private int _isDisposed;

#if NET9_0_OR_GREATER
	public GenericHidDevice(string deviceName, Lock @lock)
#else
	public GenericHidDevice(string deviceName, object @lock)
#endif
	{
		DeviceName = deviceName;
		Lock = @lock;

		// Must be called last, as it depends on DeviceName
		DeviceId = TryResolveDeviceIdFromNames(out var deviceId) ?
			deviceId :
			DeviceId.Invalid;
	}

	public override string DeviceName { get; }
#if NET9_0_OR_GREATER
	private protected override Lock Lock { get; }
#else
	private protected override object Lock { get; }
#endif

	public override DeviceId DeviceId { get; }

	public override bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

	public override void Dispose()
	{
		if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 0)
		{
			base.Dispose();
		}
	}
}
