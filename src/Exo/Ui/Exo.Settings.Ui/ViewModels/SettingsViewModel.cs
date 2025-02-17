using System.Collections.ObjectModel;
using System.Windows.Input;
using Exo.Metadata;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class SettingsViewModel : BindableObject, INotificationSystem
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

		public class GoForwardCommand : ICommand
		{
			private readonly SettingsViewModel _viewModel;

			public GoForwardCommand(SettingsViewModel viewModel) => _viewModel = viewModel;

			public void Execute(object? parameter) => _viewModel.GoForward();

			public bool CanExecute(object? parameter) => _viewModel.CanNavigateForward;

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
	private readonly ObservableCollection<NotificationViewModel> _notifications;
	private readonly ReadOnlyObservableCollection<NotificationViewModel> _readOnlyNotifications;

	private readonly List<PageViewModel> _navigationStack;
	private int _currentPageIndex;
	private PageViewModel? _selectedNavigationPage;

	public PageViewModel? SelectedNavigationPage
	{
		get => _selectedNavigationPage;
		set => SetValue(ref _selectedNavigationPage, value, ChangedProperty.SelectedNavigationPage);
	}

	public PageViewModel CurrentPage => (uint)_currentPageIndex < (uint)_navigationStack.Count ? _navigationStack[_currentPageIndex] : HomePage;

	public PageViewModel HomePage { get; }
	public PageViewModel DevicesPage { get; }
	public PageViewModel LightingPage { get; }
	public PageViewModel SensorsPage { get; }
	public PageViewModel CoolingPage { get; }
	public PageViewModel ImagesPage { get; }
	public PageViewModel CustomMenuPage { get; }
	public PageViewModel ProgrammingPage { get; }

	public PageViewModel[] NavigationPages { get; }

	private readonly Commands.GoBackCommand _goBackCommand;
	private readonly Commands.GoForwardCommand _goForwardCommand;
	private readonly Commands.NavigateCommand _navigateCommand;

	public ICommand GoBackCommand => _goBackCommand;
	public ICommand GoForwardCommand => _goForwardCommand;
	public ICommand NavigateCommand => _navigateCommand;

	public ConnectionStatus ConnectionStatus => _connectionViewModel.ConnectionStatus;

	public ISettingsMetadataService MetadataService => _metadataService;

	public SettingsViewModel
	(
		SettingsServiceConnectionManager connectionManager,
		ConnectionViewModel connectionViewModel,
		IRasterizationScaleProvider rasterizationScaleProvider,
		IEditionService editionService,
		IFileOpenDialog fileOpenDialog,
		ISettingsMetadataService metadataService
	)
	{
		ConnectionManager = connectionManager;
		_connectionViewModel = connectionViewModel;
		_editionService = editionService;
		_metadataService = metadataService;
		_goBackCommand = new(this);
		_goForwardCommand = new(this);
		_navigateCommand = new(this);
		_imagesViewModel = new(ConnectionManager, fileOpenDialog, this);
		_devicesViewModel = new(ConnectionManager, _imagesViewModel.Images, _metadataService, rasterizationScaleProvider, _navigateCommand);
		_batteryDevicesViewModel = new(_devicesViewModel);
		_lightingViewModel = new(ConnectionManager, _devicesViewModel, _metadataService);
		_sensorsViewModel = new(ConnectionManager, _devicesViewModel, _metadataService);
		_coolingViewModel = new(ConnectionManager, _devicesViewModel, _sensorsViewModel, _metadataService);
		_programmingViewModel = new(ConnectionManager);
		_customMenuViewModel = new(ConnectionManager);
		_navigationStack = new();
		_notifications = new();
		_readOnlyNotifications = new(_notifications);
		HomePage = new("Home", "\uE80F");
		DevicesPage = new("Devices", "\uE772");
		LightingPage = new("Lighting", "\uE781");
		SensorsPage = new("Sensors", "\uE9D9");
		CoolingPage = new("Cooling", "\uE9CA");
		ImagesPage = new("Images", "\uE8B9");
		CustomMenuPage = new("CustomMenu", "\uEDE3");
		ProgrammingPage = new("Programming", "\uE943");
		NavigationPages = [HomePage, DevicesPage, LightingPage, SensorsPage, CoolingPage, ImagesPage, CustomMenuPage, ProgrammingPage];
		SelectedNavigationPage = HomePage;
		_currentPageIndex = -1;

		connectionViewModel.PropertyChanged += OnConnectionViewModelPropertyChanged;
	}

	private void OnConnectionViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (Equals(e, ChangedProperty.ConnectionStatus))
		{
			if (ConnectionStatus == ConnectionStatus.Disconnected)
			{
				uint oldStackLength = (uint)_navigationStack.Count;
				int oldPageIndex = _currentPageIndex;
				_navigationStack.Clear();
				_currentPageIndex = -1;
				NotifyPropertyChanged(ChangedProperty.ConnectionStatus);
				NotifyPropertyChanged(ChangedProperty.CurrentPage);
				if (oldPageIndex >= 0) NotifyPropertyChanged(ChangedProperty.CanNavigateBack);
				if ((uint)(oldPageIndex + 1) < oldStackLength) NotifyPropertyChanged(ChangedProperty.CanNavigateForward);
				SelectedNavigationPage = HomePage;
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
	public CoolingViewModel Cooling => _coolingViewModel;
	public ImagesViewModel Images => _imagesViewModel;
	public ProgrammingViewModel Programming => _programmingViewModel;
	public CustomMenuViewModel CustomMenu => _customMenuViewModel;
	public IEditionService EditionService => _editionService;
	public ReadOnlyObservableCollection<NotificationViewModel> Notifications => _readOnlyNotifications;

	public bool CanNavigateBack => _currentPageIndex >= 0;
	public bool CanNavigateForward => (uint)(_currentPageIndex + 1) < (uint)_navigationStack.Count;

	private void NavigateTo(PageViewModel pageViewModel)
	{
		if (_navigationStack.Count == 0 ? pageViewModel != HomePage : _navigationStack[^1] != pageViewModel)
		{
			int oldStackLength = _navigationStack.Count;
			if (oldStackLength > 0)
			{
				if (_currentPageIndex < 0) _navigationStack.Clear();
				else _navigationStack.RemoveRange(++_currentPageIndex, oldStackLength - _currentPageIndex);
			}
			_currentPageIndex = _navigationStack.Count;
			_navigationStack.Add(pageViewModel);
			NotifyPropertyChanged(ChangedProperty.CurrentPage);
			if (_currentPageIndex == 0) NotifyPropertyChanged(ChangedProperty.CanNavigateBack);
			if (oldStackLength >= _navigationStack.Count) NotifyPropertyChanged(ChangedProperty.CanNavigateForward);
		}
	}

	private void GoBack()
	{
		if (_currentPageIndex >= 0)
		{
			_currentPageIndex--;
			NotifyPropertyChanged(ChangedProperty.CurrentPage);
			if (_currentPageIndex < 0) NotifyPropertyChanged(ChangedProperty.CanNavigateBack);
			if (_navigationStack.Count - _currentPageIndex == 2) NotifyPropertyChanged(ChangedProperty.CanNavigateForward);
		}
	}

	private void GoForward()
	{
		if ((uint)(_currentPageIndex + 1) < (uint)_navigationStack.Count)
		{
			_currentPageIndex++;
			NotifyPropertyChanged(ChangedProperty.CurrentPage);
			if (_currentPageIndex == 0) NotifyPropertyChanged(ChangedProperty.CanNavigateBack);
			if (_navigationStack.Count - _currentPageIndex == 1) NotifyPropertyChanged(ChangedProperty.CanNavigateForward);
		}
	}

	void INotificationSystem.PublishNotification(NotificationSeverity severity, string title, string message)
		=> _notifications.Add(new(_notifications, severity, title, message));
}
