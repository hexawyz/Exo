using System.Collections.ObjectModel;
using System.ComponentModel;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class EmbeddedMonitorFeaturesViewModel : BindableObject, IDisposable
{
	private readonly DeviceViewModel _device;
	private readonly ReadOnlyObservableCollection<ImageViewModel> _availableImages;
	private readonly IRasterizationScaleProvider _rasterizationScaleProvider;
	private readonly ObservableCollection<EmbeddedMonitorViewModel> _embeddedMonitors;
	private readonly ReadOnlyObservableCollection<EmbeddedMonitorViewModel> _readOnlyEmbeddedMonitors;
	private readonly Dictionary<Guid, EmbeddedMonitorViewModel> _embeddedMonitorById;
	private bool _isExpanded;
	private readonly PropertyChangedEventHandler _onRasterizationScaleProviderPropertyChanged;

	public EmbeddedMonitorFeaturesViewModel(DeviceViewModel device, ReadOnlyObservableCollection<ImageViewModel> availableImages, IRasterizationScaleProvider rasterizationScaleProvider)
	{
		_device = device;
		_availableImages = availableImages;
		_rasterizationScaleProvider = rasterizationScaleProvider;
		_embeddedMonitors = new();
		_embeddedMonitorById = new();
		_readOnlyEmbeddedMonitors = new(_embeddedMonitors);
		_onRasterizationScaleProviderPropertyChanged = OnRasterizationScaleProviderPropertyChanged;
		rasterizationScaleProvider.PropertyChanged += _onRasterizationScaleProviderPropertyChanged;
	}

	public void Dispose() => _rasterizationScaleProvider.PropertyChanged -= _onRasterizationScaleProviderPropertyChanged;

	private void OnRasterizationScaleProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		foreach (var monitor in _embeddedMonitors)
		{
			monitor.NotifyDpiChange();
		}
	}

	public ReadOnlyObservableCollection<EmbeddedMonitorViewModel> EmbeddedMonitors => _readOnlyEmbeddedMonitors;
	public ReadOnlyObservableCollection<ImageViewModel> AvailableImages => _availableImages;

	internal IRasterizationScaleProvider RasterizationScaleProvider => _rasterizationScaleProvider;

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	public void UpdateInformation(EmbeddedMonitorDeviceInformation information)
	{
		var monitorIds = new HashSet<Guid>();
		foreach (var monitorInformation in information.EmbeddedMonitors)
		{
			monitorIds.Add(monitorInformation.MonitorId);
		}
		for (int i = 0; i < _embeddedMonitors.Count; i++)
		{
			var embeddedMonitor = _embeddedMonitors[i];
			if (!monitorIds.Contains(embeddedMonitor.MonitorId))
			{
				_embeddedMonitors.RemoveAt(i--);
				_embeddedMonitorById.Remove(embeddedMonitor.MonitorId);
			}
		}
		foreach (var monitorInformation in information.EmbeddedMonitors)
		{
			if (_embeddedMonitorById.TryGetValue(monitorInformation.MonitorId, out var vm))
			{
				vm.UpdateInformation(monitorInformation);
			}
			else
			{
				vm = new(this, monitorInformation);
				_embeddedMonitorById.Add(monitorInformation.MonitorId, vm);
				_embeddedMonitors.Add(vm);
			}
		}
	}
}

internal sealed class EmbeddedMonitorViewModel : BindableObject
{
	private readonly EmbeddedMonitorFeaturesViewModel _owner;
	private readonly Guid _monitorId;
	private MonitorShape _shape;
	private Size _imageSize;
	private EmbeddedMonitorCapabilities _capabilities;
	private readonly ObservableCollection<EmbeddedMonitorGraphicsViewModel> _supportedGraphics;
	private readonly ReadOnlyObservableCollection<EmbeddedMonitorGraphicsViewModel> _readOnlySupportedGraphics;

	private ImageViewModel? _image;

	public EmbeddedMonitorViewModel(EmbeddedMonitorFeaturesViewModel owner, EmbeddedMonitorInformation information)
	{
		_owner = owner;
		_monitorId = information.MonitorId;
		_shape = information.Shape;
		_imageSize = information.ImageSize;
		_capabilities = information.Capabilities;
		_supportedGraphics = new();
		_readOnlySupportedGraphics = new(_supportedGraphics);
	}

	public Guid MonitorId => _monitorId;

	public MonitorShape Shape
	{
		get => _shape;
		private set => SetValue(ref _shape, value, ChangedProperty.Shape);
	}

	public Size ImageSize
	{
		get => _imageSize;
		private set
		{
			bool widthChanged = value.Width != _imageSize.Width;
			bool heightChanged = value.Height != _imageSize.Height;

			if (widthChanged | heightChanged)
			{
				_imageSize = value;
				NotifyPropertyChanged(ChangedProperty.ImageSize);

				if (widthChanged) NotifyPropertyChanged(ChangedProperty.DisplayWidth);
				if (heightChanged) NotifyPropertyChanged(ChangedProperty.DisplayHeight);
			}
		}
	}

	private EmbeddedMonitorCapabilities Capabilities
	{
		get => _capabilities;
		set
		{
			if (value != _capabilities)
			{
				var changedCapabilities = value ^ _capabilities;
				_capabilities = value;
				if ((changedCapabilities & EmbeddedMonitorCapabilities.BuiltInGraphics) != 0) NotifyPropertyChanged(ChangedProperty.HasBuiltInGraphics);
			}
		}
	}

	public bool HasBuiltInGraphics => (_capabilities & EmbeddedMonitorCapabilities.BuiltInGraphics) != 0;

	public double DisplayWidth => _imageSize.Width / _owner.RasterizationScaleProvider.RasterizationScale;
	public double DisplayHeight => _imageSize.Height / _owner.RasterizationScaleProvider.RasterizationScale;

	public ImageViewModel? Image
	{
		get => _image;
		set => SetValue(ref _image, value, ChangedProperty.Image);
	}

	public ReadOnlyObservableCollection<ImageViewModel> AvailableImages => _owner.AvailableImages;
	public ReadOnlyObservableCollection<EmbeddedMonitorGraphicsViewModel> SupportedGraphics => _readOnlySupportedGraphics;

	internal void UpdateInformation(EmbeddedMonitorInformation information)
	{
		Shape = information.Shape;
		ImageSize = information.ImageSize;
	}

	internal void NotifyDpiChange()
	{
		NotifyPropertyChanged(ChangedProperty.DisplayWidth);
		NotifyPropertyChanged(ChangedProperty.DisplayHeight);
	}
}

internal sealed class EmbeddedMonitorGraphicsViewModel
{
	public string DisplayName => "";
}
