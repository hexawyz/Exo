using System.ComponentModel;

namespace Exo.Settings.Ui.ViewModels;

internal interface IChangeable : INotifyPropertyChanged
{
	bool IsChanged { get; }
}
