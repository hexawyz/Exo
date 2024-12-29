using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeviceTools.Usb;

public readonly struct UsbEndpointCollection : IReadOnlyList<UsbEndPoint>
{
	public struct Enumerator : IEnumerator<UsbEndPoint>
	{
		private readonly ReadOnlyMemory<byte> _data;
		private int _offset;

		internal Enumerator(ReadOnlyMemory<byte> data)
		{
			_data = data;
			_offset = -1;
		}

		void IDisposable.Dispose() { }
		void IEnumerator.Reset() => _offset = -1;

		public UsbEndPoint Current => new UsbEndPoint(_data.Slice(_offset, 7));

		object IEnumerator.Current => Current;

		public bool MoveNext()
		{
			var data = _data;
			int offset = _offset;
			if ((uint)offset >= (uint)data.Length)
			{
				if (offset > 0 || (offset = 9) >= data.Length) goto Failure;
				goto ValidateCurrentDescriptor;
			}
		NextDescriptor:;
			offset += Unsafe.As<byte, UsbCommonDescriptor>(ref Unsafe.AsRef(in data.Span[offset])).Length;
			if ((uint)offset >= (uint)data.Length) goto Failure;
		ValidateCurrentDescriptor:;
			if (Unsafe.As<byte, UsbCommonDescriptor>(ref Unsafe.AsRef(in data.Span[offset])).DescriptorType == UsbDescriptorType.Endpoint)
			{
				_offset = offset;
				return true;
			}
			goto NextDescriptor;
		Failure:;
			_offset = data.Length;
			return false;
		}
	}

	private readonly ReadOnlyMemory<byte> _data;

	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete]
	public UsbEndpointCollection() => throw new NotSupportedException();

	internal UsbEndpointCollection(ReadOnlyMemory<byte> data) => _data = data;

	public ref readonly UsbInterfaceDescriptor Descriptor => ref Unsafe.As<byte, UsbInterfaceDescriptor>(ref Unsafe.AsRef(in _data.Span[0]));

	public UsbEndPoint this[int index]
	{
		get
		{
			if ((uint)index > (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
			var data = _data;
			int offset = 9;
			while (true)
			{
				if (offset + 2 > data.Length) throw new InvalidDataException();

				ref readonly var descriptor = ref Unsafe.As<byte, UsbCommonDescriptor>(ref Unsafe.AsRef(in data.Span[offset]));

				if (descriptor.DescriptorType == UsbDescriptorType.Endpoint)
				{
					if (index == 0)
					{
						return new UsbEndPoint(data.Slice(offset, descriptor.Length));
					}
					else
					{
						--index;
					}
				}
				offset += descriptor.Length;
			}
		}
	}

	public int Count => Descriptor.EndpointCount;

	public Enumerator GetEnumerator() => new(_data);
	IEnumerator<UsbEndPoint> IEnumerable<UsbEndPoint>.GetEnumerator() => GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
