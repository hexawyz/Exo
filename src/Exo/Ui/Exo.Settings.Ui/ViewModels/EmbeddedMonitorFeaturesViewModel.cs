using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Exo.EmbeddedMonitors;
using Exo.Images;
using Exo.Monitors;
using Exo.Service;
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
	private readonly Dictionary<Guid, EmbeddedMonitorConfiguration> _pendingConfigurationUpdates;
	private bool _isExpanded;
	private readonly PropertyChangedEventHandler _onRasterizationScaleProviderPropertyChanged;
	private EmbeddedMonitorViewModel? _selectedMonitor;
	private readonly INotificationSystem _notificationSystem;

	public EmbeddedMonitorFeaturesViewModel
	(
		ITypedLoggerProvider loggerProvider,
		DeviceViewModel device,
		ReadOnlyObservableCollection<ImageViewModel> availableImages,
		IRasterizationScaleProvider rasterizationScaleProvider,
		ISettingsMetadataService metadataService,
		IEmbeddedMonitorService embeddedMonitorService,
		INotificationSystem notificationSystem
	)
	{
		_device = device;
		_availableImages = availableImages;
		_rasterizationScaleProvider = rasterizationScaleProvider;
		_metadataService = metadataService;
		_embeddedMonitorService = embeddedMonitorService;
		_notificationSystem = notificationSystem;
		_embeddedMonitors = new();
		_embeddedMonitorById = new();
		_pendingConfigurationUpdates = new();
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

	internal DeviceViewModel Device => _device;

	internal IRasterizationScaleProvider RasterizationScaleProvider => _rasterizationScaleProvider;

	internal ISettingsMetadataService MetadataService => _metadataService;

	internal INotificationSystem NotificationSystem => _notificationSystem;

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	// This is used when showing monitors in a grid.
	public EmbeddedMonitorViewModel? SelectedMonitor
	{
		get => _selectedMonitor;
		set => SetValue(ref _selectedMonitor, value, ChangedProperty.SelectedMonitor);
	}

	internal IEmbeddedMonitorService EmbeddedMonitorService => _embeddedMonitorService;

	internal void UpdateInformation(EmbeddedMonitorDeviceInformation information)
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
			if (_pendingConfigurationUpdates.Remove(monitorInformation.MonitorId, out var configuration))
			{
				vm.UpdateConfiguration(configuration);
			}
		}
	}

	internal void UpdateConfiguration(EmbeddedMonitorConfiguration configuration)
	{
		if (_embeddedMonitorById.TryGetValue(configuration.MonitorId, out var monitor))
		{
			monitor.UpdateConfiguration(configuration);
		}
		else
		{
			_pendingConfigurationUpdates.Add(configuration.MonitorId, configuration);
		}
	}
}

internal sealed class EmbeddedMonitorViewModel : ApplicableResettableBindableObject
{
	private readonly EmbeddedMonitorFeaturesViewModel _owner;
	private readonly Guid _monitorId;
	private MonitorShape _shape;
	private Size _imageSize;
	private UnsignedRationalNumber16 _aspectRatio;
	private EmbeddedMonitorCapabilities _capabilities;
	private readonly ObservableCollection<EmbeddedMonitorGraphicsViewModel> _supportedGraphics;
	private readonly ReadOnlyObservableCollection<EmbeddedMonitorGraphicsViewModel> _readOnlySupportedGraphics;
	private Guid _initialCurrentGraphicsId;
	private EmbeddedMonitorGraphicsViewModel? _currentGraphics;
	private bool _isReady;

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
			_currentGraphics = imageGraphics ?? _supportedGraphics[0];
			_initialCurrentGraphicsId = _currentGraphics.Id;
		}
		_isReady = true;
		_aspectRatio = new(1, 1);
	}

	private bool IsChangedNonRecursive => _currentGraphics?.Id != _initialCurrentGraphicsId;
	public override bool IsChanged => IsChangedNonRecursive || CurrentGraphics?.IsChanged == true;
	protected override bool CanApply => IsChanged && _currentGraphics?.IsValid == true;

	public bool IsNotBusy
	{
		get => _isReady;
		private set => SetValue(ref _isReady, value, ChangedProperty.IsNotBusy);
	}

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
				_aspectRatio = value.Height == 0 ?
					UnsignedRationalNumber16.One :
					UnsignedRationalNumber16.Reduce((ushort)_imageSize.Width, (ushort)_imageSize.Height);
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

	public UnsignedRationalNumber16 RationalAspectRatio => _aspectRatio;

	public ReadOnlyObservableCollection<EmbeddedMonitorGraphicsViewModel> SupportedGraphics => _readOnlySupportedGraphics;

	public EmbeddedMonitorGraphicsViewModel? CurrentGraphics
	{
		get => _currentGraphics;
		set
		{
			// Need to ignore null values for the 🤬 bindings behaving completely irrationally.
			if (value is null) return;
			SetChangeableValue(ref _currentGraphics, value, ChangedProperty.CurrentGraphics);
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
		if (ReferenceEquals(graphics, _currentGraphics) && !IsChangedNonRecursive)
		{
			OnChanged(isChanged);
		}
	}

	internal EmbeddedMonitorFeaturesViewModel Owner => _owner;

	protected override async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		if (_currentGraphics is not null)
		{
			IsNotBusy = false;
			try
			{
				await _currentGraphics.ApplyAsync(cancellationToken);
			}
			finally
			{
				IsNotBusy = true;
			}
		}
	}

	protected override void Reset()
	{
		foreach (var supportedGraphics in _supportedGraphics)
		{
			if (supportedGraphics.Id == _initialCurrentGraphicsId)
			{
				CurrentGraphics = supportedGraphics;
				break;
			}
		}
		(_currentGraphics as IResettable)?.Reset();
	}

	internal void UpdateConfiguration(EmbeddedMonitorConfiguration configuration)
	{
		bool wasChanged = IsChanged;

		EmbeddedMonitorGraphicsViewModel? newGraphics = null;
		if (_currentGraphics?.Id == configuration.GraphicsId)
		{
			newGraphics = _currentGraphics;
		}
		else
		{
			foreach (var supportedGraphics in _supportedGraphics)
			{
				if (supportedGraphics.Id == configuration.GraphicsId)
				{
					newGraphics = supportedGraphics;
					break;
				}
			}
		}

		// Just as a small optimization, we update the contents of the graphics first, so that in the event where it is not the one displayed, we will avoid unnecessary UI updates.
		if (newGraphics is not null && configuration.GraphicsId == default)
		{
			((EmbeddedMonitorImageGraphicsViewModel)newGraphics).UpdateConfiguration(configuration.ImageId, configuration.ImageRegion);
		}

		if (_initialCurrentGraphicsId != configuration.GraphicsId)
		{
			if (_currentGraphics is null ? newGraphics is not null : (_currentGraphics.Id == _initialCurrentGraphicsId && newGraphics is not null))
			{
				_currentGraphics = newGraphics;
				NotifyPropertyChanged(ChangedProperty.CurrentGraphics);
			}
			_initialCurrentGraphicsId = configuration.GraphicsId;
		}

		OnChangeStateChange(wasChanged);
	}
}

