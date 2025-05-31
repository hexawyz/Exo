using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Exo.Settings.Ui.Extensions;

internal static class FrameworkElementExtensions
{
	public static TElement? FindChild<TElement>(this FrameworkElement element, string name)
		where TElement : FrameworkElement
	{
		int childCount = VisualTreeHelper.GetChildrenCount(element);
		return childCount > 0 ? FindChild<TElement>(element, childCount, name) : null;
	}

	public static TElement? RecursiveFindChild<TElement>(this FrameworkElement element, string name)
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
			if (child.Name == name && child.TryAs<TElement>(out var typedChild)) return typedChild;
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
}
