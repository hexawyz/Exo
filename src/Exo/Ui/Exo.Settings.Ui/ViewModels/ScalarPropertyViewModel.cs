using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Settings.Ui.ViewModels;

// This class needs to be overridden for every supported data type.
internal abstract partial class ScalarPropertyViewModel<T> : PropertyViewModel
{
	private T? _value;
	private T? _initialValue;
	private readonly T? _defaultValue;

	protected T? InitialValue
	{
		get => _initialValue;
		set
		{
			if (!EqualityComparer<T>.Default.Equals(_initialValue, value))
			{
				bool wasChanged = IsChanged;
				_initialValue = value;
				if (!wasChanged)
				{
					_value = value;
					NotifyPropertyChanged(ChangedProperty.Value);
				}
				else
				{
					OnChangeStateChange(wasChanged);
				}
			}
		}
	}

	public T? Value
	{
		get => _value;
		set
		{
			bool wasChanged = IsChanged;
			if (SetValue(ref _value, value, ChangedProperty.Value))
			{
				OnChangeStateChange(wasChanged);
			}
		}
	}

	public T? DefaultValue => _defaultValue;

	public override bool IsChanged => Value is null ? InitialValue is not null : !EqualityComparer<T>.Default.Equals(_value, _initialValue);

	internal override void Reset() => Value = InitialValue;

	public ScalarPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength, T? defaultValue)
		: base(propertyInformation, paddingLength)
	{
		_value = _initialValue = _defaultValue = defaultValue;
	}
}

internal abstract partial class NumberScalarProperty<T> : ScalarPropertyViewModel<T>
	where T : struct, INumber<T>
{
	private readonly T _minimumValue;
	private readonly T _maximumValue;
	private readonly byte _hasMinMax;

	public T MinimumValue => _minimumValue;
	public T MaximumValue => _maximumValue;

	public bool HasMinimumValue => (_hasMinMax & 1) != 0;
	public bool HasMaximumValue => (_hasMinMax & 2) != 0;

	public override bool IsRange => (_hasMinMax & 3) == 3;

	public NumberScalarProperty
	(
		ConfigurablePropertyInformation propertyInformation,
		int paddingLength
	) : this(propertyInformation, paddingLength, propertyInformation.DefaultValue as T? ?? default, propertyInformation.MinimumValue as T?, propertyInformation.MaximumValue as T?)
	{
	}

	public NumberScalarProperty
	(
		ConfigurablePropertyInformation propertyInformation,
		int paddingLength,
		T defaultValue,
		T? minimumValue,
		T? maximumValue
	) : base(propertyInformation, paddingLength, defaultValue)
	{
		byte hasMinMax = 0;
		if (minimumValue is not null)
		{
			hasMinMax |= 1;
			_minimumValue = minimumValue.GetValueOrDefault();
		}
		if (maximumValue is not null)
		{
			hasMinMax |= 2;
			_maximumValue = maximumValue.GetValueOrDefault();
		}
		_hasMinMax = hasMinMax;
	}

	internal override int ReadInitialValue(ReadOnlySpan<byte> data)
	{
		InitialValue = MemoryMarshal.Read<T>(data);
		return Unsafe.SizeOf<T>();
	}

	internal override void WriteValue(BinaryWriter writer)
	{
		T value = Value;
		writer.Write(MemoryMarshal.CreateReadOnlySpan<byte>(ref Unsafe.As<T, byte>(ref value), Unsafe.SizeOf<T>()));
	}
}

internal abstract class EnumScalarProperty<T, TEnumValueViewModel> : NumberScalarProperty<T>
	where T : struct, INumber<T>
	where TEnumValueViewModel : EnumerationValueViewModel<T>
{
	private readonly ReadOnlyCollection<TEnumValueViewModel> _enumerationValues;

	public override bool IsEnumeration => true;

	public EnumScalarProperty
	(
		ConfigurablePropertyInformation propertyInformation,
		int paddingLength,
		TEnumValueViewModel[] enumerationValues
	) : base(propertyInformation, paddingLength)
	{
		_enumerationValues = Array.AsReadOnly(enumerationValues);
	}

	public EnumScalarProperty
	(
		ConfigurablePropertyInformation propertyInformation,
		int paddingLength,
		T defaultValue,
		T? minimumValue,
		T? maximumValue,
		TEnumValueViewModel[] enumerationValues
	) : base(propertyInformation, paddingLength, defaultValue, minimumValue, maximumValue)
	{
		_enumerationValues = Array.AsReadOnly(enumerationValues);
	}

	public ReadOnlyCollection<TEnumValueViewModel> EnumerationValues => _enumerationValues;
}

