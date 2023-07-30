using System.Threading;

namespace DeviceTools.HumanInterfaceDevices;

// As opposed to Raw
internal sealed class GenericHidDevice : HidDevice
{
	private int _isDisposed;

	public GenericHidDevice(string deviceName, object @lock)
	{
		DeviceName = deviceName;
		Lock = @lock;

		// Must be called last, as it depends on DeviceName
		DeviceId = TryResolveDeviceIdFromNames(out var deviceId) ?
			deviceId :
			DeviceId.Invalid;
	}

	public override string DeviceName { get; }
	private protected override object Lock { get; }

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
