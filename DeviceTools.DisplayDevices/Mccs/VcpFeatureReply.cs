namespace DeviceTools.DisplayDevices.Mccs;

public readonly struct VcpFeatureReply
{
	public VcpFeatureReply(ushort currentValue, ushort maximumValue, bool isTemporary)
	{
		CurrentValue = currentValue;
		MaximumValue = maximumValue;
		IsTemporary = isTemporary;
	}

	public ushort CurrentValue { get; }
	public ushort MaximumValue { get; }
	public bool IsTemporary { get; }
}
