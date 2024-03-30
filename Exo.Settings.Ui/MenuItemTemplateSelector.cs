using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed class MenuItemTemplateSelector : DataTemplateSelector
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public DataTemplate TextTemplate { get; set; }
	public DataTemplate SubMenuTemplate { get; set; }
	public DataTemplate SeparatorTemplate { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
		=> SelectTemplateCore(item);

	protected override DataTemplate SelectTemplateCore(object item)
		=> item switch
		{
			SeparatorMenuItemViewModel => SeparatorTemplate,
			SubMenuMenuItemViewModel => SubMenuTemplate,
			TextMenuMenuItemViewModel => TextTemplate,
			_ => throw new InvalidOperationException("Unknown menu item type."),
		};
}
