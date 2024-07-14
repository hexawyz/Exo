namespace DeviceTools.DisplayDevices.Mccs;

public readonly struct VcpFeatureReply
{
	public VcpFeatureReply(ushort currentValue, ushort maximumValue, bool isMomentary)
	{
		CurrentValue = currentValue;
		MaximumValue = maximumValue;
		IsMomentary = isMomentary;
	}

	public ushort CurrentValue { get; }
	public ushort MaximumValue { get; }
	public bool IsMomentary { get; }
}
