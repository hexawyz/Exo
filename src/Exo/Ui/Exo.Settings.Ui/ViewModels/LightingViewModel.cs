using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;
using Exo.Lighting;
using Exo.Metadata;
using Exo.Service;
using Exo.Settings.Ui.Json;
using Exo.Settings.Ui.Models;
using Exo.Settings.Ui.Services;
using Exo.Ui;
using Microsoft.Extensions.Logging;
using WinRT;
using ILightingService = Exo.Settings.Ui.Services.ILightingService;

namespace Exo.Settings.Ui.ViewModels;

[GeneratedBindableCustomProperty]
internal sealed partial class LightingViewModel : BindableObject, IAsyncDisposable
{
	private readonly DevicesViewModel _devicesViewModel;
	private readonly ISettingsMetadataService _metadataService;
	private ILightingService? _lightingService;
	private readonly ObservableCollection<LightingDeviceViewModel> _lightingDevices;
	private readonly Dictionary<Guid, LightingDeviceViewModel> _lightingDeviceById;
	private readonly ConcurrentDictionary<Guid, LightingEffectViewModel> _effectViewModelById;
	private readonly Dictionary<Guid, LightingDeviceConfiguration> _pendingConfigurationUpdates;
	private readonly Dictionary<Guid, LightingDeviceInformation> _pendingDeviceInformations;
	private readonly LightingZoneViewModel _globalLightingZone;
	private readonly ILogger<LightingDeviceViewModel> _lightingDeviceLogger;
	private readonly INotificationSystem _notificationSystem;
	private readonly IFileOpenDialog _fileOpenDialog;
	private readonly IFileSaveDialog _fileSaveDialog;
	private readonly Commands.ApplyChangesCommand _applyChangesCommand;
	private readonly Commands.ResetChangesCommand _resetChangesCommand;
	private readonly Commands.ExportConfigurationCommand _exportConfigurationCommand;
	private readonly Commands.ImportConfigurationCommand _importConfigurationCommand;

	private bool _isReady;
	private bool _useGlobalLighting;

	private readonly CancellationTokenSource _cancellationTokenSource;

	public ObservableCollection<LightingDeviceViewModel> LightingDevices => _lightingDevices;
	public ILightingService? LightingService => _lightingService;
	public ICommand ApplyChangesCommand => _applyChangesCommand;
	public ICommand ResetChangesCommand => _resetChangesCommand;
	public ICommand ExportConfigurationCommand => _exportConfigurationCommand;
	public ICommand ImportConfigurationCommand => _importConfigurationCommand;

	public bool IsReady
	{
		get => _isReady;
		private set => SetValue(ref _isReady, value, ChangedProperty.IsReady);
	}

	public bool UseGlobalLighting
	{
		get => _useGlobalLighting;
		set => SetValue(ref _useGlobalLighting, value, ChangedProperty.UseGlobalLighting);
	}

	public LightingZoneViewModel GlobalLightingZone => _globalLightingZone;

	public LightingViewModel(ITypedLoggerProvider loggerProvider, DevicesViewModel devicesViewModel, ISettingsMetadataService metadataService, INotificationSystem notificationSystem, IFileOpenDialog fileOpenDialog, IFileSaveDialog fileSaveDialog)
	{
		_lightingDeviceLogger = loggerProvider.GetLogger<LightingDeviceViewModel>();
		_notificationSystem = notificationSystem;
		_fileOpenDialog = fileOpenDialog;
		_fileSaveDialog = fileSaveDialog;
		_devicesViewModel = devicesViewModel;
		_metadataService = metadataService;
		_lightingDevices = new();
		_lightingDeviceById = new();
		_effectViewModelById = new();
		_globalLightingZone = new(this, null, new(default, []), "Global Lighting", 0, LightingZoneComponentType.Unknown, LightingZoneShape.Other);
		_pendingConfigurationUpdates = new();
		_pendingDeviceInformations = new();
		_applyChangesCommand = new(this);
		_resetChangesCommand = new(this);
		_exportConfigurationCommand = new(this);
		_importConfigurationCommand = new(this);
		_cancellationTokenSource = new CancellationTokenSource();
		_devicesViewModel.Devices.CollectionChanged += OnDevicesCollectionChanged;
	}

	internal void OnConnected(ILightingService lightingService)
	{
		_lightingService = lightingService;
	}

