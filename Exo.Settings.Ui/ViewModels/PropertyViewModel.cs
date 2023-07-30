using System;
using System.Collections.ObjectModel;
using CommunityToolkit.WinUI.Helpers;
using Exo.Ui.Contracts;
using Windows.UI;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class PropertyViewModel : BindableObject
{
	private static object? GetValue(DataType type, DataValue? value)
		=> value is not null ?
			type switch
			{
				DataType.UInt8 => (byte?)value.UnsignedValue,
				DataType.Int8 => (sbyte?)value.SignedValue,
				DataType.UInt16 => (ushort?)value.UnsignedValue,
				DataType.Int16 => (short?)value.SignedValue,
				DataType.UInt32 => (uint?)value.UnsignedValue,
				DataType.Int32 => (int?)value.SignedValue,
				DataType.UInt64 => value.UnsignedValue,
				DataType.Int64 => value.SignedValue,
				DataType.Float16 => value.SingleValue,
				DataType.Float32 => value.SingleValue,
				DataType.Float64 => value.DoubleValue,
				DataType.ColorGrayscale8 => (byte?)value.UnsignedValue,
				DataType.ColorGrayscale16 => (ushort?)value.UnsignedValue,
				DataType.ColorRgb24 => value.UnsignedValue is ulong uv ? Color.FromArgb(255, (byte)(uv >> 16), (byte)(uv >> 8), (byte)uv) : null,
				DataType.ColorArgb32 => value.UnsignedValue is ulong uv ? Color.FromArgb((byte)(uv >> 24), (byte)(uv >> 16), (byte)(uv >> 8), (byte)uv) : null,
				DataType.Guid => value.GuidValue,
				DataType.String => value.StringValue,
				DataType.TimeSpan => throw new NotImplementedException(),
				DataType.DateTime => throw new NotImplementedException(),
				_ => throw new NotSupportedException()
			} :
			null;

	private static object? GetDefaultValueForType(DataType type)
		=> type switch
		{
			DataType.UInt8 => (byte?)0,
			DataType.Int8 => (sbyte?)0,
			DataType.UInt16 => (ushort?)0,
			DataType.Int16 => (short?)0,
			DataType.UInt32 => (uint?)0,
			DataType.Int32 => (int?)0,
			DataType.UInt64 => 0UL,
			DataType.Int64 => 0L,
			DataType.Float16 => 0f,
			DataType.Float32 => 0f,
			DataType.Float64 => 0d,
			DataType.ColorGrayscale8 => (byte?)0,
			DataType.ColorGrayscale16 => (ushort?)0,
			DataType.ColorRgb24 or DataType.ColorArgb32 => Color.FromArgb(255, 255, 255, 255),
			DataType.Guid => Guid.Empty,
			DataType.String => null,
			DataType.TimeSpan => TimeSpan.Zero,
			DataType.DateTime => DateTime.UtcNow,
			_ => throw new NotSupportedException()
		};

	private readonly ConfigurablePropertyInformation _propertyInformation;
	private object? _value;
	private object? _initialValue;

	public object? MinimumValue { get; }
	public object? MaximumValue { get; }
	public object? DefaultValue { get; }

	private bool _isModified;

	public object? InitialValue
	{
		get => _value;
		set
		{
			if (SetValue(ref _initialValue, value))
			{
				if (!IsModified)
				{
					Value = value;
				}
			}
		}
	}

	public object? Value
	{
		get => _value;
		set
		{
			if (SetValue(ref _value, value))
			{
				IsModified = value != InitialValue;
			}
		}
	}

	public uint? Index => _propertyInformation.Index;

	public DataType DataType => _propertyInformation.DataType;

	public string Name => _propertyInformation.Name;

	public string DisplayName => _propertyInformation.DisplayName;

	public bool IsModified
	{
		get => _isModified;
		private set => SetValue(ref _isModified, value);
	}

	public void OnChangesApplied()
	{
		if (InitialValue != Value)
		{
			InitialValue = Value;
		}
	}

	public void SetInitialValue(DataValue value)
	{
		InitialValue = GetValue(DataType, value);
	}

	public DataValue? GetDataValue() => GetDataValue(DataType, Value);

	private static DataValue? GetDataValue(DataType dataType, object? value)
	{
		if (value is null) return null;

		switch (dataType)
		{
		case DataType.UInt8:
		case DataType.UInt16:
		case DataType.UInt32:
		case DataType.UInt64:
			return new DataValue { UnsignedValue = Convert.ToUInt64(value) };
		case DataType.Int8:
		case DataType.Int16:
		case DataType.Int32:
		case DataType.Int64:
			return new DataValue { SignedValue = Convert.ToInt64(value) };
		case DataType.Float16:
		case DataType.Float32:
			return new DataValue { SingleValue = Convert.ToSingle(value) };
		case DataType.Float64:
			return new DataValue { DoubleValue = Convert.ToDouble(value) };
		case DataType.Boolean:
			return new DataValue { UnsignedValue = (bool)value ? 1U : 0U };
		case DataType.ColorRgb24:
			return new DataValue { UnsignedValue = (uint)((Color)value).ToInt() & 0xFFFFFFU };
		case DataType.ColorArgb32:
			return new DataValue { UnsignedValue = (uint)((Color)value).ToInt() };
		case DataType.String:
			return new DataValue { StringValue = (string)value };
		case DataType.Guid:
			return new DataValue { GuidValue = (Guid)value };
		case DataType.TimeSpan:
		case DataType.DateTime:
		default:
			throw new NotImplementedException();
		}
	}

	public PropertyViewModel(ConfigurablePropertyInformation propertyInformation)
	{
		_propertyInformation = propertyInformation;
		var dataType = _propertyInformation.DataType;
		MinimumValue = GetValue(dataType, _propertyInformation.MinimumValue);
		MaximumValue = GetValue(dataType, _propertyInformation.MaximumValue);
		DefaultValue = GetValue(dataType, _propertyInformation.DefaultValue) ?? GetDefaultValueForType(dataType);
		EnumerationValues = _propertyInformation.EnumerationValues.IsDefaultOrEmpty ?
			ReadOnlyCollection<EnumerationValueViewModel>.Empty :
			Array.AsReadOnly
			(
				Array.ConvertAll
				(
					_propertyInformation.EnumerationValues.AsMutable(),
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
		_value = DefaultValue;
	}

	public ReadOnlyCollection<EnumerationValueViewModel> EnumerationValues { get; }
}
