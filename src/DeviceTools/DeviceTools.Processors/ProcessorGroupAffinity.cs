namespace DeviceTools.Processors;

// This is synchronized with the native structure on 64-bit systems.
public readonly struct ProcessorGroupAffinity(ulong mask, ushort group)
{
	public ulong Mask { get; } = mask;
	public ushort Group { get; } = group;
}
