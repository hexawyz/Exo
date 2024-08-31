namespace DeviceTools;

public sealed class DeviceObjectInformation
{
	internal static readonly DevicePropertyDictionary EmptyProperties = new([]);

	public DeviceObjectInformation(DeviceObjectKind kind, string id, Dictionary<PropertyKey, object?>? properties)
		: this(kind, id, properties is not null ? new DevicePropertyDictionary(properties) : EmptyProperties)
	{
	}

	public DeviceObjectInformation(DeviceObjectKind kind, string id, DevicePropertyDictionary properties)
	{
		Kind = kind;
		Id = id;
		Properties = properties;
	}

	public DeviceObjectKind Kind { get; }

	public string Id { get; }

	public DevicePropertyDictionary Properties { get; }
}
