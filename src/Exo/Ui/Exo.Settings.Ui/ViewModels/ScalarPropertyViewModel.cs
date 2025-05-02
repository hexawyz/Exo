using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Exo.ColorFormats;
using Exo.Contracts;
using Exo.Lighting;
using Windows.UI;

namespace Exo.Settings.Ui.ViewModels;

// TODO: Strongly type the values
internal sealed class ScalarPropertyViewModel : PropertyViewModel
{
	private object? _value;
	private object? _initialValue;

	public object? MinimumValue { get; }
	public object? MaximumValue { get; }
	public object? DefaultValue { get; }

	public object? InitialValue
	{
		get => _initialValue;
		private set
		{
			bool wasChanged = IsChanged;
			if (SetValue(ref _initialValue, value, ChangedProperty.InitialValue))
			{
				if (!wasChanged)
				{
					_value = _initialValue;
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
			bool wasChanged = IsChanged;
			if (SetValue(ref _value, value, ChangedProperty.Value))
			{
				OnChangeStateChange(wasChanged);
			}
		}
	}

	public override bool IsChanged => Value is null ? InitialValue is not null : !Value.Equals(InitialValue);

	protected override void Reset() => Value = InitialValue;

	public override int ReadInitialValue(ReadOnlySpan<byte> data)
	{
		var (value, length) = ReadValue(DataType, data);
		InitialValue = value;
		return length;
	}

	public override void WriteValue(BinaryWriter writer)
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
