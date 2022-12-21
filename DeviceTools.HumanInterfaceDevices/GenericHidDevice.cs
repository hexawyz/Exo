using System.Threading;

namespace DeviceTools.HumanInterfaceDevices
{
	// As opposed to Raw
	internal sealed class GenericHidDevice : HidDevice
	{
		private int _isDisposed;

		public GenericHidDevice(string deviceName, object @lock)
		{
			DeviceName = deviceName;
			if (DeviceNameParser.TryParseDeviceName(deviceName, out var ids))
			{
				(VendorId, ProductId) = (ids.VendorId, ids.ProductId);
			}
			else
			{
				(VendorId, ProductId) = (0xFFFF, 0xFFFF);
			}
			Lock = @lock;
		}

		public override string DeviceName { get; }
		private protected override object Lock { get; }

		public override ushort VendorId { get; }
		public override ushort ProductId { get; }

		public override bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

		public override void Dispose()
		{
			if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 0)
			{
				base.Dispose();
			}
		}
	}
}
