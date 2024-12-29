using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeviceTools.Usb;

public readonly struct UsbInterfaceCollection : IReadOnlyList<UsbInterface>
{
	public struct Enumerator : IEnumerator<UsbInterface>
	{
		private readonly byte[] _data;
		private int _offset;
		private int _length;

		internal Enumerator(byte[] data)
		{
			_data = data;
			_offset = -1;
			_length = 10;
		}

		void IDisposable.Dispose() { }

		void IEnumerator.Reset()
		{
			_offset = -1;
			_length = 10;
		}

		public UsbInterface Current => new UsbInterface(_data.AsMemory(_offset, _length));

		object IEnumerator.Current => Current;

		public bool MoveNext()
		{
			var data = _data;
			int offset = _offset += _length;
			if ((uint)offset >= (uint)data.Length) goto Failure;
			while (true)
			{
				offset += Unsafe.As<byte, UsbCommonDescriptor>(ref data[offset]).Length;
				if (offset == data.Length) break;
				if (offset > data.Length) goto Failure;

				if (Unsafe.As<byte, UsbCommonDescriptor>(ref data[offset]).DescriptorType == UsbDescriptorType.Interface) break;
			}
			_length = offset - _offset;
			return true;
		Failure:;
			_offset = data.Length;
			_length = 0;
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

	public UsbInterface this[int index]
	{
		get
		{
			if ((uint)index > (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
			var data = _data;
			int previousOffset = 9;
			int offset = 9;
			while (true)
			{
				if (offset + 2 > data.Length) throw new InvalidDataException();

				ref readonly var descriptor = ref Unsafe.As<byte, UsbCommonDescriptor>(ref data[offset]);

				if (descriptor.DescriptorType == UsbDescriptorType.Interface)
				{
					if (index < 0) break;

					previousOffset = offset;
					--index;
				}
				offset += descriptor.Length;
				if (offset == data.Length && index < 0) break;
			}
			return new UsbInterface(data.AsMemory(previousOffset, offset - previousOffset));
		}
	}

	public int Count => Descriptor.InterfaceCount;

	public Enumerator GetEnumerator() => new(_data);
	IEnumerator<UsbInterface> IEnumerable<UsbInterface>.GetEnumerator() => GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
