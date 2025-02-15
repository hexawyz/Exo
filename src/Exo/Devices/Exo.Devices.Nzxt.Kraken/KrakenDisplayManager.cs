using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Exo.Devices.Nzxt.Kraken;

internal sealed class KrakenDisplayManager : IAsyncDisposable
{
	[DebuggerDisplay("Image #{ImageIndex}, Age={Age}, Range={Range}")]
	private struct MemoryRegionInfo
	{
		public readonly byte ImageIndex;
		// This value will certainly wrap around, but should be good enough to be able to efficiently determine when a slot can be evicted.
		public byte Age;
		public readonly MemoryRange Range;

		public MemoryRegionInfo(byte imageIndex, byte age, MemoryRange range)
		{
			ImageIndex = imageIndex;
			Age = age;
			Range = range;
		}
	}

	[DebuggerDisplay("Offset={Offset}, Length={Length}")]
	private readonly struct MemoryRange
	{
		public readonly ushort Offset;
		public readonly ushort Length;

		public MemoryRange(ushort offset, ushort length)
		{
			Offset = offset;
			Length = length;
		}
	}

	public static async Task<KrakenDisplayManager> CreateAsync
	(
		byte imageCount,
		ushort memoryBlockCount,
		KrakenHidTransport hidTransport,
		KrakenWinUsbImageTransport imageTransport,
		CancellationToken cancellationToken
	)
	{
		// The number of image slots is a technical limitation. Can easily be extended if needed, but that is unlikely, so let's not for now.
		// This allows us to quickly map out used image indices.
		if (imageCount > nuint.Size * 8) throw new ArgumentOutOfRangeException(nameof(imageCount), $"The current driver does not support more than {nuint.Size * 8} image slots.");
		// Consider unusable image indices as being allocated. This will simplify everything else.
		nuint allocatedImageIndices = nuint.MaxValue & ~(((nuint)1 << imageCount) - 1);

		// This is the command that CAM is sending when starting up. It would be some kind of reset for the image upload state in case of a previous crash or whatever.
		await hidTransport.CancelImageUploadAsync(cancellationToken).ConfigureAwait(false);

		var currentDisplayMode = await hidTransport.GetDisplayModeAsync(cancellationToken).ConfigureAwait(false);

		// Used to assign age 1 to all images that are not the current one. (So all images will have age 1 if the current display not is not an image :))
		byte currentImageIndex = currentDisplayMode.DisplayMode == KrakenDisplayMode.StoredImage ? currentDisplayMode.ImageIndex : (byte)255;

		// NB: To be honest, we could discard all images here, but something that we can do later is to store the last know state of the device (in case of graceful shutdown),
		// then compare the live state with the last known state. If all images have a different size than the raw image size, we could then consider restoring the persisted state.
		// This would essentially be useful to handle service restarts, and it could be a nice feature to have, but it is not urgent.
		// (Basically, images other than raw images should have a random-enough size, so it is likely a good comparison heuristic in most cases)
		var memoryRegions = new MemoryRegionInfo[imageCount];
		int count = 0;
		for (int i = 0; i < imageCount; i++)
		{
			var info = await hidTransport.GetImageStorageInformationAsync((byte)i, cancellationToken).ConfigureAwait(false);
			if (info.MemoryBlockCount > 0)
			{
				memoryRegions[count++] = new((byte)i, i != currentImageIndex ? (byte)1 : (byte)0, new(info.MemoryBlockIndex, info.MemoryBlockCount));
				allocatedImageIndices |= (nuint)1 << i;
			}
		}
		memoryRegions.AsSpan(0, count).Sort((x, y) => Comparer<ushort>.Default.Compare(x.Range.Offset, y.Range.Offset));
		ref MemoryRegionInfo prev = ref memoryRegions[0];
		for (int i = 1; i < count; i++)
		{
			ref MemoryRegionInfo current = ref memoryRegions[i];
			if (prev.Range.Offset == current.Range.Offset || (uint)prev.Range.Offset + prev.Range.Length > current.Range.Offset)
			{
				// TODO: Log.
				// The device has overlapping memory ranges so to simplify everything, we will fix the problem by forcefully switch to the built-in Liquid temperature view and clear all images.
				await ResetDeviceAsync(hidTransport, memoryRegions, cancellationToken).ConfigureAwait(false);
				memoryRegions.AsSpan().Clear();
				allocatedImageIndices = nuint.MaxValue & ~(((nuint)1 << imageCount) - 1);
				count = 0;
				break;
			}
			prev = ref current;
		}
		return new(hidTransport, imageTransport, memoryRegions, allocatedImageIndices, (ushort)count, memoryBlockCount, currentDisplayMode);
	}

