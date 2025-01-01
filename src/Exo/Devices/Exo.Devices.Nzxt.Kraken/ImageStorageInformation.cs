namespace Exo.Devices.Nzxt.Kraken;

internal readonly struct ImageStorageInformation
{
	public readonly byte ImageIndex;
	public readonly byte OtherImageIndex;
	public readonly byte Unknown;
	public readonly ushort MemoryBlockIndex;
	public readonly ushort MemoryBlockCount;

	public ImageStorageInformation(byte imageIndex, byte otherImageIndex, byte unknown, ushort memoryBlockIndex, ushort memoryBlockCount)
	{
		ImageIndex = imageIndex;
		OtherImageIndex = otherImageIndex;
		Unknown = unknown;
		MemoryBlockIndex = memoryBlockIndex;
		MemoryBlockCount = memoryBlockCount;
	}
}
