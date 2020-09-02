using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AnyLayout.RawInput
{
	// As opposed to Raw
	internal sealed class GenericHidDevice : HidDevice
	{
		private int _isDisposed;

		public GenericHidDevice(string deviceName, object @lock)
		{
			DeviceName = deviceName;
			(VendorId, ProductId) = DeviceNameParser.ParseDeviceName(deviceName);
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
