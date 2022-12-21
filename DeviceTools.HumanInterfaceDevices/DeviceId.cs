namespace DeviceTools.HumanInterfaceDevices
{
	internal readonly struct DeviceId
	{
		public static readonly DeviceId Invalid = new DeviceId(0xFFFF, 0xFFFF);

		public ushort VendorId { get; }
		public ushort ProductId { get; }

		public DeviceId(ushort vendorId, ushort productId)
		{
			VendorId = vendorId;
			ProductId = productId;
		}
	}
}