	private static async ValueTask ResetDeviceAsync(KrakenHidTransport hidTransport, Memory<MemoryRegionInfo> memoryRegions, CancellationToken cancellationToken)
	{
		await hidTransport.DisplayPresetVisualAsync(KrakenPresetVisual.Off, cancellationToken).ConfigureAwait(false);
		for (int i = 0; i < memoryRegions.Length; i++)
		{
			await hidTransport.ClearImageStorageAsync(memoryRegions.Span[i].ImageIndex, cancellationToken).ConfigureAwait(false);
		}
	}

	private readonly KrakenHidTransport _hidTransport;
	private readonly KrakenWinUsbImageTransport _imageTransport;
	private readonly AsyncLock _lock;
	// The memory is managed by keeping a memory ordered list of regions that are allocated and to which image slot.
	// This gives us immediate information on which blocks are free and allows for relatively easily search of memory storage locations.
	// Only downside in this case is having to parse the entire array to find info about a memory slot. The array, however, is pretty small.
	// Also to note, this array contains LRU information about all memory slots.
	private readonly MemoryRegionInfo[] _allocatedRegions;
	// Preserve the ID of the last used images for each slot. Or zero if the slot maps to no known image.
	private readonly UInt128[] _imageIds;
	private nuint _allocatedImageIndices;
	private ushort _allocatedRegionCount;
	private readonly ushort _memoryBlockCount;
	private ushort _displayMode;

	private KrakenDisplayManager
	(
		KrakenHidTransport hidTransport,
		KrakenWinUsbImageTransport imageTransport,
		MemoryRegionInfo[] allocatedRegions,
		nuint allocatedImageIndices,
		ushort allocatedRegionCount,
		ushort memoryBlockCount,
		DisplayModeInformation displayMode
	)
	{
		_hidTransport = hidTransport;
		_imageTransport = imageTransport;
		_lock = new();
		_allocatedRegions = allocatedRegions;
		_imageIds = new UInt128[allocatedRegions.Length];
		_allocatedImageIndices = allocatedImageIndices;
		_allocatedRegionCount = allocatedRegionCount;
		_memoryBlockCount = memoryBlockCount;
		_displayMode = GetRawDisplayMode(displayMode);
	}

	public async ValueTask DisposeAsync()
	{
		await _hidTransport.DisposeAsync().ConfigureAwait(false);
		await _imageTransport.DisposeAsync().ConfigureAwait(false);
	}

