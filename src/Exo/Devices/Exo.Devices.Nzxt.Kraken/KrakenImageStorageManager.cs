using System.Diagnostics;

namespace Exo.Devices.Nzxt.Kraken;

internal sealed class KrakenImageStorageManager : IAsyncDisposable
{
	[DebuggerDisplay("Image #{ImageIndex}, Index={Index}, Count={Count}")]
	private readonly struct MemoryRegionInfo
	{
		public readonly byte ImageIndex;
		// TODO: Implement LRU logic
		//public readonly byte Version;
		public readonly ushort Index;
		public readonly ushort Count;

		public MemoryRegionInfo(byte imageIndex, ushort index, ushort count)
		{
			ImageIndex = imageIndex;
			Index = index;
			Count = count;
		}
	}

	public static async Task<KrakenImageStorageManager> CreateAsync
	(
		byte imageCount,
		ushort memoryBlockCount,
		KrakenHidTransport hidTransport,
		KrakenWinUsbImageTransport imageTransport,
		CancellationToken cancellationToken
	)
	{
		// This is the command that CAM is sending when starting up. It would be some kind of reset for the image upload state in case of a previous crash or whatever.
		await hidTransport.CancelImageUploadAsync(cancellationToken).ConfigureAwait(false);

		var memoryRegions = new MemoryRegionInfo[imageCount];
		int count = 0;
		for (int i = 0; i < imageCount; i++)
		{
			var info = await hidTransport.GetImageStorageInformationAsync((byte)i, cancellationToken).ConfigureAwait(false);
			if (info.MemoryBlockCount > 0)
			{
				memoryRegions[count++] = new((byte)i, info.MemoryBlockIndex, info.MemoryBlockCount);
			}
		}
		memoryRegions.AsSpan(0, count).Sort((x, y) => Comparer<ushort>.Default.Compare(x.Index, y.Index));
		ref MemoryRegionInfo prev = ref memoryRegions[0];
		for (int i = 1; i < count; i++)
		{
			ref MemoryRegionInfo current = ref memoryRegions[i];
			if (prev.Index == current.Index || (uint)prev.Index + prev.Count > current.Index)
			{
				// TODO: Log.
				// The device has overlapping memory ranges so to simplify everything, we will fix the problem by forcefully switch to the built-in Liquid temperature view and clear all images.
				await ResetDeviceAsync(hidTransport, memoryRegions, cancellationToken).ConfigureAwait(false);
				memoryRegions.AsSpan().Clear();
				count = 0;
				break;
			}
			prev = ref current;
		}
		return new(hidTransport, imageTransport, memoryRegions, (ushort)count, memoryBlockCount);
	}

	private static async ValueTask ResetDeviceAsync(KrakenHidTransport hidTransport, Memory<MemoryRegionInfo> memoryRegions, CancellationToken cancellationToken)
	{
		await hidTransport.DisplayPresetVisualAsync(KrakenPresetVisual.LiquidTemperature, cancellationToken).ConfigureAwait(false);
		for (int i = 0; i < memoryRegions.Length; i++)
		{
			await hidTransport.ClearImageStorageAsync((byte)memoryRegions.Span[i].ImageIndex, cancellationToken).ConfigureAwait(false);
		}
	}

	private readonly KrakenHidTransport _hidTransport;
	private readonly KrakenWinUsbImageTransport _imageTransport;
	private readonly AsyncLock _lock;
	// The memory is managed by keeping a memory ordered list of regions that are allocated and to which image slot.
	// This gives us immediate information on which blocks are free and allows for relatively easily search of memory storage locations.
	// ONly downside in this case is having to parse the entire array to find info about a memory slot. The array, however, is pretty small.
	private readonly MemoryRegionInfo[] _allocatedRegions;
	private ushort _allocatedRegionCount;
	private readonly ushort _memoryBlockCount;

	private KrakenImageStorageManager
	(
		KrakenHidTransport hidTransport,
		KrakenWinUsbImageTransport imageTransport,
		MemoryRegionInfo[] allocatedRegions,
		ushort allocatedRegionCount,
		ushort memoryBlockCount
	)
	{
		_hidTransport = hidTransport;
		_imageTransport = imageTransport;
		_lock = new();
		_allocatedRegions = allocatedRegions;
		_allocatedRegionCount = allocatedRegionCount;
		_memoryBlockCount = memoryBlockCount;
	}

	public async ValueTask DisposeAsync()
	{
		await _hidTransport.DisposeAsync().ConfigureAwait(false);
		await _imageTransport.DisposeAsync().ConfigureAwait(false);
	}

