using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.LedEffects;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 22)]
[DebuggerDisplay("Count = {Count}")]
public struct LedEffectCollection : IReadOnlyList<LedEffect>
{
	public struct Enumerator : IEnumerator<LedEffect>
	{
		private int _index;
		private readonly LedEffectCollection _collection;

		internal Enumerator(in LedEffectCollection collection)
		{
			_index = -1;
			_collection = collection;
		}

		void IDisposable.Dispose() { }

		public LedEffect Current => _collection[_index];
		object IEnumerator.Current => Current;

		public bool MoveNext() => (uint)++_index < (uint)_collection.Count;
		void IEnumerator.Reset() => _index = -1;
	}

	private readonly LedEffect _led0;
	private readonly LedEffect _led1;

	public LedEffect this[int index]
	{
		get
		{
			if ((uint)index > 2) throw new ArgumentOutOfRangeException(nameof(index));

			return Unsafe.ReadUnaligned<LedEffect>(in Unsafe.AddByteOffset(ref Unsafe.As<LedEffect, byte>(ref Unsafe.AsRef(in _led0)), 11 * index));
		}
		set
		{
			if ((uint)index > 2) throw new ArgumentOutOfRangeException(nameof(index));

			Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref Unsafe.As<LedEffect, byte>(ref Unsafe.AsRef(in _led0)), 11 * index), value);
		}
	}

	public int Count => 2;

	public Enumerator GetEnumerator() => new(in this);
	IEnumerator<LedEffect> IEnumerable<LedEffect>.GetEnumerator() => GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
