using System.Collections.ObjectModel;
using Exo.Contracts.Ui.Settings;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class EmbeddedMonitorFeaturesViewModel : BindableObject
{
	private readonly DeviceViewModel _device;
	private readonly Dictionary<Guid, EmbeddedMonitorViewModel> _embeddedMonitorById;
	private readonly ObservableCollection<EmbeddedMonitorViewModel> _embeddedMonitors;
	private readonly ReadOnlyObservableCollection<EmbeddedMonitorViewModel> _readOnlyEmbeddedMonitors;
	private bool _isExpanded;

	public EmbeddedMonitorFeaturesViewModel(DeviceViewModel device)
	{
		_device = device;
		_embeddedMonitors = new();
		_embeddedMonitorById = new();
		_readOnlyEmbeddedMonitors = new(_embeddedMonitors);
	}

	public ReadOnlyObservableCollection<EmbeddedMonitorViewModel> EmbeddedMonitors => _readOnlyEmbeddedMonitors;

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
				_embeddedMonitorById.Add(monitorInformation.MonitorId, new(monitorInformation));
			}
		}
	}
}

internal sealed class EmbeddedMonitorViewModel : BindableObject
{
	private readonly Guid _monitorId;
	private MonitorShape _shape;
	private Size _imageSize;

	public EmbeddedMonitorViewModel(EmbeddedMonitorInformation information)
	{
		_monitorId = information.MonitorId;
		_shape = information.Shape;
		_imageSize = information.ImageSize;
	}

	public Guid MonitorId => _monitorId;

	public MonitorShape Shape
	{
		get => _shape;
		set => SetValue(ref _shape, value, ChangedProperty.Shape);
	}

	public Size ImageSize
	{
		get => _imageSize;
		set => SetValue(ref _imageSize, value, ChangedProperty.ImageSize);
	}

	internal void UpdateInformation(EmbeddedMonitorInformation information)
	{
		Shape = information.Shape;
		ImageSize = information.ImageSize;
	}
}
