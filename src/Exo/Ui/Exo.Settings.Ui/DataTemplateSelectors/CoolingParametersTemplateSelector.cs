using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui.DataTemplateSelectors;

internal sealed partial class CoolingParametersTemplateSelector : DataTemplateSelector
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public DataTemplate AutomaticTemplate { get; set; }
	public DataTemplate FixedTemplate { get; set; }
	public DataTemplate SoftwareControlCurveTemplate { get; set; }
	public DataTemplate HardwareControlCurveTemplate { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
		=> SelectTemplateCore(item);

	protected override DataTemplate SelectTemplateCore(object item)
		=> item switch
		{
			null or AutomaticCoolingModeViewModel => AutomaticTemplate,
			FixedCoolingModeViewModel => FixedTemplate,
			SoftwareControlCurveCoolingModeViewModel => SoftwareControlCurveTemplate,
			HardwareControlCurveCoolingModeViewModel => HardwareControlCurveTemplate,
			_ => throw new InvalidOperationException("Unknown cooling parameters."),
		};
}
