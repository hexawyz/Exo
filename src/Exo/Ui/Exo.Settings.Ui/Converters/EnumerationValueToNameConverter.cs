using System.Collections.ObjectModel;
using System.Numerics;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal abstract partial class EnumerationValueToNameConverter<T, TEnumerationValueViewModel> : DependencyObject, IValueConverter
	where T : INumber<T>
	where TEnumerationValueViewModel : EnumerationValueViewModel<T>
{
	public ReadOnlyCollection<TEnumerationValueViewModel> Values
	{
		get => (ReadOnlyCollection<TEnumerationValueViewModel>)GetValue(ValuesProperty);
		set => SetValue(ValuesProperty, value);
	}

	public static readonly DependencyProperty ValuesProperty =
		DependencyProperty.Register
		(
			"Values",
			typeof(ReadOnlyCollection<TEnumerationValueViewModel>),
			typeof(EnumerationValueToNameConverter<T, TEnumerationValueViewModel>),
			new PropertyMetadata
			(
				ReadOnlyCollection<TEnumerationValueViewModel>.Empty,
				(s, e) => (s as EnumerationValueToNameConverter<T, TEnumerationValueViewModel>)?.OnValuesChanged((ReadOnlyCollection<TEnumerationValueViewModel>)e.NewValue))
			);

	private Dictionary<T, TEnumerationValueViewModel>? _valueToModelMappings;

	private void OnValuesChanged(ReadOnlyCollection<TEnumerationValueViewModel> values)
	{
		var dict = new Dictionary<T, TEnumerationValueViewModel>();
		if (values.Count != 0)
			foreach (var ev in values)
				dict.Add(ev.Value, ev);
		Interlocked.Exchange(ref _valueToModelMappings, dict);
	}

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not T convertedValue)
		{
			if (value is double d) convertedValue = T.CreateChecked(d);
			else throw new Exception("The value is not of an appropriate data type.");
		}
		if (_valueToModelMappings?.TryGetValue(convertedValue, out var result) == true)
			return result.DisplayName;
		return value.ToString();
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

internal sealed partial class ByteEnumerationValueToNameConverter : EnumerationValueToNameConverter<byte, ByteEnumerationValueViewModel> { }
internal sealed partial class SByteEnumerationValueToNameConverter : EnumerationValueToNameConverter<sbyte, SByteEnumerationValueViewModel> { }
internal sealed partial class UInt16EnumerationValueToNameConverter : EnumerationValueToNameConverter<ushort, UInt16EnumerationValueViewModel> { }
internal sealed partial class Int16EnumerationValueToNameConverter : EnumerationValueToNameConverter<short, Int16EnumerationValueViewModel> { }
internal sealed partial class UInt32EnumerationValueToNameConverter : EnumerationValueToNameConverter<uint, UInt32EnumerationValueViewModel> { }
internal sealed partial class Int32EnumerationValueToNameConverter : EnumerationValueToNameConverter<int, Int32EnumerationValueViewModel> { }
internal sealed partial class UInt64EnumerationValueToNameConverter : EnumerationValueToNameConverter<ulong, UInt64EnumerationValueViewModel> { }
internal sealed partial class Int64EnumerationValueToNameConverter : EnumerationValueToNameConverter<long, Int64EnumerationValueViewModel> { }
