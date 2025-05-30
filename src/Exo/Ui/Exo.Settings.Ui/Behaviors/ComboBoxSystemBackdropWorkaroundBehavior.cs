// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// https://github.com/DGP-Studio/Snap.Hutao/blob/4c040c24f60498c7ad540de20cf2bf5c938238b3/src/Snap.Hutao/Snap.Hutao/UI/Xaml/Behavior/ComboBoxSystemBackdropWorkaroundBehavior.cs

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using CommunityToolkit.WinUI.Behaviors;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Content;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using WinRT;

namespace Exo.Settings.Ui.Behaviors;

[SuppressMessage("", "CA1001")]
internal sealed class ComboBoxSystemBackdropWorkaroundBehavior : BehaviorBase<ComboBox>
{
	private Popup? _popup;
	private ContentExternalBackdropLink? _backdropLink;
	private DesktopAcrylicController? _desktopAcrylicController;
	private SystemBackdropConfiguration? _systemBackdropConfiguration;
	private bool _connected;

	public static bool TryAs<TInterface>(IWinRTObject? value, [NotNullWhen(true)] out TInterface? result)
		where TInterface : class
	{
		if (value is null) goto NotSuccessful;

		if (value is TInterface i)
		{
			result = i;
			return true;
		}

		nint abi = 0;
		try
		{
			int qir = value.NativeObject.TryAs(GuidGenerator.GetGUID(typeof(TInterface)), out abi);
			if (qir >= 0)
			{
				result = MarshalInspectable<TInterface>.FromAbi(abi);
				return true;
			}
			else if ((uint)qir == 0x80004002U)
			{
				goto NotSuccessful;
			}
			else
			{
				Marshal.ThrowExceptionForHR(qir);
			}
		}
		finally
		{
			if (abi != 0)
			{
				MarshalInspectable<object>.DisposeAbi(abi);
			}
		}

	NotSuccessful:;
		result = null;
		return false;
	}

	private static TElement? FindChild<TElement>(FrameworkElement element, string name)
		where TElement : FrameworkElement
	{
		int childCount = VisualTreeHelper.GetChildrenCount(element);
		return childCount > 0 ? FindChild<TElement>(element, childCount, name) : null;
	}

	private static TElement? RecursiveFindChild<TElement>(FrameworkElement element, string name)
		where TElement : FrameworkElement
	{
		int childCount = VisualTreeHelper.GetChildrenCount(element);
		return childCount > 0 ? RecursiveFindChild<TElement>(element, childCount, name) : null;
	}

	private static TElement? FindChild<TElement>(FrameworkElement element, int childCount, string name)
		where TElement : FrameworkElement
	{
		for (int i = 0; i < childCount; i++)
		{
			if (VisualTreeHelper.GetChild(element, i) is not FrameworkElement child) continue;
			if (child.Name == name && TryAs<TElement>(child, out var typedChild)) return typedChild;
		}
		return null;
	}

	private static TElement? RecursiveFindChild<TElement>(FrameworkElement element, int childCount, string name)
		where TElement : FrameworkElement
	{
		// Step 1: Search children at top level
		if (FindChild<TElement>(element, childCount, name) is { } foundChild) return foundChild;
		// Step 2: Search children of children.
		for (int i = 0; i < childCount; i++)
		{
			if (VisualTreeHelper.GetChild(element, i) is not FrameworkElement child) continue;
			int count = VisualTreeHelper.GetChildrenCount(child);
			if (count > 0 && RecursiveFindChild<TElement>(child, count, name) is { } childChild) return childChild;
		}
		return null;
	}

	protected override bool Initialize()
	{
		ComboBox comboBox = AssociatedObject;
		if (RecursiveFindChild<Popup>(comboBox, "Popup") is not { } popup)
		{
			return false;
		}

		_popup = popup;

		popup.Opened += OnPopupOpened;
		popup.ActualThemeChanged += OnPopupActualThemeChanged;

		if (!comboBox.IsEditable)
		{
			comboBox.IsDropDownOpen = true;
		}

		return true;
	}

	protected override bool Uninitialize()
	{
		if (_popup is not null)
		{
			_popup.Opened -= OnPopupOpened;
			_popup.ActualThemeChanged -= OnPopupActualThemeChanged;
			_popup = null;
		}

		Interlocked.Exchange(ref _backdropLink, null)?.Dispose();
		Interlocked.Exchange(ref _desktopAcrylicController, null)?.Dispose();

		return base.Uninitialize();
	}

	private void OnPopupOpened(object? sender, object e)
	{
		if (sender is not Popup popup)
		{
			return;
		}

		if (popup.FindName("PopupBorder") is not Border border)
		{
			return;
		}

		Vector2 size = border.ActualSize;
		Compositor compositor = ElementCompositionPreview.GetElementVisual(border).Compositor;
		Vector2 cornerRadius = new(8, 8);

		if (!_connected)
		{
			_connected = true;

			UIElement child = border.Child;
			Grid rootGrid = new();
			border.Child = rootGrid;
			Grid visualGrid = new();
			rootGrid.Children.Add(visualGrid);
			rootGrid.Children.Add(child);

			_backdropLink = ContentExternalBackdropLink.Create(compositor);
			_backdropLink.ExternalBackdropBorderMode = CompositionBorderMode.Soft;

			// Modify PlacementVisual
			Visual placementVisual = _backdropLink.PlacementVisual;
			placementVisual.Size = size;
			placementVisual.Clip = compositor.CreateRectangleClip(0, 0, size.X, size.Y, cornerRadius, cornerRadius, cornerRadius, cornerRadius);
			placementVisual.BorderMode = CompositionBorderMode.Soft;

			ElementCompositionPreview.SetElementChildVisual(visualGrid, placementVisual);

			_systemBackdropConfiguration = new()
			{
				IsInputActive = true,
				Theme = ThemeHelper.ElementToSystemBackdrop(popup.ActualTheme),
			};

			_desktopAcrylicController = new();
			_desktopAcrylicController.SetSystemBackdropConfiguration(_systemBackdropConfiguration);
			_desktopAcrylicController.AddSystemBackdropTarget(_backdropLink.As<ICompositionSupportsSystemBackdrop>());

			popup.IsOpen = false;
		}
		else if (_backdropLink is not null && _systemBackdropConfiguration is not null)
		{
			// Update PlacementVisual
			Visual placementVisual = _backdropLink.PlacementVisual;
			placementVisual.Size = size;
			placementVisual.Clip = compositor.CreateRectangleClip(0, 0, size.X, size.Y, cornerRadius, cornerRadius, cornerRadius, cornerRadius);
		}
	}

	private void OnPopupActualThemeChanged(FrameworkElement sender, object args)
	{
		if (_systemBackdropConfiguration is not null)
		{
			_systemBackdropConfiguration.Theme = ThemeHelper.ElementToSystemBackdrop(sender.ActualTheme);
		}
	}
}
