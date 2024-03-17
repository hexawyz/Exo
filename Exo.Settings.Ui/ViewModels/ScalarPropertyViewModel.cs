using System.Collections.ObjectModel;
using Exo.Contracts;

namespace Exo.Settings.Ui.ViewModels;

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

	public override void Reset() => Value = InitialValue;

	public override void SetInitialValue(DataValue? value)
	{
		InitialValue = GetValue(DataType, value);
	}

	public override DataValue? GetDataValue() => GetDataValue(DataType, Value);

	public ScalarPropertyViewModel(ConfigurablePropertyInformation propertyInformation, LightingDeviceBrightnessCapabilitiesViewModel? brightnessCapabilities)
		: base(propertyInformation)
	{
		var dataType = PropertyInformation.DataType;
		if (dataType == DataType.UInt8 && propertyInformation.Name == "BrightnessLevel")
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
			MinimumValue = GetValue(dataType, PropertyInformation.MinimumValue);
			MaximumValue = GetValue(dataType, PropertyInformation.MaximumValue);
			DefaultValue = GetValue(dataType, PropertyInformation.DefaultValue) ?? GetDefaultValueForType(dataType);
		}
		EnumerationValues = PropertyInformation.EnumerationValues.IsDefaultOrEmpty ?
			ReadOnlyCollection<EnumerationValueViewModel>.Empty :
			Array.AsReadOnly
			(
				Array.ConvertAll
				(
					PropertyInformation.EnumerationValues.AsMutable(),
					e => new EnumerationValueViewModel
					(
						e.DisplayName,
						dataType switch
						{
							DataType.UInt8 => (byte)e.Value,
							DataType.Int8 => (sbyte)e.Value,
							DataType.UInt16 => (ushort)e.Value,
							DataType.Int16 => (short)e.Value,
							DataType.UInt32 => (uint)e.Value,
							DataType.Int32 => (int)e.Value,
							DataType.UInt64 => e.Value,
							DataType.Int64 => (long)e.Value,
							_ => throw new NotSupportedException()
						}
					)
				)
			);
		_value = _initialValue = DefaultValue;
	}

	public ReadOnlyCollection<EnumerationValueViewModel> EnumerationValues { get; }
}
