using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class UInt16ToDoubleConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value is ushort i ? (double)i : null;

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => value is double d ? (ushort)d : null;
}

internal sealed partial class Int16ToDoubleConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value is short i ? (double)i : null;

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => value is double d ? (short)d : null;
}

internal sealed partial class UInt32ToDoubleConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value is uint i ? (double)i : null;

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => value is double d ? (uint)d : null;
}

internal sealed partial class Int32ToDoubleConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value is int i ? (double)i : null;

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => value is double d ? (int)d : null;
}

internal sealed partial class UInt64ToDoubleConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value is ulong i ? (double)i : null;

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => value is double d ? (ulong)d : null;
}

internal sealed partial class Int64ToDoubleConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value is long i ? (double)i : null;

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => value is double d ? (long)d : null;
}

internal sealed partial class HalfToDoubleConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value is Half h ? (double)h : null;

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => value is double d ? (Half)d : null;
}
