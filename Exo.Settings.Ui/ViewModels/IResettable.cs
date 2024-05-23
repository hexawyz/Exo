using System.Windows.Input;

namespace Exo.Settings.Ui.ViewModels;

internal interface IResettable : IChangeable
{
	private static class Commands
	{
		public sealed class ResetCommand : ICommand
		{
			public static readonly ResetCommand Instance = new();

			private ResetCommand() { }

			public void Execute(object? parameter) => ((IResettable)parameter!).Reset();
			public bool CanExecute(object? parameter) => (parameter as IResettable)?.IsChanged ?? false;

			public event EventHandler? CanExecuteChanged;

			public static void RaiseCanExecuteChanged() => Instance.CanExecuteChanged?.Invoke(Instance, EventArgs.Empty);
		}
	}

	static sealed void NotifyCanExecuteChanged() => Commands.ResetCommand.RaiseCanExecuteChanged();

	ICommand ResetCommand => Commands.ResetCommand.Instance;

	void Reset();
}
