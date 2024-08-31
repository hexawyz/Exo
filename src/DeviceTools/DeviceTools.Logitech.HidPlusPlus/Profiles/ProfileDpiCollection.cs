using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 10)]
[DebuggerDisplay("{this[0],d}, {this[1],d}, {this[2],d}, {this[3],d}, {this[4],d}")]
public struct ProfileDpiCollection : IReadOnlyList<ushort>
{
	public struct Enumerator : IEnumerator<ushort>
	{
		private int _index;
		private readonly ProfileDpiCollection _dpiCollection;

		internal Enumerator(in ProfileDpiCollection dpiCollection)
		{
			_index = -1;
			_dpiCollection = dpiCollection;
		}

		readonly void IDisposable.Dispose() { }

		public readonly ushort Current => _dpiCollection[_index];
		readonly object IEnumerator.Current => Current;

		public bool MoveNext() => (uint)++_index < (uint)_dpiCollection.Count;
		void IEnumerator.Reset() => _index = -1;
	}

	private readonly byte _dpi00;
	private readonly byte _dpi01;
	private readonly byte _dpi10;
	private readonly byte _dpi11;
	private readonly byte _dpi20;
	private readonly byte _dpi21;
	private readonly byte _dpi30;
	private readonly byte _dpi31;
	private readonly byte _dpi40;
	private readonly byte _dpi41;

	public ushort this[int index]
	{
		get
		{
			if ((uint)index >= Count) throw new ArgumentOutOfRangeException(nameof(index));

			return LittleEndian.ReadUInt16(in Unsafe.AddByteOffset(ref Unsafe.AsRef(in _dpi00), 2 * index));
		}
		set
		{
			if ((uint)index >= Count) throw new ArgumentOutOfRangeException(nameof(index));

			LittleEndian.Write(ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _dpi00), 2 * index), value);
		}
	}

	public int Count => 5;

	public Enumerator GetEnumerator() => new(in this);
	IEnumerator<ushort> IEnumerable<ushort>.GetEnumerator() => GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
