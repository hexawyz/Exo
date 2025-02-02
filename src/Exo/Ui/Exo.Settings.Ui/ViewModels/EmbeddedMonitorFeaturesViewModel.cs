using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class EmbeddedMonitorFeaturesViewModel : BindableObject, IDisposable
{
	private readonly DeviceViewModel _device;
	private readonly ReadOnlyObservableCollection<ImageViewModel> _availableImages;
	private readonly IRasterizationScaleProvider _rasterizationScaleProvider;
	private readonly ISettingsMetadataService _metadataService;
	private readonly IEmbeddedMonitorService _embeddedMonitorService;
	private readonly ObservableCollection<EmbeddedMonitorViewModel> _embeddedMonitors;
	private readonly ReadOnlyObservableCollection<EmbeddedMonitorViewModel> _readOnlyEmbeddedMonitors;
	private readonly Dictionary<Guid, EmbeddedMonitorViewModel> _embeddedMonitorById;
	private bool _isExpanded;
	private readonly PropertyChangedEventHandler _onRasterizationScaleProviderPropertyChanged;

	public EmbeddedMonitorFeaturesViewModel
	(
		DeviceViewModel device,
		ReadOnlyObservableCollection<ImageViewModel> availableImages,
		IRasterizationScaleProvider rasterizationScaleProvider,
		ISettingsMetadataService metadataService,
		IEmbeddedMonitorService embeddedMonitorService
	)
	{
		_device = device;
		_availableImages = availableImages;
		_rasterizationScaleProvider = rasterizationScaleProvider;
		_metadataService = metadataService;
		_embeddedMonitorService = embeddedMonitorService;
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

	public Guid DeviceId => _device.Id;

	public ReadOnlyObservableCollection<EmbeddedMonitorViewModel> EmbeddedMonitors => _readOnlyEmbeddedMonitors;
	public ReadOnlyObservableCollection<ImageViewModel> AvailableImages => _availableImages;

	internal IRasterizationScaleProvider RasterizationScaleProvider => _rasterizationScaleProvider;

	internal ISettingsMetadataService MetadataService => _metadataService;

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	internal IEmbeddedMonitorService EmbeddedMonitorService => _embeddedMonitorService;

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

internal sealed class EmbeddedMonitorViewModel : ApplicableResettableBindableObject
{
	private readonly EmbeddedMonitorFeaturesViewModel _owner;
	private readonly Guid _monitorId;
	private MonitorShape _shape;
	private Size _imageSize;
	private EmbeddedMonitorCapabilities _capabilities;
	private readonly ObservableCollection<EmbeddedMonitorGraphicsViewModel> _supportedGraphics;
	private readonly ReadOnlyObservableCollection<EmbeddedMonitorGraphicsViewModel> _readOnlySupportedGraphics;
	private EmbeddedMonitorGraphicsViewModel? _initialCurrentGraphics;
	private EmbeddedMonitorGraphicsViewModel? _currentGraphics;

	public EmbeddedMonitorViewModel(EmbeddedMonitorFeaturesViewModel owner, EmbeddedMonitorInformation information)
	{
		_owner = owner;
		_monitorId = information.MonitorId;
		_shape = information.Shape;
		_imageSize = information.ImageSize;
		_capabilities = information.Capabilities;
		_supportedGraphics = new();
		EmbeddedMonitorImageGraphicsViewModel? imageGraphics = null;
		foreach (var graphics in information.SupportedGraphics)
		{
			if (graphics.GraphicsId == default)
			{
				imageGraphics = new EmbeddedMonitorImageGraphicsViewModel(this, graphics);
				_supportedGraphics.Add(imageGraphics);
			}
			else
			{
				_supportedGraphics.Add(new EmbeddedMonitorBuiltInGraphicsViewModel(this, graphics));
			}
		}
		_readOnlySupportedGraphics = new(_supportedGraphics);
		if (_supportedGraphics.Count > 0)
		{
			_initialCurrentGraphics = _currentGraphics = imageGraphics ?? _supportedGraphics[0];
		}
	}

	private bool IsChangedExceptGraphics => !ReferenceEquals(_initialCurrentGraphics, _currentGraphics);
	public override bool IsChanged => IsChangedExceptGraphics || CurrentGraphics?.IsChanged == true;
	protected override bool CanApply => IsChanged && _currentGraphics?.IsValid == true;

	public Guid MonitorId => _monitorId;

	public MonitorShape Shape
	{
		get => _shape;
		private set
		{
			if (value != _shape)
			{
				double oldAspectRatio = AspectRatio;
				_shape = value;
				NotifyPropertyChanged(ChangedProperty.Shape);
				if (AspectRatio != oldAspectRatio)
				{
					NotifyPropertyChanged(ChangedProperty.AspectRatio);
				}
			}
		}
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
	public double AspectRatio => _shape != MonitorShape.Rectangle && _imageSize.Height != 0 ? (double)_imageSize.Width / _imageSize.Height : 1;

	public ReadOnlyObservableCollection<EmbeddedMonitorGraphicsViewModel> SupportedGraphics => _readOnlySupportedGraphics;

	public EmbeddedMonitorGraphicsViewModel? CurrentGraphics
	{
		get => _currentGraphics;
		set
		{
			bool wasChanged = IsChanged;
			if (SetValue(ref _currentGraphics, value, ChangedProperty.CurrentGraphics))
			{
				OnChangeStateChange(wasChanged);
			}
		}
	}

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

	internal void NotifyGraphicsChanged(EmbeddedMonitorGraphicsViewModel graphics, bool isChanged)
	{
		if (ReferenceEquals(graphics, _currentGraphics) && !IsChangedExceptGraphics)
		{
			OnChanged(isChanged);
		}
	}

	internal EmbeddedMonitorFeaturesViewModel Owner => _owner;

	protected override async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		if (_currentGraphics is not null)
		{
			await _currentGraphics.ApplyAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	protected override void Reset()
	{
		CurrentGraphics = _initialCurrentGraphics;
	}
}

internal abstract class EmbeddedMonitorGraphicsViewModel : ChangeableBindableObject
{
	private readonly EmbeddedMonitorViewModel _monitor;
	private readonly Guid _id;
	private Guid _nameStringId;

	protected EmbeddedMonitorGraphicsViewModel(EmbeddedMonitorViewModel monitor, EmbeddedMonitorGraphicsDescription description)
	{
		_monitor = monitor;
		_id = description.GraphicsId;
		_nameStringId = description.NameStringId;
	}

	protected EmbeddedMonitorViewModel Monitor => _monitor;

	public Guid Id => _id;
	public string DisplayName => _monitor.Owner.MetadataService.GetString(CultureInfo.CurrentCulture, _nameStringId) ?? _id.ToString();

	public virtual bool IsValid => true;

	internal void UpdateInformation(EmbeddedMonitorGraphicsDescription description)
	{
		if (description.NameStringId != _nameStringId)
		{
			_nameStringId = description.NameStringId;
			NotifyPropertyChanged(nameof(DisplayName));
		}
	}

	protected override void OnChanged(bool isChanged)
	{
		base.OnChanged(isChanged);
		Monitor.NotifyGraphicsChanged(this, isChanged);
	}

	internal abstract ValueTask ApplyAsync(CancellationToken cancellationToken);
}

internal sealed class EmbeddedMonitorBuiltInGraphicsViewModel : EmbeddedMonitorGraphicsViewModel
{
	public EmbeddedMonitorBuiltInGraphicsViewModel(EmbeddedMonitorViewModel monitor, EmbeddedMonitorGraphicsDescription description)
		: base(monitor, description)
	{
	}

	public override bool IsChanged => false;

	internal override async ValueTask ApplyAsync(CancellationToken cancellationToken)
		=> await Monitor.Owner.EmbeddedMonitorService.SetBuiltInGraphicsAsync
		(
			new()
			{
				DeviceId = Monitor.Owner.DeviceId,
				MonitorId = Monitor.MonitorId,
				GraphicsId = Id 
			},
			cancellationToken
		).ConfigureAwait(false);
}

internal sealed class EmbeddedMonitorImageGraphicsViewModel : EmbeddedMonitorGraphicsViewModel, IDisposable
{
	private UInt128 _initialImageId;
	private Rectangle _cropRectangle;
	private ImageViewModel? _image;
	private readonly PropertyChangedEventHandler _onMonitorPropertyChanged;

	public EmbeddedMonitorImageGraphicsViewModel(EmbeddedMonitorViewModel monitor, EmbeddedMonitorGraphicsDescription description)
		: base(monitor, description)
	{
		_initialImageId = 0;
		_onMonitorPropertyChanged = OnMonitorPropertyChanged;
		monitor.PropertyChanged += _onMonitorPropertyChanged;
	}

	public void Dispose()
	{
		Monitor.PropertyChanged -= _onMonitorPropertyChanged;
	}

	private void OnMonitorPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (Equals(e, ChangedProperty.DisplayWidth) ||
			Equals(e, ChangedProperty.DisplayHeight) ||
			Equals(e, ChangedProperty.DisplayWidth) ||
			Equals(e, ChangedProperty.Shape) ||
			Equals(e, ChangedProperty.ImageSize))
		{
			NotifyPropertyChanged(e);
		}
	}

	public override bool IsChanged => (_image?.Id).GetValueOrDefault() != _initialImageId;
	public override bool IsValid => _image is not null && _cropRectangle.Width > 0 && _cropRectangle.Height > 0 && (double)_cropRectangle.Width / _cropRectangle.Height == AspectRatio;

	public ImageViewModel? Image
	{
		get => _image;
		set
		{
			bool wasChanged = IsChanged;
			if (SetValue(ref _image, value, ChangedProperty.Image))
			{
				OnChangeStateChange(wasChanged);
			}
		}
	}

	public MonitorShape Shape => Monitor.Shape;

	public Size ImageSize => Monitor.ImageSize;

	public double DisplayWidth => Monitor.DisplayWidth;
	public double DisplayHeight => Monitor.DisplayHeight;

	public Rectangle CropRectangle
	{
		get => _cropRectangle;
		set
		{
			SetValue(ref _cropRectangle, value, ChangedProperty.CropRectangle);
		}
	}

	public double AspectRatio => Monitor.AspectRatio;

	public ReadOnlyObservableCollection<ImageViewModel> AvailableImages => Monitor.Owner.AvailableImages;

	internal override ValueTask ApplyAsync(CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
}
