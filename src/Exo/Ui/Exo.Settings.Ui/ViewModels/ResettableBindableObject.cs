using System.Windows.Input;

namespace Exo.Settings.Ui.ViewModels;

internal abstract class ResettableBindableObject : ChangeableBindableObject, IResettable
{
	public ICommand ResetCommand => IResettable.SharedResetCommand;

	protected abstract void Reset();

	protected override void OnChanged(bool isChanged)
	{
		IResettable.NotifyCanExecuteChanged();
		base.OnChanged(isChanged);
	}

	void IResettable.Reset() => Reset();
}
