using System.Collections.ObjectModel;
using System.Windows.Input;
using Exo.Settings.Ui.Services;

namespace Exo.Settings.Ui.ViewModels;

public sealed class NotificationViewModel
{
	private static class Commands
	{
		public sealed class CloseCommand : ICommand
		{
			public static readonly CloseCommand Instance = new();

			private CloseCommand() { }

			public bool CanExecute(object? parameter) => true;
			public void Execute(object? parameter) => (parameter as NotificationViewModel)?.Close();

			public event EventHandler? CanExecuteChanged;

			public static void RaiseCanExecuteChanged() => Instance.CanExecuteChanged?.Invoke(Instance, EventArgs.Empty);
		}
	}

	private readonly ObservableCollection<NotificationViewModel> _owner;
	private readonly NotificationSeverity _severity;
	private readonly string _title;
	private readonly string _message;

	public NotificationViewModel(ObservableCollection<NotificationViewModel> owner, NotificationSeverity severity, string title, string message)
	{
		_owner = owner;
		_severity = severity;
		_title = title;
		_message = message;
	}

	public NotificationSeverity Severity => _severity;
	public string Title => _title;
	public string Message => _message;
	public bool IsOpen => true;

	public ICommand CloseCommand => Commands.CloseCommand.Instance;

	private void Close() => _owner.Remove(this);
}
