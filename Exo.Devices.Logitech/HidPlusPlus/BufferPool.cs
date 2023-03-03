using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Exo.Devices.Logitech.HidPlusPlus;

internal sealed class BufferPool
{
	private readonly ConcurrentStack<IMemoryOwner<byte>> _availableBuffers;
	private readonly int _bufferLength;
	private readonly int _blockLength;

	public BufferPool(int bufferLength, int blockLength)
	{
		_availableBuffers = new();
		_bufferLength = bufferLength;
		_blockLength = blockLength;
	}

	public IMemoryOwner<byte> Rent()
	{
		while (true)
		{
			if (_availableBuffers.TryPop(out var m))
			{
				GC.ReRegisterForFinalize(m);
				return m;
			}
			AllocateNewBlock();
		}
	}

	private void AllocateNewBlock()
	{
		int length = _bufferLength * _blockLength;
		var array = GC.AllocateUninitializedArray<byte>(length, true);

		for (int offset = 0; offset < length; offset += _bufferLength)
		{
			new Buffer(this, array, offset);
		}
	}

	private sealed class Buffer : IMemoryOwner<byte>
	{
		private readonly BufferPool _pool;
		private readonly byte[] _pinnedArray;
		private readonly int _offset;

		public Buffer(BufferPool pool, byte[] pinnedArray, int offset)
		{
			_pool = pool;
			_pinnedArray = pinnedArray;
			_offset = offset;
			Dispose();
		}

		~Buffer() => Dispose(false);

		public void Dispose() => Dispose(true);

		private void Dispose(bool disposing)
		{
			if (_pool is not null && _pinnedArray is not null)
			{
				_pool._availableBuffers.Push(this);
			}
			if (disposing)
			{
				GC.SuppressFinalize(this);
			}
		}

		public Memory<byte> Memory => MemoryMarshal.CreateFromPinnedArray(_pinnedArray, _offset, _pool._bufferLength);
	}
}
