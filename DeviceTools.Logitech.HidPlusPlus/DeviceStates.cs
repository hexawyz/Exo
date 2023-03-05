using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus;

internal struct DeviceStates<T>
	where T : class
{
	[StructLayout(LayoutKind.Sequential)]
	private struct FixedDeviceStates
	{
#pragma warning disable IDE0044 // Add readonly modifier
		private T? _device0;
		private T? _device1;
		private T? _device2;
		private T? _device3;
		private T? _device4;
		private T? _device5;
		private T? _device6;
		private T? _device255;
#pragma warning restore IDE0044 // Add readonly modifier

		internal static Span<T?> GetSpan(ref FixedDeviceStates value)
			=> MemoryMarshal.CreateSpan(ref Unsafe.AsRef(value._device0), 8);
	}

	private FixedDeviceStates _fixed;
	private T?[]? _variable;

	internal static ref T? GetReference(ref DeviceStates<T> value, byte index)
	{
		byte i = index;

		// We can either minimize the number of comparisons for the special case 255 or for the other cases 0..6.
		// The code below makes it simpler for device indices 0..6, but that is arbitrary.
		if (i >= 7)
		{
			if (i == 255)
			{
				i = 7;
			}
			else
			{
				return ref value.GetRefVariable((byte)(i - 7));
			}
		}
		return ref FixedDeviceStates.GetSpan(ref value._fixed)[i];
	}

	private ref T? GetRefVariable(byte index)
	{
		var array = Volatile.Read(ref _variable);

		if (array is null)
		{
			array = new T[248];
			array = Interlocked.CompareExchange(ref _variable, array, null) ?? array;
		}

		return ref array[index];
	}

	internal static ref T? TryGetReference(ref DeviceStates<T> value, byte index)
	{
		byte i = index;

		// We can either minimize the number of comparisons for the special case 255 or for the other cases 0..6.
		// The code below makes it simpler for device indices 0..6, but that is arbitrary.
		if (i >= 7)
		{
			if (i == 255)
			{
				i = 7;
			}
			else
			{
				return ref value.TryGetRefVariable((byte)(i - 7));
			}
		}
		return ref FixedDeviceStates.GetSpan(ref value._fixed)[i];
	}

	private ref T? TryGetRefVariable(byte index)
	{
		var array = Volatile.Read(ref _variable);

		if (array is null)
		{
			return ref Unsafe.NullRef<T?>();
		}

		return ref array[index];
	}

	public ref struct Enumerator
	{
		private ref DeviceStates<T> _states;
		private int _index;

		public Enumerator(DeviceStates<T> states)
		{
			_states = states;
			_index = -1;
		}

		public T? Current => Volatile.Read(ref GetReference(ref _states, (byte)_index));

		public bool MoveNext()
		{
			int index = _index;

			if ((uint)++index > 256)
			{
				_index = index;
				return false;
			}

			// Skip the variable part, because we don't want to go allocating a new array just for enumerating the contents.
			if (_index == 7 && Volatile.Read(ref _states._variable) is null)
			{
				_index = 255;
			}

			_index = index;
			return true;
		}
	}

	public Enumerator GetEnumerator() => throw new NotImplementedException();
}
