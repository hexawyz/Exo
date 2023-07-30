using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui;

internal sealed class EnumerationValueToNameConverter : DependencyObject, IValueConverter
{
	public ReadOnlyCollection<EnumerationValueViewModel> Values
	{
		get => (ReadOnlyCollection<EnumerationValueViewModel>)GetValue(ValuesProperty);
		set => SetValue(ValuesProperty, value);
	}

	// Using a DependencyProperty as the backing store for Values.  This enables animation, styling, binding, etc...
	public static readonly DependencyProperty ValuesProperty =
		DependencyProperty.Register
		(
			"Values",
			typeof(ReadOnlyCollection<EnumerationValueViewModel>),
			typeof(EnumerationValueToNameConverter),
			new PropertyMetadata
			(
				ReadOnlyCollection<EnumerationValueViewModel>.Empty,
				(s, e) => (s as EnumerationValueToNameConverter)?.OnValuesChanged((ReadOnlyCollection<EnumerationValueViewModel>)e.NewValue))
			);

	private Dictionary<ulong, EnumerationValueViewModel>? _valueToModelMappings;

	private void OnValuesChanged(ReadOnlyCollection<EnumerationValueViewModel> values)
	{
		var dict = new Dictionary<ulong, EnumerationValueViewModel>();
		if (values.Count != 0)
		{
			foreach (var ev in values)
			{
				dict.Add(System.Convert.ToUInt64(ev.Value), ev);
			}
		}
		Interlocked.Exchange(ref _valueToModelMappings, dict);
	}

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (_valueToModelMappings?.TryGetValue(System.Convert.ToUInt64(value), out var result) == true)
		{
			return result.DisplayName;
		}
		return value.ToString();
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
