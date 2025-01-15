using System.Windows.Input;
using Exo.Metadata;
using Exo.Settings.Ui.Services;
using Exo.Ui;

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

			// TODO: See if it is worth implementing.
			public event EventHandler? CanExecuteChanged
			{
				add { }
				remove { }
			}
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
	private readonly ConnectionViewModel _connectionViewModel;
	private readonly IEditionService _editionService;
	private readonly ISettingsMetadataService _metadataService;
	private readonly DevicesViewModel _devicesViewModel;
	private readonly BatteryDevicesViewModel _batteryDevicesViewModel;
	private readonly LightingViewModel _lightingViewModel;
	private readonly ImagesViewModel _imagesViewModel;
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
	public PageViewModel ImagesPage { get; }
	public PageViewModel SensorsPage { get; }
	public PageViewModel CoolingPage { get; }
	public PageViewModel CustomMenuPage { get; }
	public PageViewModel ProgrammingPage { get; }

	public PageViewModel[] NavigationPages { get; }

	private readonly Commands.GoBackCommand _goBackCommand;
	private readonly Commands.NavigateCommand _navigateCommand;

	public ICommand GoBackCommand => _goBackCommand;
	public ICommand NavigateCommand => _navigateCommand;

	public ConnectionStatus ConnectionStatus => _connectionViewModel.ConnectionStatus;

	public ISettingsMetadataService MetadataService => _metadataService;

	public SettingsViewModel(SettingsServiceConnectionManager connectionManager, ConnectionViewModel connectionViewModel, IEditionService editionService, ISettingsMetadataService metadataService)
	{
		ConnectionManager = connectionManager;
		_connectionViewModel = connectionViewModel;
		_editionService = editionService;
		_metadataService = metadataService;
		_goBackCommand = new(this);
		_navigateCommand = new(this);
		_devicesViewModel = new(ConnectionManager, _metadataService, _navigateCommand);
		_batteryDevicesViewModel = new(_devicesViewModel);
		_lightingViewModel = new(ConnectionManager, _devicesViewModel, _metadataService);
		_imagesViewModel = new();
		_sensorsViewModel = new(ConnectionManager, _devicesViewModel, _metadataService);
		_coolingViewModel = new(ConnectionManager, _devicesViewModel, _sensorsViewModel, _metadataService);
		_programmingViewModel = new(ConnectionManager);
		_customMenuViewModel = new(ConnectionManager);
		_navigationStack = new();
		HomePage = new("Home", "\uE80F");
		DevicesPage = new("Devices", "\uE772");
		LightingPage = new("Lighting", "\uE781");
		ImagesPage = new("Images", "\uE8B9");
		SensorsPage = new("Sensors", "\uE9D9");
		CoolingPage = new("Cooling", "\uE9CA");
		CustomMenuPage = new("CustomMenu", "\uEDE3");
		ProgrammingPage = new("Programming", "\uE943");
		NavigationPages = [HomePage, DevicesPage, LightingPage, ImagesPage, SensorsPage, CoolingPage, CustomMenuPage, ProgrammingPage];
		SelectedNavigationPage = HomePage;

		connectionViewModel.PropertyChanged += OnConnectionViewModelPropertyChanged;
	}

	private void OnConnectionViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (Equals(e, ChangedProperty.ConnectionStatus))
		{
			if (ConnectionStatus == ConnectionStatus.Disconnected)
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
	}

	public DevicesViewModel Devices => _devicesViewModel;
	public BatteryDevicesViewModel BatteryDevices => _batteryDevicesViewModel;
	public LightingViewModel Lighting => _lightingViewModel;
	public SensorsViewModel Sensors => _sensorsViewModel;
	public ImagesViewModel Images => _imagesViewModel;
	public CoolingViewModel Cooling => _coolingViewModel;
	public ProgrammingViewModel Programming => _programmingViewModel;
	public CustomMenuViewModel CustomMenu => _customMenuViewModel;
	public IEditionService EditionService => _editionService;

	public bool CanNavigateBack => _navigationStack.Count > 0;

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
