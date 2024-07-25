using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class MonitorSettingsControl : UserControl
{
	private CancellationTokenSource? _dataContextCancellationTokenSource;
	private MonitorDeviceFeaturesViewModel? _viewModel;

	public MonitorSettingsControl()
	{
		InitializeComponent();
		DataContextChanged += OnDataContextChanged;
	}

	private async void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
	{
		var oldValue = _viewModel;
		_viewModel = args.NewValue as MonitorDeviceFeaturesViewModel;

		if (!ReferenceEquals(oldValue, _viewModel))
		{
			if (Interlocked.Exchange(ref _dataContextCancellationTokenSource, null) is { } cts)
			{
				cts.Cancel();
				cts.Dispose();
			}

			if (_viewModel is not null)
			{
				_dataContextCancellationTokenSource = cts = new CancellationTokenSource();
				try
				{
					await _viewModel.DebouncedRefreshAsync(cts.Token);
				}
				catch
				{
				}
			}
		}
	}
}
