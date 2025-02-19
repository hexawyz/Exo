using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace Exo.Service;

public class ImageSharpNativeMemoryAllocator : MemoryAllocator
{
	// Allows falling back to the default allocator for allocations smaller than a certain size.
	// May get rid of this later, but this does not seem to harm for now.
	private const int FallbackThreshold = 256;

	protected override int GetBufferCapacityInBytes() => 4 << 20;

	public unsafe override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
		=> length <= FallbackThreshold ? Default.Allocate<T>(length, options) : new NativeMemoryManager<T>(length, options);

	private sealed class NativeMemoryManager<T> : MemoryManager<T>
		where T : struct
	{
		private nint _pointer;
		private readonly int _length;
		private int _refCount;

		public unsafe NativeMemoryManager(int length, AllocationOptions options = AllocationOptions.None)
		{
			_length = length;
			_pointer = options == AllocationOptions.Clean ?
				(nint)NativeMemory.AllocZeroed((nuint)length, (nuint)Unsafe.SizeOf<T>()) :
				(nint)NativeMemory.Alloc((nuint)length, (nuint)Unsafe.SizeOf<T>());
			GC.AddMemoryPressure(_length * Unsafe.SizeOf<T>());
		}

#if DEBUG
#pragma warning disable CA2015 // Do not define finalizers for types derived from MemoryManager<T>
		~NativeMemoryManager()
		{
			System.Diagnostics.Debug.Write($"Finalizer of {nameof(ImageSharpNativeMemoryAllocator)} was called. Check that there is not a Dispose() missing somewhere.");
		}
#pragma warning restore CA2015 // Do not define finalizers for types derived from MemoryManager<T>
#endif

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				int refCount;
				if ((refCount = Interlocked.CompareExchange(ref _refCount, int.MinValue >> 1, 0)) is 0)
				{
					TryFree();
					return;
				}
				while (refCount >= 0)
				{
					if (refCount == (refCount = Interlocked.CompareExchange(ref _refCount, refCount | (int.MinValue >> 1), refCount)))
					{
						if (refCount == 0)
						{
							TryFree();
						}
						return;
					}
				}
			}
			else
			{
				TryFree();
			}
		}

		private unsafe void TryFree()
		{
			nint pointer = Interlocked.Exchange(ref _pointer, 0);
			if (pointer != 0)
			{
				NativeMemory.Free((void*)pointer);
				GC.RemoveMemoryPressure(_length * Unsafe.SizeOf<T>());
			}
		}

		public unsafe override Span<T> GetSpan() => new Span<T>((T*)_pointer, _length);

		public unsafe override MemoryHandle Pin(int elementIndex = 0)
		{
			if (Interlocked.Increment(ref _refCount) < 0)
			{
				Unpin();
			}
			return new MemoryHandle((byte*)_pointer + (nuint)elementIndex * (nuint)Unsafe.SizeOf<T>(), default, this);
		}

		public override void Unpin()
		{
			int refCount = Interlocked.Decrement(ref _refCount);
			if (refCount < 0)
			{
				if (refCount <= int.MinValue >> 1)
				{
					TryFree();
					return;
				}
				// If this (public 😞) method is called without a corresponding call to Pin, we'll just let the user shoot itself in the foot. This should never happen.
			}
		}
	}
}