internal abstract class EmbeddedMonitorGraphicsViewModel : ResettableBindableObject
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

	protected override void Reset() { }
}

internal sealed class EmbeddedMonitorBuiltInGraphicsViewModel : EmbeddedMonitorGraphicsViewModel
{
	public EmbeddedMonitorBuiltInGraphicsViewModel(EmbeddedMonitorViewModel monitor, EmbeddedMonitorGraphicsDescription description)
		: base(monitor, description)
	{
	}

	public override bool IsChanged => false;

	internal override async ValueTask ApplyAsync(CancellationToken cancellationToken)
	{
		try
		{
			await Monitor.Owner.EmbeddedMonitorService.SetBuiltInGraphicsAsync
			(
				Monitor.Owner.DeviceId,
				Monitor.MonitorId,
				Id,
				cancellationToken
			);
		}
		catch (Exception ex)
		{
			Monitor.Owner.NotificationSystem.PublishError(ex, $"Failed to set the graphics of {Monitor.Owner.Device.FriendlyName}.");
		}
	}
}

internal sealed class EmbeddedMonitorImageGraphicsViewModel : EmbeddedMonitorGraphicsViewModel, IDisposable
{
	private static class Commands
	{
		public sealed class AutoCropCommand : ICommand
		{
			private readonly EmbeddedMonitorImageGraphicsViewModel _viewModel;

			public AutoCropCommand(EmbeddedMonitorImageGraphicsViewModel viewModel) => _viewModel = viewModel;

			public bool CanExecute(object? parameter) => _viewModel.Image != null;
			public void Execute(object? parameter) => _viewModel.SetAutomaticCropRegion(parameter is bool b && b);

			public event EventHandler? CanExecuteChanged;

			public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private UInt128 _initialImageId;
	private Rectangle _initialCropRectangle;
	private Rectangle _cropRectangle;
	private ImageViewModel? _image;
	private readonly PropertyChangedEventHandler _onMonitorPropertyChanged;
	private readonly Commands.AutoCropCommand _autoCropCommand;

