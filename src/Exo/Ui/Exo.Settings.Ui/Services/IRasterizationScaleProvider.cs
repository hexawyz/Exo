using System.ComponentModel;

namespace Exo.Settings.Ui.Services;

internal interface IRasterizationScaleProvider : INotifyPropertyChanged
{
	double RasterizationScale { get; }
}
