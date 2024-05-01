using System.ComponentModel;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class SettingsViewModel : BindableObject, IConnectedState
{
	public SettingsServiceConnectionManager ConnectionManager { get; }
	private readonly IEditionService _editionService;
	private readonly DevicesViewModel _devicesViewModel;
	private readonly LightingViewModel _lightingViewModel;
	private readonly SensorsViewModel _sensorsViewModel;
	private readonly ProgrammingViewModel _programmingViewModel;
	private readonly CustomMenuViewModel _customMenuViewModel;
	private string? _icon;
	private string _title;

	public SettingsViewModel(IEditionService editionService)
	{
		ConnectionManager = new("Local\\Exo.Service.Configuration", 100);
		_editionService = editionService;
		_devicesViewModel = new(ConnectionManager);
		_lightingViewModel = new(ConnectionManager, _devicesViewModel, _editionService);
		_sensorsViewModel = new(ConnectionManager, _devicesViewModel);
		_programmingViewModel = new(ConnectionManager);
		_customMenuViewModel = new();
		_icon = string.Empty;
		_title = string.Empty;
		ConnectionManager.RegisterStateAsync(this).GetAwaiter().GetResult();
	}

	public DevicesViewModel Devices => _devicesViewModel;
	public LightingViewModel Lighting => _lightingViewModel;
	public SensorsViewModel Sensors => _sensorsViewModel;
	public ProgrammingViewModel Programming => _programmingViewModel;
	public CustomMenuViewModel CustomMenu => _customMenuViewModel;
	public IEditionService EditionService => _editionService;

	public string? Icon
	{
		get => _icon;
		set => SetValue(ref _icon, value);
	}

	public string Title
	{
		get => _title;
		set => SetValue(ref _title, value);
	}

	public bool IsConnected => ConnectionManager.IsConnected;

	public Task RunAsync(CancellationToken cancellationToken)
	{
		NotifyPropertyChanged(ChangedProperty.IsConnected);
		return Task.CompletedTask;
	}

	public void Reset()
	{
		NotifyPropertyChanged(ChangedProperty.IsConnected);
	}
}
