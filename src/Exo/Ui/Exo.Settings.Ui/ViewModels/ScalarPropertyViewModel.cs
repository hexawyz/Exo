using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Exo.ColorFormats;
using Exo.Lighting;
using Windows.UI;

namespace Exo.Settings.Ui.ViewModels;

// TODO: Strongly type the values. Sadly, current state of WinUI XAML won't make this simple, as it can't reference generic types explicitly.
internal sealed class ScalarPropertyViewModel : PropertyViewModel
{
	private object? _value;
	private object? _initialValue;

	public object? MinimumValue { get; }
	public object? MaximumValue { get; }
	public object? DefaultValue { get; }

	private object? InitialValue
	{
		get => _initialValue;
		set
		{
			if (!Equals(_initialValue, value))
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

	public object? Value
	{
		get => _value;
		set
		{
			// We need to coerce the numeric data types here, as sliders will always provide us with their garbage and make everything fail.
			object? newValue = value is null ?
				null :
				PropertyInformation.DataType switch
				{
					LightingDataType.UInt8 => Convert.ToByte(value, CultureInfo.InvariantCulture),
					LightingDataType.SInt8 => Convert.ToSByte(value, CultureInfo.InvariantCulture),
					LightingDataType.UInt16 => Convert.ToUInt16(value, CultureInfo.InvariantCulture),
					LightingDataType.SInt16 => Convert.ToInt16(value, CultureInfo.InvariantCulture),
					LightingDataType.UInt32 => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
					LightingDataType.SInt32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
					LightingDataType.UInt64 => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
					LightingDataType.SInt64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
					LightingDataType.Float16 => value is Half h ? h : (Half)Convert.ToSingle(value, CultureInfo.InvariantCulture),
					LightingDataType.Float32 => Convert.ToSingle(value, CultureInfo.InvariantCulture),
					LightingDataType.Float64 => Convert.ToDouble(value, CultureInfo.InvariantCulture),
					LightingDataType.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
					_ => value
				};
			bool wasChanged = IsChanged;
			if (SetValue(ref _value, newValue, ChangedProperty.Value))
			{
				OnChangeStateChange(wasChanged);
			}
		}
	}

	public override bool IsChanged => Value is null ? InitialValue is not null : !Equals(Value, InitialValue);

	internal override void Reset() => Value = InitialValue;

	internal override int ReadInitialValue(ReadOnlySpan<byte> data)
	{
		var (value, length) = ReadValue(DataType, data);
		InitialValue = value;
		return length;
	}

	internal override void WriteValue(BinaryWriter writer)
	{
		WriteValue(DataType, _value!, writer);
	}

	public ScalarPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength, LightingDeviceBrightnessCapabilitiesViewModel? brightnessCapabilities)
		: base(propertyInformation, paddingLength)
	{
		var dataType = PropertyInformation.DataType;
		if (dataType == LightingDataType.UInt8 && propertyInformation.Name == "BrightnessLevel")
		{
			if (brightnessCapabilities is not null)
			{
				MinimumValue = brightnessCapabilities.MinimumLevel;
				MaximumValue = brightnessCapabilities.MaximumLevel;
				DefaultValue = brightnessCapabilities.MaximumLevel;
			}
		}
		else
		{
			MinimumValue = PropertyInformation.MinimumValue;
			MaximumValue = PropertyInformation.MaximumValue;
			if (PropertyInformation.DefaultValue is not null)
			{
				if (dataType == LightingDataType.ColorRgb24)
				{
					var color = (RgbColor)PropertyInformation.DefaultValue;
					DefaultValue = Color.FromArgb(255, color.R, color.G, color.B);
				}
				else
				{
					DefaultValue = PropertyInformation.DefaultValue;
				}
			}
			else
			{
				DefaultValue = GetDefaultValueForType(dataType);
			}
		}
		EnumerationValues = PropertyInformation.EnumerationValues.IsDefaultOrEmpty ?
			ReadOnlyCollection<EnumerationValueViewModel>.Empty :
			Array.AsReadOnly
			(
				Array.ConvertAll
				(
					ImmutableCollectionsMarshal.AsArray(PropertyInformation.EnumerationValues)!,
					e => new EnumerationValueViewModel
					(
						e.DisplayName,
						dataType switch
						{
							LightingDataType.UInt8 => (byte)e.Value,
							LightingDataType.SInt8 => (sbyte)e.Value,
							LightingDataType.UInt16 => (ushort)e.Value,
							LightingDataType.SInt16 => (short)e.Value,
							LightingDataType.UInt32 => (uint)e.Value,
							LightingDataType.SInt32 => (int)e.Value,
							LightingDataType.UInt64 => e.Value,
							LightingDataType.SInt64 => (long)e.Value,
							_ => throw new NotSupportedException()
						}
					)
				)
			);
		_value = _initialValue = DefaultValue;
	}

	public ReadOnlyCollection<EnumerationValueViewModel> EnumerationValues { get; }
}
