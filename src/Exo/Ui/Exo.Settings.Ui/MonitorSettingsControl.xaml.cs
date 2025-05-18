using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class MonitorSettingsControl : UserControl
{
	public MonitorDeviceFeaturesViewModel? MonitorDeviceFeatures
	{
		get => (MonitorDeviceFeaturesViewModel)GetValue(MonitorDeviceFeaturesProperty);
		set => SetValue(MonitorDeviceFeaturesProperty, value);
	}

	public static readonly DependencyProperty MonitorDeviceFeaturesProperty = DependencyProperty.Register
	(
		nameof(MonitorDeviceFeatures),
		typeof(MonitorDeviceFeaturesViewModel),
		typeof(MonitorMiscSettingsControl),
		new PropertyMetadata(null, (d, e) => ((MonitorSettingsControl)d).OnMonitorDeviceFeaturesChanged((MonitorDeviceFeaturesViewModel)e.OldValue, (MonitorDeviceFeaturesViewModel)e.NewValue))
	);

	private CancellationTokenSource? _featuresCancellationTokenSource;

	public MonitorSettingsControl()
	{
		InitializeComponent();
	}

	private async void OnMonitorDeviceFeaturesChanged(MonitorDeviceFeaturesViewModel? oldValue, MonitorDeviceFeaturesViewModel? newValue)
	{
		if (!ReferenceEquals(oldValue, newValue))
		{
			if (Interlocked.Exchange(ref _featuresCancellationTokenSource, null) is { } cts)
			{
				cts.Cancel();
				cts.Dispose();
			}

			if (newValue is not null)
			{
				_featuresCancellationTokenSource = cts = new CancellationTokenSource();
				try
				{
					await newValue.DebouncedRefreshAsync(cts.Token);
				}
				catch
				{
				}
			}
		}
	}
}