internal sealed partial class BrightnessPropertyViewModel : NumberScalarProperty<byte>
{
	public BrightnessPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength, LightingDeviceBrightnessCapabilitiesViewModel brightnessCapabilities)
		: base(propertyInformation, paddingLength, brightnessCapabilities.MaximumLevel, brightnessCapabilities.MinimumLevel, brightnessCapabilities.MaximumLevel)
	{
	}
}

internal sealed partial class BytePropertyViewModel : NumberScalarProperty<byte>
{
	public BytePropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class SBytePropertyViewModel : NumberScalarProperty<sbyte>
{
	public SBytePropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class UInt16PropertyViewModel : NumberScalarProperty<ushort>
{
	public UInt16PropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class Int16PropertyViewModel : NumberScalarProperty<short>
{
	public Int16PropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class UInt32PropertyViewModel : NumberScalarProperty<uint>
{
	public UInt32PropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class Int32PropertyViewModel : NumberScalarProperty<int>
{
	public Int32PropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class UInt64PropertyViewModel : NumberScalarProperty<ulong>
{
	public UInt64PropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class Int64PropertyViewModel : NumberScalarProperty<long>
{
	public Int64PropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class UInt128PropertyViewModel : NumberScalarProperty<UInt128>
{
	public UInt128PropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class Int128PropertyViewModel : NumberScalarProperty<Int128>
{
	public Int128PropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class HalfPropertyViewModel : NumberScalarProperty<Half>
{
	public HalfPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class SinglePropertyViewModel : NumberScalarProperty<float>
{
	public SinglePropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class DoublePropertyViewModel : NumberScalarProperty<double>
{
	public DoublePropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength) { }
}

internal sealed partial class StringPropertyViewModel : ScalarPropertyViewModel<string>
{
	public StringPropertyViewModel(ConfigurablePropertyInformation propertyInformation) : base(propertyInformation, 0, propertyInformation.DefaultValue as string) { }

	internal override int ReadInitialValue(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		InitialValue = reader.ReadVariableString();
		return (int)((uint)data.Length - reader.RemainingLength);
	}

	internal override void WriteValue(BinaryWriter writer)
	{
		writer.Write(Value ?? "");
	}
}

internal sealed partial class RgbColorPropertyViewModel : ScalarPropertyViewModel<RgbColor>
{
	public RgbColorPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, propertyInformation.DefaultValue as RgbColor? ?? new(255, 255, 255)) { }

	internal override int ReadInitialValue(ReadOnlySpan<byte> data)
	{
		InitialValue = MemoryMarshal.Read<RgbColor>(data);
		return 3;
	}

	internal override void WriteValue(BinaryWriter writer)
	{
		var color = Value;
		writer.Write(color.R);
		writer.Write(color.G);
		writer.Write(color.B);
	}
}

internal sealed partial class ArgbColorPropertyViewModel : ScalarPropertyViewModel<ArgbColor>
{
	public ArgbColorPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, propertyInformation.DefaultValue as ArgbColor? ?? new(255, 255, 255, 255)) { }

	internal override int ReadInitialValue(ReadOnlySpan<byte> data)
	{
		InitialValue = MemoryMarshal.Read<ArgbColor>(data);
		return 4;
	}

	internal override void WriteValue(BinaryWriter writer)
	{
		var color = Value;
		writer.Write(color.A);
		writer.Write(color.R);
		writer.Write(color.G);
		writer.Write(color.B);
	}
}

internal sealed partial class RgbwColorPropertyViewModel : ScalarPropertyViewModel<RgbwColor>
{
	public RgbwColorPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, propertyInformation.DefaultValue as RgbwColor? ?? new(255, 255, 255, 255)) { }

	internal override int ReadInitialValue(ReadOnlySpan<byte> data)
	{
		InitialValue = MemoryMarshal.Read<RgbwColor>(data);
		return 4;
	}

	internal override void WriteValue(BinaryWriter writer)
	{
		var color = Value;
		writer.Write(color.R);
		writer.Write(color.G);
		writer.Write(color.B);
		writer.Write(color.W);
	}
}

internal sealed partial class BooleanPropertyViewModel : ScalarPropertyViewModel<bool>
{
	public BooleanPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, propertyInformation.DefaultValue as bool? ?? false) { }

	internal override int ReadInitialValue(ReadOnlySpan<byte> data)
	{
		InitialValue = data[0] != 0;
		return 1;
	}

	internal override void WriteValue(BinaryWriter writer)
		=> writer.Write(Value ? (byte)1 : (byte)0);
}

internal sealed partial class EffectDirection1DPropertyViewModel : ScalarPropertyViewModel<EffectDirection1D>
{
	public EffectDirection1DPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, propertyInformation.DefaultValue as EffectDirection1D? ?? EffectDirection1D.Forward) { }

	internal override int ReadInitialValue(ReadOnlySpan<byte> data)
	{
		InitialValue = (EffectDirection1D)data[0];
		return 1;
	}

	internal override void WriteValue(BinaryWriter writer)
		=> writer.Write((byte)Value);
}


internal sealed partial class ByteEnumPropertyViewModel : EnumScalarProperty<byte, ByteEnumerationValueViewModel>
{
	private static ByteEnumerationValueViewModel[] CreateEnumerationValues(ConfigurablePropertyInformation propertyInformation)
		=> propertyInformation.EnumerationValues.IsDefaultOrEmpty ?
			[] :
			Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(propertyInformation.EnumerationValues)!, e => new ByteEnumerationValueViewModel(e.DisplayName, (byte)e.Value));

	public ByteEnumPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, CreateEnumerationValues(propertyInformation)) { }
}

internal sealed partial class SByteEnumPropertyViewModel : EnumScalarProperty<sbyte, SByteEnumerationValueViewModel>
{
	private static SByteEnumerationValueViewModel[] CreateEnumerationValues(ConfigurablePropertyInformation propertyInformation)
		=> propertyInformation.EnumerationValues.IsDefaultOrEmpty ?
			[] :
			Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(propertyInformation.EnumerationValues)!, e => new SByteEnumerationValueViewModel(e.DisplayName, (sbyte)e.Value));

	public SByteEnumPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, CreateEnumerationValues(propertyInformation)) { }
}

internal sealed partial class UInt16EnumPropertyViewModel : EnumScalarProperty<ushort, UInt16EnumerationValueViewModel>
{
	private static UInt16EnumerationValueViewModel[] CreateEnumerationValues(ConfigurablePropertyInformation propertyInformation)
		=> propertyInformation.EnumerationValues.IsDefaultOrEmpty ?
			[] :
			Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(propertyInformation.EnumerationValues)!, e => new UInt16EnumerationValueViewModel(e.DisplayName, (ushort)e.Value));

	public UInt16EnumPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, CreateEnumerationValues(propertyInformation)) { }
}

