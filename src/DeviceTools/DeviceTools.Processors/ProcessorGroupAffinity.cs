namespace DeviceTools.Processors;

public readonly struct ProcessorGroupAffinity(ulong mask, ushort group)
{
	public ulong Mask { get; } = mask;
	public ushort Group { get; } = group;
}
