using Exo.Lighting.Effects;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class EffectDirectionToBooleanConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
		=> (value is EffectDirection1D d && d != EffectDirection1D.Forward)/* ^ (parameter as bool?).GetValueOrDefault()*/;

	public object ConvertBack(object value, Type targetType, object parameter, string language)
		=> (value as bool?).GetValueOrDefault()/* ^ (parameter as bool?).GetValueOrDefault()*/ ? EffectDirection1D.Backward : EffectDirection1D.Forward;
}

internal sealed partial class EffectDirectionToInverseBooleanConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
		=> value is EffectDirection1D d && d == EffectDirection1D.Forward;

	public object ConvertBack(object value, Type targetType, object parameter, string language)
		=> (value as bool?).GetValueOrDefault() ? EffectDirection1D.Forward : EffectDirection1D.Backward;
}