	internal void OnConnectionReset()
	{
		_lightingDeviceById.Clear();
		_effectViewModelById.Clear();
		_pendingConfigurationUpdates.Clear();
		_pendingDeviceInformations.Clear();

		foreach (var device in _lightingDevices)
		{
			device.Dispose();
		}

		_lightingDevices.Clear();

		_lightingService = null;
		UseGlobalLighting = false;
	}

	public ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		return ValueTask.CompletedTask;
	}

	private void OnDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add)
		{
			var vm = (DeviceViewModel)e.NewItems![0]!;
			if (_pendingDeviceInformations.Remove(vm.Id, out var info))
			{
				OnDeviceAdded(vm, info);
			}
		}
		else if (e.Action == NotifyCollectionChangedAction.Remove)
		{
			var vm = (DeviceViewModel)e.OldItems![0]!;
			if (!_pendingDeviceInformations.Remove(vm.Id))
			{
				OnDeviceRemoved(vm.Id);
			}
		}
		else if (e.Action == NotifyCollectionChangedAction.Reset)
		{
			// Reset will only be triggered when the service connection is reset. In that case, the change will be handled in the appropriate reset code for this component.
		}
		else
		{
			// As of writing this code, we don't require support for anything else, but if this change in the future, this exception will be triggered.
			throw new InvalidOperationException("This case is not handled.");
		}
	}

	private void OnDeviceAdded(DeviceViewModel device, LightingDeviceInformation lightingDeviceInformation)
	{
		var vm = new LightingDeviceViewModel(_lightingDeviceLogger, this, device, lightingDeviceInformation, _notificationSystem);
		_lightingDevices.Add(vm);
		_lightingDeviceById[vm.Id] = vm;
		if (_pendingConfigurationUpdates.Remove(lightingDeviceInformation.DeviceId, out var configuration))
		{
			vm.OnDeviceConfigurationUpdated(in configuration);
		}
	}

	private void OnDeviceRemoved(Guid deviceId)
	{
		for (int i = 0; i < _lightingDevices.Count; i++)
		{
			var vm = _lightingDevices[i];
			if (_lightingDevices[i].Id == deviceId)
			{
				_lightingDevices.RemoveAt(i);
				_lightingDeviceById.Remove(vm.Id);
				break;
			}
		}
	}

	internal void OnLightingDevice(LightingDeviceInformation info)
	{
		if (_lightingDeviceById.TryGetValue(info.DeviceId, out var vm))
		{
			vm.UpdateInformation(info);
		}
		else
		{
			if (_devicesViewModel.TryGetDevice(info.DeviceId, out var device))
			{
				OnDeviceAdded(device, info);
			}
			else if (!_devicesViewModel.IsRemovedId(info.DeviceId))
			{
				_pendingDeviceInformations[info.DeviceId] = info;
			}
		}
	}

	internal void OnLightingConfigurationUpdate(in LightingDeviceConfiguration configuration)
	{
		if (_lightingDeviceById.TryGetValue(configuration.DeviceId, out var vm))
		{
			vm.OnDeviceConfigurationUpdated(in configuration);
		}
		else
		{
			_pendingConfigurationUpdates[configuration.DeviceId] = configuration;
		}
	}

	internal void CacheEffectInformation(LightingEffectInformation effectInformation)
	{
		if (_effectViewModelById.TryGetValue(effectInformation.EffectId, out var vm))
		{
			// NB: This is imperfect. If an effect is currently selected, it won't recreate the properties.
			vm.OnMetadataUpdated(effectInformation);
		}
		else
		{
			string? displayName = null;
			uint displayOrder = uint.MaxValue;
			if (_metadataService.TryGetLightingEffectMetadata("", "", effectInformation.EffectId, out var metadata))
			{
				displayName = _metadataService.GetString(CultureInfo.CurrentCulture, metadata.NameStringId);
				displayOrder = metadata.DisplayOrder;
			}
			displayName ??= string.Create(CultureInfo.InvariantCulture, $"Effect {effectInformation.EffectId:B}.");

			_effectViewModelById.TryAdd(effectInformation.EffectId, new(effectInformation, displayName, displayOrder));
		}
	}

	public LightingEffectViewModel GetEffect(Guid effectId)
		=> _effectViewModelById.TryGetValue(effectId, out var effect) ? effect : throw new InvalidOperationException("Missing effect information.");

	public (string DisplayName, uint DisplayOrder, LightingZoneComponentType ComponentType, LightingZoneShape Shape) GetZoneMetadata(Guid zoneId)
	{
		string? displayName = null;
		uint displayOrder = 0;
		LightingZoneComponentType componentType = 0;
		LightingZoneShape shape = 0;
		if (_metadataService.TryGetLightingZoneMetadata("", "", zoneId, out var metadata))
		{
			displayName = _metadataService.GetString(CultureInfo.CurrentCulture, metadata.NameStringId);
			displayOrder = metadata.DisplayOrder;
			componentType = metadata.ComponentType;
			shape = metadata.Shape;
		}
		return (displayName ?? $"Unknown {zoneId:B}", displayOrder, componentType, shape);
	}

	private async Task ExportConfigurationAsync(CancellationToken cancellationToken)
	{
		if (await _fileSaveDialog.ChooseAsync([("Lighting Configuration", ".light")]) is { } file)
		{
			using var stream = await file.OpenForWriteAsync();

			var devices = new Dictionary<Guid, DeviceLightingConfiguration>();
			foreach (var device in _lightingDevices)
			{
				if (device.GetLightingConfiguration() is { } configuration) devices.Add(device.Id, configuration);
			}

			await JsonSerializer.SerializeAsync(stream, new LightingConfiguration(false, devices), SourceGenerationContext.Default.LightingConfiguration);
		}
	}

	private async Task ImportConfigurationAsync(CancellationToken cancellationToken)
	{
		if (await _fileOpenDialog.OpenAsync([".light"]) is { } file)
		{
			using (var stream = await file.OpenForReadAsync())
			{
				var configuration = await JsonSerializer.DeserializeAsync(stream, SourceGenerationContext.Default.LightingConfiguration);
				foreach (var (deviceId, deviceConfiguration) in configuration.Devices)
				{
					if (!_lightingDeviceById.TryGetValue(deviceId, out var device)) continue;
					device.SetLightingConfiguration(deviceConfiguration);
				}
			}
		}
	}

	private async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		if (_useGlobalLighting)
		{
		}
		else
		{
			await Task.WhenAll(_lightingDevices.Select(device => device.ApplyChangesAsync(cancellationToken)));
		}
	}

	private void ResetChanges()
	{
		if (_useGlobalLighting)
		{
			_globalLightingZone.ResetChanges();
		}
		else
		{
			foreach (var device in _lightingDevices)
			{
				device.ResetChanges();
			}
		}
	}

	private static partial class Commands
	{
		[GeneratedBindableCustomProperty]
		public sealed partial class ApplyChangesCommand(LightingViewModel owner) : ICommand
		{
			private readonly LightingViewModel _owner = owner;

			bool ICommand.CanExecute(object? parameter) => true;

			async void ICommand.Execute(object? parameter)
			{
				try
				{
					await _owner.ApplyChangesAsync(default);
				}
				catch
				{
				}
			}

			private event EventHandler? CanExecuteChanged;

			event EventHandler? ICommand.CanExecuteChanged
			{
				add => CanExecuteChanged += value;
				remove => CanExecuteChanged -= value;
			}
		}

		[GeneratedBindableCustomProperty]
		public sealed partial class ResetChangesCommand(LightingViewModel owner) : ICommand
		{
			private readonly LightingViewModel _owner = owner;

			bool ICommand.CanExecute(object? parameter) => true;

			void ICommand.Execute(object? parameter)
			{
				try
				{
					_owner.ResetChanges();
				}
				catch
				{
				}
			}

			private event EventHandler? CanExecuteChanged;

			event EventHandler? ICommand.CanExecuteChanged
			{
				add => CanExecuteChanged += value;
				remove => CanExecuteChanged -= value;
			}
		}

		[GeneratedBindableCustomProperty]
		public sealed partial class ExportConfigurationCommand(LightingViewModel owner) : ICommand
		{
			private readonly LightingViewModel _owner = owner;

			bool ICommand.CanExecute(object? parameter) => true;

			async void ICommand.Execute(object? parameter)
			{
				try
				{
					await _owner.ExportConfigurationAsync(default);
				}
				catch
				{
				}
			}

			event EventHandler? ICommand.CanExecuteChanged
			{
				add { }
				remove { }
			}
		}

		[GeneratedBindableCustomProperty]
		public sealed partial class ImportConfigurationCommand(LightingViewModel owner) : ICommand
		{
			private readonly LightingViewModel _owner = owner;

			bool ICommand.CanExecute(object? parameter) => true;

			async void ICommand.Execute(object? parameter)
			{
				try
				{
					await _owner.ImportConfigurationAsync(default);
				}
				catch
				{
				}
			}

			event EventHandler? ICommand.CanExecuteChanged
			{
				add { }
				remove { }
			}
		}
	}
}
