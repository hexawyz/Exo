namespace DeviceTools
{
	public sealed class DeviceObjectInformation
	{
		public DeviceObjectInformation(DeviceObjectKind kind, string id)
		{
			Kind = kind;
			Id = id;
		}

		public DeviceObjectKind Kind { get; }

		public string Id { get; }
	}
}
