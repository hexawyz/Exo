using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeviceTools.Usb;

public readonly struct UsbEndpointCollection : IReadOnlyList<UsbEndPoint>
{
	public struct Enumerator : IEnumerator<UsbEndPoint>
	{
		private readonly ReadOnlyMemory<byte> _data;
		private int _index;

		internal Enumerator(ReadOnlyMemory<byte> data)
		{
			_data = data;
			_index = -1;
		}

		void IDisposable.Dispose() { }
		void IEnumerator.Reset() => _index = -1;

		public UsbEndPoint Current => new UsbEndPoint(_data.Slice(_index, 6));

		object IEnumerator.Current => Current;

		public bool MoveNext()
		{
			if ((uint)_index < _data.Length)
			{
				_index += 6;
			}
			else if (_index < 0)
			{
				_index = 9;
			}
			else
			{
				goto Failure;
			}
			if (_index + 6 <= _data.Length) return true;
			Failure:;
			_index = _data.Length;
			return false;
		}
	}

	private readonly ReadOnlyMemory<byte> _data;

	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete]
	public UsbEndpointCollection() => throw new NotSupportedException();

	internal UsbEndpointCollection(ReadOnlyMemory<byte> data) => _data = data;

	public ref readonly UsbInterfaceDescriptor Descriptor => ref Unsafe.As<byte, UsbInterfaceDescriptor>(ref Unsafe.AsRef(in _data.Span[0]));

	public UsbEndPoint this[int index] => new UsbEndPoint(_data.Slice(9 + checked(6 * index), 6));
	public int Count => Descriptor.EndpointCount;

	public Enumerator GetEnumerator() => new(_data);
	IEnumerator<UsbEndPoint> IEnumerable<UsbEndPoint>.GetEnumerator() => GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
