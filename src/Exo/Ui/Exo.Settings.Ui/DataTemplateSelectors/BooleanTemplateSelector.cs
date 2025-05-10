using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui.DataTemplateSelectors;

internal sealed partial class BooleanTemplateSelector : DataTemplateSelector
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public DataTemplate NullTemplate { get; set; }
	public DataTemplate FalseTemplate { get; set; }
	public DataTemplate TrueTemplate { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	protected override DataTemplate SelectTemplateCore(object item)
		=> item is bool b ?
			b ?
				TrueTemplate :
				FalseTemplate :
			NullTemplate;
}
