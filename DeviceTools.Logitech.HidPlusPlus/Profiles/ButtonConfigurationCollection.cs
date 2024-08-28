using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
[DebuggerDisplay("Count = {Count}")]
public readonly struct ButtonConfigurationCollection : IReadOnlyList<ButtonConfiguration>
{
	public struct Enumerator : IEnumerator<ButtonConfiguration>
	{
		private int _index;
		private readonly ButtonConfigurationCollection _collection;

		internal Enumerator(in ButtonConfigurationCollection collection)
		{
			_index = -1;
			_collection = collection;
		}

		void IDisposable.Dispose() { }

		public ButtonConfiguration Current => _collection[_index];
		object IEnumerator.Current => Current;

		public bool MoveNext() => (uint)++_index < (uint)_collection.Count;
		void IEnumerator.Reset() => _index = -1;
	}

	private readonly ButtonConfiguration _button0;
	private readonly ButtonConfiguration _button1;
	private readonly ButtonConfiguration _button2;
	private readonly ButtonConfiguration _button3;
	private readonly ButtonConfiguration _button4;
	private readonly ButtonConfiguration _button5;
	private readonly ButtonConfiguration _button6;
	private readonly ButtonConfiguration _button7;
	private readonly ButtonConfiguration _button8;
	private readonly ButtonConfiguration _button9;
	private readonly ButtonConfiguration _buttonA;
	private readonly ButtonConfiguration _buttonB;
	private readonly ButtonConfiguration _buttonC;
	private readonly ButtonConfiguration _buttonD;
	private readonly ButtonConfiguration _buttonE;
	private readonly ButtonConfiguration _buttonF;

	public ButtonConfiguration this[int index]
	{
		get
		{
			if ((uint)index > 16) throw new ArgumentOutOfRangeException(nameof(index));

			return Unsafe.ReadUnaligned<ButtonConfiguration>(in Unsafe.AddByteOffset(ref Unsafe.As<ButtonConfiguration, byte>(ref Unsafe.AsRef(in _button0)), 4 * index));
		}
		init
		{
			if ((uint)index > 16) throw new ArgumentOutOfRangeException(nameof(index));

			Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref Unsafe.As<ButtonConfiguration, byte>(ref Unsafe.AsRef(in _button0)), 4 * index), value);
		}
	}

	public int Count => 16;

	public Enumerator GetEnumerator() => new(in this);
	IEnumerator<ButtonConfiguration> IEnumerable<ButtonConfiguration>.GetEnumerator() => GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
