using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Exo.Settings.Ui.ViewModels;
using CommunityToolkit.WinUI.UI.Controls;
using CommunityToolkit.WinUI.UI;

namespace Exo.Settings.Ui;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class LightingPage : Page
{
	public LightingPage()
	{
		InitializeComponent();
	}

	private async void OnApplyButtonClick(object sender, RoutedEventArgs e) => await ((LightingDeviceViewModel)((FrameworkElement)sender).DataContext).ApplyChangesAsync(default);
}
