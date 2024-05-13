using System.Windows.Input;
using Exo.Settings.Ui.Services;
using Exo.Ui;
using Exo.Utils;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class SettingsViewModel : BindableObject
{
	private static class Commands
	{
		public class GoBackCommand : ICommand
		{
			private readonly SettingsViewModel _viewModel;

			public GoBackCommand(SettingsViewModel viewModel) => _viewModel = viewModel;

			public void Execute(object? parameter) => _viewModel.GoBack();

			public bool CanExecute(object? parameter) => _viewModel.CanNavigateBack;

			public event EventHandler? CanExecuteChanged;
		}

		public class NavigateCommand : ICommand
		{
			private readonly SettingsViewModel _viewModel;

			public NavigateCommand(SettingsViewModel viewModel) => _viewModel = viewModel;

			public void Execute(object? parameter)
			{
				ArgumentNullException.ThrowIfNull(parameter);
				_viewModel.NavigateTo((PageViewModel)parameter!);
			}

			public bool CanExecute(object? parameter) => parameter is PageViewModel;

			public event EventHandler? CanExecuteChanged
			{
				add { }
				remove { }
			}
		}
	}

	public SettingsServiceConnectionManager ConnectionManager { get; }
	private readonly IEditionService _editionService;
	private readonly DevicesViewModel _devicesViewModel;
	private readonly LightingViewModel _lightingViewModel;
	private readonly SensorsViewModel _sensorsViewModel;
	private readonly CoolingViewModel _coolingViewModel;
	private readonly ProgrammingViewModel _programmingViewModel;
	private readonly CustomMenuViewModel _customMenuViewModel;

	private readonly List<PageViewModel> _navigationStack;
	private PageViewModel? _selectedNavigationPage;

	public PageViewModel? SelectedNavigationPage
	{
		get => _selectedNavigationPage;
		set => SetValue(ref _selectedNavigationPage, value, ChangedProperty.SelectedNavigationPage);
	}

	public PageViewModel CurrentPage => _navigationStack.Count > 0 ? _navigationStack[^1] : HomePage;

	public PageViewModel HomePage { get; }
	public PageViewModel DevicesPage { get; }
	public PageViewModel LightingPage { get; }
	public PageViewModel SensorsPage { get; }
	public PageViewModel CoolingPage { get; }
	public PageViewModel CustomMenuPage { get; }
	public PageViewModel ProgrammingPage { get; }

	public PageViewModel[] NavigationPages { get; }

	private readonly Commands.GoBackCommand _goBackCommand;
	private readonly Commands.NavigateCommand _navigateCommand;

	public ICommand GoBackCommand => _goBackCommand;
	public ICommand NavigateCommand => _navigateCommand;

	private ConnectionStatus _connectionStatus;

	public SettingsViewModel(IEditionService editionService)
	{
		ConnectionManager = new
		(
			"Local\\Exo.Service.Configuration", 
			100,
#if DEBUG
			null,
#else
			GitCommitHelper.GetCommitId(typeof(SettingsViewModel).Assembly),
#endif
			OnConnectionStatusChanged
		);
		_editionService = editionService;
		_goBackCommand = new(this);
		_navigateCommand = new(this);
		_devicesViewModel = new(ConnectionManager, _navigateCommand);
		_lightingViewModel = new(ConnectionManager, _devicesViewModel, _editionService);
		_sensorsViewModel = new(ConnectionManager, _devicesViewModel);
		_coolingViewModel = new(ConnectionManager, _devicesViewModel, _sensorsViewModel);
		_programmingViewModel = new(ConnectionManager);
		_customMenuViewModel = new();
		_navigationStack = new();
		HomePage = new("Home", "\uE80F");
		DevicesPage = new("Devices", "\uE772");
		LightingPage = new("Lighting", "\uE781");
		SensorsPage = new("Sensors", "\uE9D9");
		CoolingPage = new("Cooling", "\uE9CA");
		CustomMenuPage = new("CustomMenu", "\uEDE3");
		ProgrammingPage = new("Programming", "\uE943");
		NavigationPages = [HomePage, DevicesPage, LightingPage, SensorsPage, CoolingPage, CustomMenuPage, ProgrammingPage];
		SelectedNavigationPage = HomePage;
	}

	public DevicesViewModel Devices => _devicesViewModel;
	public LightingViewModel Lighting => _lightingViewModel;
	public SensorsViewModel Sensors => _sensorsViewModel;
	public CoolingViewModel Cooling => _coolingViewModel;
	public ProgrammingViewModel Programming => _programmingViewModel;
	public CustomMenuViewModel CustomMenu => _customMenuViewModel;
	public IEditionService EditionService => _editionService;

	public ConnectionStatus ConnectionStatus => _connectionStatus;
	public bool CanNavigateBack => _navigationStack.Count > 0;

	private void OnConnectionStatusChanged(SettingsServiceConnectionManager connectionManager, ConnectionStatus connectionStatus)
	{
		_connectionStatus = connectionStatus;
		if (connectionStatus == ConnectionStatus.Disconnected)
		{
			bool wasStackEmpty = _navigationStack.Count == 0;
			_navigationStack.Clear();
			NotifyPropertyChanged(ChangedProperty.ConnectionStatus);
			NotifyPropertyChanged(ChangedProperty.CurrentPage);
			if (!wasStackEmpty) NotifyPropertyChanged(ChangedProperty.CanNavigateBack);
			SelectedNavigationPage = null;
		}
		else
		{
			NotifyPropertyChanged(ChangedProperty.ConnectionStatus);
		}
	}

	private void NavigateTo(PageViewModel pageViewModel)
	{
		if (_navigationStack.Count == 0 ? pageViewModel != HomePage : _navigationStack[^1] != pageViewModel)
		{
			_navigationStack.Add(pageViewModel);
			NotifyPropertyChanged(ChangedProperty.CurrentPage);
			if (_navigationStack.Count == 1) NotifyPropertyChanged(ChangedProperty.CanNavigateBack);
		}
	}

	private void GoBack()
	{
		if (_navigationStack.Count > 0)
		{
			_navigationStack.RemoveAt(_navigationStack.Count - 1);
			NotifyPropertyChanged(ChangedProperty.CurrentPage);
			if (_navigationStack.Count == 0) NotifyPropertyChanged(ChangedProperty.CanNavigateBack);
		}
	}
}
