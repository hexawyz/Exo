using System.ComponentModel;
using Windows.UI;

namespace Exo.Settings.Ui.Services;

internal interface IEditionService : INotifyPropertyChanged
{
	public Color Color { get; set; }
	public bool ShowToolbar { get; set; }
}
