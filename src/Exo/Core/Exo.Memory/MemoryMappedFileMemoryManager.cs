using System.Buffers;
using System.IO.MemoryMappedFiles;
using Microsoft.Win32.SafeHandles;

namespace Exo.Memory;

public sealed unsafe class MemoryMappedFileMemoryManager : MemoryManager<byte>
{
	private readonly SafeMemoryMappedViewHandle _viewHandle;
	private readonly nint _offset;
	private readonly int _length;
	private MemoryMappedViewAccessor? _viewAccessor;

	public MemoryMappedFileMemoryManager(MemoryMappedFile memoryMappedFile, nint offset, int length, MemoryMappedFileAccess access)
	{
		ArgumentNullException.ThrowIfNull(memoryMappedFile);
		ArgumentOutOfRangeException.ThrowIfNegative(offset);
		ArgumentOutOfRangeException.ThrowIfNegative(length);
		_viewAccessor = memoryMappedFile.CreateViewAccessor(offset, length, access);
		_viewHandle = _viewAccessor.SafeMemoryMappedViewHandle;
		_offset = offset;
		_length = length;
	}

	protected override void Dispose(bool disposing)
	{
		if (Interlocked.Exchange(ref _viewAccessor, null) is { } accessor) accessor.Dispose();
	}

	public override Span<byte> GetSpan() => new((byte*)_viewHandle.DangerousGetHandle(), _length);

	public override MemoryHandle Pin(int elementIndex = 0)
	{
		byte* pointer = null;
		_viewHandle.AcquirePointer(ref pointer);
		return new MemoryHandle(pointer, pinnable: this);
	}

	public override void Unpin() => _viewHandle.ReleasePointer();
}