	// Base logic:
	// 1 - Find the smallest block of free memory at least big enough to fit the requested size.
	// 2 - If the memory block is adjacent to other allocated regions, try to anchor to the largest packed chunk of regions.
	// 3 - In doubt, try to anchor the furthest away from absolute middle memory address. (This would implicitly allow packing regions to the beginning or end of the address space)
	// The algorithm can certainly fail if there is not enough contiguous memory even when excluding the specified index from the lookup.
	// There are basically three possible scenarios:
	// 1 - There is free memory by discarding at most a single image.
	// 2 - There is enough freeable memory before or after the currently displayed image
	// 3 - There is not enough memory available if not discarding the current image.
	// Scenario 1 is the ideal, handled by the algorithm here. It has two sub-scenarios depending on the number of regions currently allocated. (If it is the maximum we always want to try deleting)
	// Scenario 2 has sub-scenarios regarding to how many images would need to be freed to be able to allocate.
	// This can only be solved efficiently using a more complex algorithm. TBD if we want to do that. Easy way out is to just discard all images before OR after the current one.
	// Scenario 3 implies switching off the display and discarding all images without exception. Then starting with free memory.
	// Identifying scenario 3 is very easy, as well as the basic heuristic for scenario 2.
	// Implementing the optimal resolution for scenario 2 is more complex, especially if we want to take into account the LRU.
	// The method below will return a memory offset at which the allocation should be done.
	// The caller must ensure that all corresponding slots are freed and do the cleanup. (Could theoretically be inlined with the rest, but it would make code more complex)
	private bool FindMemoryRegion(ushort length, byte preservedIndex, out ushort offset, out ushort freeLength)
	{
		var allocatedRegions = _allocatedRegions;
		nuint allocatedRegionCount = _allocatedRegionCount;
		// If there are no regions allocated, we can easily start filling up from the start.
		if (allocatedRegionCount == 0)
		{
			offset = 0;
			freeLength = _memoryBlockCount;
			return true;
		}
		// Maintain a map of free memory if we were to discard the corresponding image. (We compute this for every block)
		Span<MemoryRange> potentialFreeRegions = stackalloc MemoryRange[(int)allocatedRegionCount];
		nint freeFoundOffset = -1;
		nuint freeFoundLength = nuint.MaxValue;
		nuint currentFreeOffset = 0;
		nuint currentLength;
		// Track the contiguous blocks of memory following the currently chosen free region.
		nuint previousAllocatedChunkLength = 0;
		nuint nextAllocatedChunkLength = 0;
		nuint currentAllocatedChunkLength = 0;
		// Track the potential region that has been determined to be a valid candidate for freeing.
		nint foundPotentialFreeRegionIndex = -1;
		// Identify the index of the region to preserve if possible.
		nint preservedRegionIndex = -1;

		// In this loop, we do two things at the same time:
		// 1 - Track the optimal already free region
		// 2 - Track the potential free regions, and pick the best one possible. We try to free the oldest region possible for optimal caching. Other criteria are more subjective.
		for (nuint i = 0; i < allocatedRegionCount; i++)
		{
			ref var region = ref allocatedRegions[i];
			ref var potentialFreeRegion = ref potentialFreeRegions[(int)i];

			// This part is tracking how much contiguous free memory would be available if this block was to be freed.
			potentialFreeRegion = i + 1 < allocatedRegionCount ?
				new((ushort)currentFreeOffset, (ushort)(allocatedRegions[i + 1].Range.Offset - currentFreeOffset)) :
				new((ushort)currentFreeOffset, (ushort)(_memoryBlockCount - currentFreeOffset));

			if (region.ImageIndex == preservedIndex)
			{
				preservedRegionIndex = (nint)i;
			}
			// If the potential region can fit the buffer AND it is not the preserved region AND it is the oldest for now AND the smallest with that age, pick it.
			else if (potentialFreeRegion.Length >= length &&
				region.ImageIndex != preservedIndex &&
				(foundPotentialFreeRegionIndex < 0 ||
					region.Age > allocatedRegions[(int)foundPotentialFreeRegionIndex].Age ||
					potentialFreeRegion.Length < potentialFreeRegions[(int)foundPotentialFreeRegionIndex].Length))
			{
				foundPotentialFreeRegionIndex = (nint)i;
			}

			currentLength = region.Range.Offset - currentFreeOffset;
			if (currentLength == 0)
			{
				currentAllocatedChunkLength += region.Range.Length;
			}
			else
			{
				if (currentLength >= length && currentLength < freeFoundLength)
				{
					freeFoundOffset = (nint)currentFreeOffset;
					freeFoundLength = currentLength;
					previousAllocatedChunkLength = currentAllocatedChunkLength;
					nextAllocatedChunkLength = 0;
				}
				else if (nextAllocatedChunkLength == 0)
				{
					nextAllocatedChunkLength = currentAllocatedChunkLength;
				}
				currentAllocatedChunkLength = 0;
			}
			currentFreeOffset = (nuint)region.Range.Offset + region.Range.Length;
		}

		// Handle the last possible memory region.
		currentLength = _memoryBlockCount - currentFreeOffset;
		if (currentLength >= length && currentLength < freeFoundLength)
		{
			freeFoundOffset = (nint)currentFreeOffset;
			freeFoundLength = currentLength;
			previousAllocatedChunkLength = currentAllocatedChunkLength;
			nextAllocatedChunkLength = 0;
		}

		// At that point in the algorithm, we have explored what we needed to explore.
		// If we are not strictly required to free a memory region and a free region was available, we pick that one.
		if (_allocatedImageIndices + 1 != 0 && freeFoundOffset >= 0)
		{
			if (freeFoundLength > length)
			{
				if (nextAllocatedChunkLength > previousAllocatedChunkLength || nextAllocatedChunkLength == previousAllocatedChunkLength && freeFoundOffset >= _memoryBlockCount >>> 1)
				{
					freeFoundOffset = (nint)((nuint)freeFoundOffset + freeFoundLength - length);
					freeFoundLength = length;
				}
			}
			offset = (ushort)freeFoundOffset;
			freeLength = (ushort)freeFoundLength;
			return true;
		}
		else if (foundPotentialFreeRegionIndex >= 0)
		{
			ref var potentialFreeRegion = ref potentialFreeRegions[(int)foundPotentialFreeRegionIndex];
			// TODO: Compare the previous and following chunk lengths. For now, it just sticks to the preceding block.
			offset = potentialFreeRegion.Offset;
			// We return the length to free so that the memory cleanup can return an available index.
			freeLength = potentialFreeRegion.Length;
			return true;
		}
		else if (preservedRegionIndex >= 0)
		{
			var region = allocatedRegions[(int)preservedRegionIndex];
			nuint freeSpaceAfter = (nuint)_memoryBlockCount - region.Range.Length - region.Range.Offset;

			// If there is memory available before or after the current image, choose the smallest valid region just before or after the image.
			if (region.Range.Offset >= length)
			{
				if (freeSpaceAfter >= length && freeSpaceAfter < region.Range.Offset)
				{
					goto PickFreeSpaceAfter;
				}
				else
				{
					goto PickFreeSpaceBefore;
				}
			}
			else if (freeSpaceAfter >= length)
			{
				goto PickFreeSpaceAfter;
			}
			else
			{
				// If we reach this case, it has been determined to be strictly impossible to not free the current image.
				goto OverwriteAll;
			}

		PickFreeSpaceBefore:;
			offset = (ushort)(region.Range.Offset - length);
			freeLength = length;
			return true;
		PickFreeSpaceAfter:;
			offset = (ushort)(region.Range.Offset + region.Range.Length);
			freeLength = length;
			return true;

		}

	OverwriteAll:;
		offset = 0;
		freeLength = _memoryBlockCount;
		return false;
	}

