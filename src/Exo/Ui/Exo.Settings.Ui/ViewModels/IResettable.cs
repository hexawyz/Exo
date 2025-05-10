using System.Windows.Input;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

internal partial interface IResettable : IChangeable
{
	public static ICommand SharedResetCommand => Commands.ResetCommand.Instance;

	private static partial class Commands
	{
		[GeneratedBindableCustomProperty]
		public sealed partial class ResetCommand : ICommand
		{
			public static readonly ResetCommand Instance = new();

			private ResetCommand() { }

			public void Execute(object? parameter) => (parameter as IResettable)?.Reset();

			public bool CanExecute(object? parameter) => (parameter as IResettable)?.IsChanged ?? false;

			public event EventHandler? CanExecuteChanged;

			public static void RaiseCanExecuteChanged() => Instance.CanExecuteChanged?.Invoke(Instance, EventArgs.Empty);
		}
	}

	static sealed void NotifyCanExecuteChanged() => Commands.ResetCommand.RaiseCanExecuteChanged();

	ICommand ResetCommand => SharedResetCommand;

	void Reset();
}
