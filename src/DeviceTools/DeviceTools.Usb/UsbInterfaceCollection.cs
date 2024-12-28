using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeviceTools.Usb;

public readonly struct UsbInterfaceCollection : IReadOnlyCollection<UsbInterface>
{
	public struct Enumerator : IEnumerator<UsbInterface>
	{
		private readonly byte[] _data;
		private int _index;

		internal Enumerator(byte[] data)
		{
			_data = data;
			_index = -1;
		}

		void IDisposable.Dispose() { }
		void IEnumerator.Reset() => _index = -1;

		public UsbInterface Current => new UsbInterface(_data.AsMemory(_index, 9 + 6 * Unsafe.As<byte, UsbInterfaceDescriptor>(ref _data[_index]).EndpointCount));

		object IEnumerator.Current => Current;

		public bool MoveNext()
		{
			if ((uint)_index < _data.Length)
			{
				_index += 9 + 6 * Unsafe.As<byte, UsbInterfaceDescriptor>(ref _data[_index]).EndpointCount;
			}
			else if (_index < 0)
			{
				_index = 9;
			}
			else
			{
				goto Failure;
			}
			if (_index + 9 <= _data.Length) return true;
		Failure:;
			_index = _data.Length;
			return false;
		}
	}

	private readonly byte[] _data;

	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete]
	public UsbInterfaceCollection() => throw new NotSupportedException();

	internal UsbInterfaceCollection(byte[] data)
	{
		_data = data;
	}

	private ref readonly UsbConfigurationDescriptor Descriptor => ref Unsafe.As<byte, UsbConfigurationDescriptor>(ref _data[0]);

	public int Count => Descriptor.InterfaceCount;

	public Enumerator GetEnumerator() => new(_data);
	IEnumerator<UsbInterface> IEnumerable<UsbInterface>.GetEnumerator() => GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