	// There are multiple ways of allocating memory.
	// Let's try this one for now:
	// 1 - Find the smallest block of free memory at least big enough to fit the requested size.
	// 2 - If the memory block is adjacent to other allocated regions, try to anchor to the largest packed chunk of regions.
	// 3 - In doubt, try to anchor the furthest away from absolute middle memory address. (This would implicitly allow packing regions to the beginning or end of the address space)
	private bool TryFindFreeRegion(ushort count, out ushort index)
	{
		var allocatedRegions = _allocatedRegions;
		nint allocatedRegionCount = _allocatedRegionCount;
		if (allocatedRegionCount == 0) goto NotFound;
		nint foundIndex = -1;
		nuint foundCount = nuint.MaxValue;
		nuint currentIndex = 0;
		nuint currentCount;
		// Track the contiguous blocks of memory following the chosen region.
		nuint previousChunkLength = 0;
		nuint nextChunkLength = 0;
		nuint currentChunkLength = 0;

		for (int i = 0; i < allocatedRegionCount; i++)
		{
			ref var region = ref allocatedRegions[i];
			currentCount = region.Index - currentIndex;
			if (currentCount == 0)
			{
				currentChunkLength += region.Count;
			}
			else
			{
				if (currentCount >= count && currentCount < foundCount)
				{
					foundIndex = (nint)currentIndex;
					foundCount = currentCount;
					previousChunkLength = currentChunkLength;
					nextChunkLength = 0;
				}
				else if (nextChunkLength == 0)
				{
					nextChunkLength = currentChunkLength;
				}
				currentChunkLength = 0;
			}
			currentIndex = (nuint)region.Index + region.Count;
		}

		// Handle the last possible memory region.
		currentCount = _memoryBlockCount - currentIndex;
		if (currentCount >= count && currentCount < foundCount)
		{
			foundIndex = (nint)currentIndex;
			foundCount = currentCount;
			previousChunkLength = currentChunkLength;
			nextChunkLength = 0;
		}

		if (foundIndex >= 0)
		{
			if (foundCount > count)
			{
				if (nextChunkLength > previousChunkLength || nextChunkLength == previousChunkLength && foundIndex >= _memoryBlockCount >>> 1)
				{
					foundIndex = (nint)((nuint)foundIndex + foundCount - count);
				}
			}
			index = (ushort)foundIndex;
			return true;
		}

	NotFound:;
		index = 0;
		return false;
	}

	// This MUST be called from within the lock.
	private void RegisterImageMemoryRegion(byte imageIndex, ushort index, ushort count)
	{
		var allocatedRegions = _allocatedRegions;
		nint allocatedRegionCount = _allocatedRegionCount;
		nint i;
		nuint end = (nuint)index + count;
		for (i = 0; i < allocatedRegionCount; i++)
		{
			ref var region = ref allocatedRegions[i];
			if (region.Index >= end) break;
		}
		nint j = i + 1;
		Array.Copy(allocatedRegions, i, allocatedRegions, j, allocatedRegionCount++ - j);
		allocatedRegions[i] = new(imageIndex, index, count);
		_allocatedRegionCount = (ushort)allocatedRegionCount;
	}

	// This MUST be called from within the lock.
	private int FindImageMemoryRegion(byte index)
	{
		var allocatedRegions = _allocatedRegions;
		nint allocatedRegionCount = _allocatedRegionCount;
		for (int i = 0; i < allocatedRegionCount; i++)
		{
			ref var region = ref allocatedRegions[i];
			if (region.ImageIndex == index) return i;
		}
		return -1;
	}

	// This MUST be called from within the lock.
	private void DeleteMemoryRegion(int index)
	{
		var allocatedRegions = _allocatedRegions;
		nint allocatedRegionCount = _allocatedRegionCount;
		Array.Copy(allocatedRegions, index + 1, allocatedRegions, index, --allocatedRegionCount - index);
		_allocatedRegionCount = (ushort)allocatedRegionCount;
	}

	public async ValueTask UploadImageAsync(byte index, KrakenImageFormat imageFormat, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 15);
		if (data.Length == 0) throw new ArgumentException("Image data is empty.");
		ushort blockCount = (ushort)(((uint)data.Length + 1023) / 1024);
		if (blockCount > _memoryBlockCount) throw new ArgumentException($"Image data exceeded the maximum size of {(float)_memoryBlockCount / 1024}.");
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			int regionIndex = FindImageMemoryRegion(index);
			// For simplicity, for now, we will always delete the current memory region allocation before searching for a better location.
			// TODO: In some cases, we would actually just reuse the exact same storage location and avoid unnecessary steps. To be done after.
			// (Heuristics could be just checking if anchored to previous block or next block. However, it might have some drawbacks regarding memory fragmentation ?)
			if (regionIndex >= 0)
			{
				await _hidTransport.ClearImageStorageAsync(index, cancellationToken).ConfigureAwait(false);
				DeleteMemoryRegion(index);
			}
			if (!TryFindFreeRegion(blockCount, out ushort blockIndex)) throw new InvalidOperationException("Cannot find an available memory region large enough to hold the image data.");
			// TODO: If these operations are interrupted, it could mess up the state of the driver. A backup mechanism should be implemented.
			await _hidTransport.SetImageStorageAsync(index, blockIndex, blockCount, cancellationToken).ConfigureAwait(false);
			RegisterImageMemoryRegion(index, blockIndex, blockCount);
			await _hidTransport.BeginImageUploadAsync(index, cancellationToken).ConfigureAwait(false);
			await _imageTransport.UploadImageAsync(imageFormat, data, cancellationToken).ConfigureAwait(false);
			await _hidTransport.EndImageUploadAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	public async ValueTask DestroyImageAsync(byte index, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 15);
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			int regionIndex = FindImageMemoryRegion(index);
			if (regionIndex < 0) return;
			await _hidTransport.ClearImageStorageAsync(index, cancellationToken).ConfigureAwait(false);
			DeleteMemoryRegion(regionIndex);
		}
	}
}