	private async ValueTask<byte> CleanupAsync(ushort blockIndex, ushort blockCount, CancellationToken cancellationToken)
	{
		var allocatedRegions = _allocatedRegions;
		nuint allocatedRegionCount = _allocatedRegionCount;
		nuint start = blockIndex;
		nuint end = start + blockCount;
		nuint allocatedImageIndices = _allocatedImageIndices;
		nint freedRegionIndex = -1;
		nuint freedRegionCount = 0;

		for (nuint i = 0; i < allocatedRegionCount; i++)
		{
			ref var region = ref allocatedRegions[i];
			nuint regionStart = region.Range.Offset;
			nuint regionEnd = regionStart + region.Range.Length;
			if (regionEnd <= start) continue;
			if (regionStart >= end) break;
			if (freedRegionIndex < 0) freedRegionIndex = (nint)i;
			freedRegionCount++;
			allocatedImageIndices &= ~((nuint)1 << region.ImageIndex);
			_imageIds[region.ImageIndex] = 0;
			await _hidTransport.ClearImageStorageAsync(region.ImageIndex, cancellationToken).ConfigureAwait(false);
		}

		if (freedRegionIndex >= 0)
		{
			Array.Copy(allocatedRegions, (int)((nuint)freedRegionIndex + freedRegionCount), allocatedRegions, freedRegionIndex, (int)(allocatedRegionCount - (nuint)freedRegionIndex - freedRegionCount));
			allocatedRegionCount -= freedRegionCount;
			_allocatedImageIndices = allocatedImageIndices;
			_allocatedRegionCount = (ushort)allocatedRegionCount;
		}

		return (byte)BitOperations.TrailingZeroCount(~allocatedImageIndices);
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
			if (region.Range.Offset >= end) break;
		}
		if (i != allocatedRegionCount)
		{
			Array.Copy(allocatedRegions, i, allocatedRegions, i + 1, allocatedRegionCount - i);
		}
		++allocatedRegionCount;
		allocatedRegions[i] = new(imageIndex, 0, new(index, count));
		_allocatedRegionCount = (ushort)allocatedRegionCount;
		_allocatedImageIndices |= (nuint)1 << imageIndex;
	}

	// TODO: There is certainly a way to avoid the allocated region age going too high and overflowing.
	// The likeliest way in which it would happen is if the software was constantly switching between only two (or up to N-1) images, leaving others untouched.
	// While that is not the most likely scenario, it can definitely happen.
	// In the meantime, overflow is still an acceptable scenario, as an region having an age of zero will just make it less likely to be killed.
	private void IncrementAges(byte imageIndex)
	{
		var allocatedRegions = _allocatedRegions;
		nint allocatedRegionCount = _allocatedRegionCount;
		for (nint i = 0; i < allocatedRegionCount; i++)
		{
			ref var region = ref allocatedRegions[i];
			region.Age = region.ImageIndex != imageIndex ? (byte)(region.Age + 1) : (byte)0;
		}
	}

	public async ValueTask DisplayPresetVisualAsync(KrakenPresetVisual visual, CancellationToken cancellationToken)
	{
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			await _hidTransport.DisplayPresetVisualAsync(visual, cancellationToken).ConfigureAwait(false);
		}
	}

	public async ValueTask DisplayImageAsync(UInt128 imageId, KrakenImageFormat imageFormat, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
	{
		if (data.Length == 0) throw new ArgumentException("Image data is empty.");
		ushort blockCount = (ushort)(((uint)data.Length + 1023) / 1024);
		if (blockCount > _memoryBlockCount) throw new ArgumentException($"Image data exceeded the maximum size of {(float)_memoryBlockCount / 1024}.");
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var displayMode = GetDisplayMode(_displayMode);

			// Restore an image that was displayed previously.
			if (_allocatedRegionCount > 0)
			{
				// Easiest scenario in the world is if the image that is being requested is already the one displayed.
				if (displayMode.DisplayMode == KrakenDisplayMode.StoredImage && imageId != 0 && _imageIds[displayMode.ImageIndex] == imageId) return;

				// Otherwise, look across all image slots for an image that could match this one.
				int foundIndex = _imageIds.AsSpan().IndexOf(imageId);

				if (foundIndex >= 0)
				{
					// TODO: Still need to see how this call can be secured so that we don't have a discrepancy in case of cancellation.
					await _hidTransport.DisplayImageAsync((byte)foundIndex, cancellationToken).ConfigureAwait(false);
					IncrementAges((byte)foundIndex);
					Volatile.Write(ref _displayMode, GetRawDisplayMode(new(KrakenDisplayMode.StoredImage, (byte)foundIndex)));
					return;
				}
			}

			byte currentImageIndex = displayMode.DisplayMode == KrakenDisplayMode.StoredImage ? displayMode.ImageIndex : (byte)255;
			bool foundWithoutOverlap = FindMemoryRegion(blockCount, currentImageIndex, out ushort blockIndex, out ushort cleanupRegionLength);

			// NB: Can in fact probably just handle this within CleanupAsync. I initially decided to return a boolean for this info, but it might be unnecessary.
			if (!foundWithoutOverlap)
			{
				await _hidTransport.DisplayPresetVisualAsync(KrakenPresetVisual.Off, cancellationToken).ConfigureAwait(false);
				Volatile.Write(ref _displayMode, GetRawDisplayMode(new(KrakenDisplayMode.Off, 0)));
			}

			byte imageIndex = await CleanupAsync(blockIndex, cleanupRegionLength, cancellationToken).ConfigureAwait(false);

			IncrementAges(255);

			// Register the new memory region at first, so that we can avoid messing up the state too much in case of error.
			// TODO: There may still be some things to to to handle problems more gracefully in case of cancellation. (e.g. remember when there was an error and reset any transfer later)
			// Probably just use a global cancellation instead of the one provided, so that the few tasks below are actually not interrupted before disposal.
			RegisterImageMemoryRegion(imageIndex, blockIndex, blockCount);
			await _hidTransport.SetImageStorageAsync(imageIndex, blockIndex, blockCount, cancellationToken).ConfigureAwait(false);
			await _hidTransport.BeginImageUploadAsync(imageIndex, cancellationToken).ConfigureAwait(false);
			await _imageTransport.UploadImageAsync(imageFormat, data, cancellationToken).ConfigureAwait(false);
			await _hidTransport.EndImageUploadAsync(cancellationToken).ConfigureAwait(false);

			_imageIds[imageIndex] = imageId;

			await _hidTransport.DisplayImageAsync(imageIndex, cancellationToken).ConfigureAwait(false);
			Volatile.Write(ref _displayMode, GetRawDisplayMode(new(KrakenDisplayMode.StoredImage, imageIndex)));
		}
	}

	private static DisplayModeInformation GetDisplayMode(ushort raw) => Unsafe.BitCast<ushort, DisplayModeInformation>(raw);
	private static ushort GetRawDisplayMode(DisplayModeInformation mode) => Unsafe.BitCast<DisplayModeInformation, ushort>(mode);

	public DisplayModeInformation CurrentDisplayMode => GetDisplayMode(Volatile.Read(ref _displayMode));
}