	public EmbeddedMonitorImageGraphicsViewModel(EmbeddedMonitorViewModel monitor, EmbeddedMonitorGraphicsDescription description)
		: base(monitor, description)
	{
		_initialImageId = 0;
		_autoCropCommand = new(this);
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

	public ICommand AutoCropCommand => _autoCropCommand;

	public override bool IsChanged => (_image?.Id).GetValueOrDefault() != _initialImageId || _initialCropRectangle != _cropRectangle;

	public override bool IsValid => _image is not null && IsRegionValid(_cropRectangle);

	private bool IsRegionValid(Rectangle rectangle)
		=> rectangle.Width > 0 && rectangle.Height > 0 && (_image is null || rectangle.Width <= _image.Width && rectangle.Height <= _image.Height) && rectangle.Width * AspectRatio == rectangle.Height;

	public ImageViewModel? Image
	{
		get => _image;
		set
		{
			if (value != _image)
			{
				bool wasChanged = IsChanged;
				bool wasValid = IsValid;
				bool wasNull = _image is null;

				_image = value;

				NotifyPropertyChanged(ChangedProperty.Image);

				if (value is not null)
				{
					SetAutomaticCropRegion(false);
				}

				OnChangeStateChange(wasChanged);

				if (wasNull || value is null) _autoCropCommand.RaiseCanExecuteChanged();
				if (wasValid != IsValid) IApplicable.NotifyCanExecuteChanged();
			}
		}
	}

	public void SetAutomaticCropRegion(bool avoidUpsampling)
	{
		if (_image is not { } image) return;
		var targetSize = Monitor.ImageSize;

		var foundSize = avoidUpsampling && image.Width >= targetSize.Width && image.Height >= targetSize.Height ?
			targetSize :
			ComputeOptimalCropSize(new() { Width = image.Width, Height = image.Height }, Monitor.RationalAspectRatio);

		CropRectangle = new()
		{
			Left = (image.Width - foundSize.Width) >>> 1,
			Top = (image.Height - foundSize.Height) >>> 1,
			Width = foundSize.Width,
			Height = foundSize.Height,
		};
	}

	public static Size ComputeOptimalCropSize(Size imageSize, UnsignedRationalNumber16 aspectRatio)
	{
		uint n = Math.Min((uint)imageSize.Width / aspectRatio.P, (uint)imageSize.Height / aspectRatio.Q);
		return new() { Width = (int)(n * aspectRatio.P), Height = (int)(n * aspectRatio.Q) };
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
			var oldRectangle = _cropRectangle;
			if (value != oldRectangle)
			{
				bool wasChanged = IsChanged;
				_cropRectangle = value;
				NotifyPropertyChanged(ChangedProperty.CropRectangle);
				bool isChanged = IsChanged;
				OnChangeStateChange(wasChanged, isChanged);

				// Propagate the applicable status change. Trying to make this as efficient as possible by minimizing the computations.
				// Probably introducing a persisted IsValid flag would be better. (Also maybe just refactor the IApplicable stuff)
				if (isChanged == wasChanged && _image is not null)
				{
					if (IsRegionValid(oldRectangle) != IsRegionValid(value))
					{
						IApplicable.NotifyCanExecuteChanged();
					}
				}
			}
		}
	}

	public double AspectRatio => Monitor.AspectRatio;

	public ReadOnlyObservableCollection<ImageViewModel> AvailableImages => Monitor.Owner.AvailableImages;

	internal override async ValueTask ApplyAsync(CancellationToken cancellationToken)
	{
		if (_image is not { } image)
		{
			Monitor.Owner.NotificationSystem.PublishNotification(NotificationSeverity.Warning, $"Failed to set image for {Monitor.Owner.Device.FriendlyName}.", "No image is currently selected.");
			return;
		}

		var rectangle = _cropRectangle;

		try
		{
			await Monitor.Owner.EmbeddedMonitorService.SetImageAsync
			(
				Monitor.Owner.DeviceId,
				Monitor.MonitorId,
				image.Id,
				new(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height),
				cancellationToken
			);
		}
		catch (Exception ex)
		{
			Monitor.Owner.NotificationSystem.PublishError(ex, $"Failed to set image for {Monitor.Owner.Device.FriendlyName}.");
		}
	}

	internal void UpdateConfiguration(UInt128 imageId, Rectangle cropRegion)
	{
		bool wasChanged = IsChanged;
		bool imageChanged = false;
		bool cropRectangleChanged = false;
		if (_initialImageId != imageId)
		{
			if (_image is null || _image.Id == _initialImageId)
			{
				foreach (var image in AvailableImages)
				{
					if (image.Id == imageId)
					{
						_image = image;
						imageChanged = true;
						break;
					}
				}
			}
			_initialImageId = imageId;
		}
		if (_initialCropRectangle != cropRegion)
		{
			if (_cropRectangle == _initialCropRectangle)
			{
				_cropRectangle = cropRegion;
				cropRectangleChanged = true;
			}
			_initialCropRectangle = cropRegion;
		}
		if (imageChanged) NotifyPropertyChanged(ChangedProperty.Image);
		if (cropRectangleChanged) NotifyPropertyChanged(ChangedProperty.CropRectangle);
		OnChangeStateChange(wasChanged);
	}

	protected override void Reset()
	{
		if (!IsChanged) return;
		bool imageChanged = false;
		bool cropRectangleChanged = false;
		if (_image?.Id != _initialImageId)
		{
			foreach (var image in AvailableImages)
			{
				if (image.Id == _initialImageId)
				{
					_image = image;
					imageChanged = true;
					break;
				}
			}
		}
		if (_cropRectangle != _initialCropRectangle)
		{
			_cropRectangle = _initialCropRectangle;
			cropRectangleChanged = true;
		}
		if (imageChanged) NotifyPropertyChanged(ChangedProperty.Image);
		if (cropRectangleChanged) NotifyPropertyChanged(ChangedProperty.CropRectangle);
		OnChangeStateChange(true);
	}
}