internal sealed partial class Int16EnumPropertyViewModel : EnumScalarProperty<short, Int16EnumerationValueViewModel>
{
	private static Int16EnumerationValueViewModel[] CreateEnumerationValues(ConfigurablePropertyInformation propertyInformation)
		=> propertyInformation.EnumerationValues.IsDefaultOrEmpty ?
			[] :
			Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(propertyInformation.EnumerationValues)!, e => new Int16EnumerationValueViewModel(e.DisplayName, (short)e.Value));

	public Int16EnumPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, CreateEnumerationValues(propertyInformation)) { }
}

internal sealed partial class UInt32EnumPropertyViewModel : EnumScalarProperty<uint, UInt32EnumerationValueViewModel>
{
	private static UInt32EnumerationValueViewModel[] CreateEnumerationValues(ConfigurablePropertyInformation propertyInformation)
		=> propertyInformation.EnumerationValues.IsDefaultOrEmpty ?
			[] :
			Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(propertyInformation.EnumerationValues)!, e => new UInt32EnumerationValueViewModel(e.DisplayName, (uint)e.Value));

	public UInt32EnumPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, CreateEnumerationValues(propertyInformation)) { }
}

internal sealed partial class Int32EnumPropertyViewModel : EnumScalarProperty<int, Int32EnumerationValueViewModel>
{
	private static Int32EnumerationValueViewModel[] CreateEnumerationValues(ConfigurablePropertyInformation propertyInformation)
		=> propertyInformation.EnumerationValues.IsDefaultOrEmpty ?
			[] :
			Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(propertyInformation.EnumerationValues)!, e => new Int32EnumerationValueViewModel(e.DisplayName, (int)e.Value));

	public Int32EnumPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, CreateEnumerationValues(propertyInformation)) { }
}

internal sealed partial class UInt64EnumPropertyViewModel : EnumScalarProperty<ulong, UInt64EnumerationValueViewModel>
{
	private static UInt64EnumerationValueViewModel[] CreateEnumerationValues(ConfigurablePropertyInformation propertyInformation)
		=> propertyInformation.EnumerationValues.IsDefaultOrEmpty ?
			[] :
			Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(propertyInformation.EnumerationValues)!, e => new UInt64EnumerationValueViewModel(e.DisplayName, (ulong)e.Value));

	public UInt64EnumPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, CreateEnumerationValues(propertyInformation)) { }
}

internal sealed partial class Int64EnumPropertyViewModel : EnumScalarProperty<long, Int64EnumerationValueViewModel>
{
	private static Int64EnumerationValueViewModel[] CreateEnumerationValues(ConfigurablePropertyInformation propertyInformation)
		=> propertyInformation.EnumerationValues.IsDefaultOrEmpty ?
			[] :
			Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(propertyInformation.EnumerationValues)!, e => new Int64EnumerationValueViewModel(e.DisplayName, (long)e.Value));

	public Int64EnumPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength) : base(propertyInformation, paddingLength, CreateEnumerationValues(propertyInformation)) { }
}
