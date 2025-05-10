using System.Windows.Input;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

internal partial interface IRefreshable
{
	public static ICommand SharedRefreshCommand => Commands.RefreshCommand.Instance;

	private static partial class Commands
	{
		[GeneratedBindableCustomProperty]
		public sealed partial class RefreshCommand : ICommand
		{
			public static readonly RefreshCommand Instance = new();

			private RefreshCommand() { }

			public async void Execute(object? parameter)
			{
				try
				{
					await ((IRefreshable)parameter!).RefreshAsync(default);
				}
				catch
				{
				}
			}

			public bool CanExecute(object? parameter) => (parameter as IRefreshable)?.CanRefresh ?? false;

			public event EventHandler? CanExecuteChanged;

			public static void RaiseCanExecuteChanged() => Instance.CanExecuteChanged?.Invoke(Instance, EventArgs.Empty);
		}
	}

	static sealed void NotifyCanExecuteChanged() => Commands.RefreshCommand.RaiseCanExecuteChanged();

	ICommand RefreshCommand => SharedRefreshCommand;

	bool CanRefresh => true;

	Task RefreshAsync(CancellationToken cancellationToken);
}
