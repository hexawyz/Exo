using System.Numerics;

namespace Exo.Settings.Ui.ViewModels;

internal abstract class EnumerationValueViewModel(string displayName)
{
	public string DisplayName { get; } = displayName;
	public ulong Value => GetValue();

	protected abstract ulong GetValue();
}

// This class needs to be overridden for every supported data type.
internal abstract class EnumerationValueViewModel<T> : EnumerationValueViewModel
	where T : INumber<T>
{
	public EnumerationValueViewModel(string displayName, T value) : base(displayName)
	{
		Value = value;
	}

	public new T Value { get; }
}

internal sealed class ByteEnumerationValueViewModel : EnumerationValueViewModel<byte>
{
	public ByteEnumerationValueViewModel(string displayName, byte value) : base(displayName, value) { }
	protected sealed override ulong GetValue() => Value;
}

internal sealed class SByteEnumerationValueViewModel : EnumerationValueViewModel<sbyte>
{
	public SByteEnumerationValueViewModel(string displayName, sbyte value) : base(displayName, value) { }
	protected sealed override ulong GetValue() => (ulong)(long)Value;
}

internal sealed class UInt16EnumerationValueViewModel : EnumerationValueViewModel<ushort>
{
	public UInt16EnumerationValueViewModel(string displayName, ushort value) : base(displayName, value) { }
	protected sealed override ulong GetValue() => Value;
}

internal sealed class Int16EnumerationValueViewModel : EnumerationValueViewModel<short>
{
	public Int16EnumerationValueViewModel(string displayName, short value) : base(displayName, value) { }
	protected sealed override ulong GetValue() => (ulong)(long)Value;
}

internal sealed class UInt32EnumerationValueViewModel : EnumerationValueViewModel<uint>
{
	public UInt32EnumerationValueViewModel(string displayName, uint value) : base(displayName, value) { }
	protected sealed override ulong GetValue() => Value;
}

internal sealed class Int32EnumerationValueViewModel : EnumerationValueViewModel<int>
{
	public Int32EnumerationValueViewModel(string displayName, int value) : base(displayName, value) { }
	protected sealed override ulong GetValue() => (ulong)(long)Value;
}

internal sealed class UInt64EnumerationValueViewModel : EnumerationValueViewModel<ulong>
{
	public UInt64EnumerationValueViewModel(string displayName, ulong value) : base(displayName, value) { }
	protected sealed override ulong GetValue() => Value;
}

internal sealed class Int64EnumerationValueViewModel : EnumerationValueViewModel<long>
{
	public Int64EnumerationValueViewModel(string displayName, long value) : base(displayName, value) { }
	protected sealed override ulong GetValue() => (ulong)Value;
}
