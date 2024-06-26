using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Exo.Overlay.NativeMethods;

namespace Exo.Overlay;

internal sealed class PopupMenu : NotificationControl
{
	public readonly struct MenuItemCollection : IList<MenuItem>
	{
		public struct Enumerator : IEnumerator<MenuItem>
		{
			private readonly MenuItem[] _items;
			private readonly int _itemCount;
			private int _index;

			public readonly MenuItem Current => _items[_index];
			readonly object IEnumerator.Current => Current;

			internal Enumerator(MenuItem[] items, int itemCount)
			{
				_items = items;
				_itemCount = itemCount;
				_index = -1;
			}

			public readonly void Dispose() { }

			public bool MoveNext() => ++_index < _itemCount;
			public void Reset() => _index = -1;
		}

		private readonly PopupMenu _popupMenu;

		internal MenuItemCollection(PopupMenu popupMenu)
		{
			_popupMenu = popupMenu;
		}

		public int Count
		{
			get
			{
				_popupMenu.NotificationWindow.EnforceThreadSafety();
				return _popupMenu._itemCount;
			}
		}

		public MenuItem this[int index]
		{
			get => _popupMenu._items.AsSpan(0, _popupMenu._itemCount)[index]!;
			set => _popupMenu._items[index] = value;
		}

		bool ICollection<MenuItem>.IsReadOnly => false;

		public bool Contains(MenuItem item) => IndexOf(item) >= 0;

		public int IndexOf(MenuItem item)
		{
			EnforceThreadSafety();
			return Array.IndexOf(_popupMenu._items, item, 0, _popupMenu._itemCount);
		}

		public void Add(MenuItem item)
		{
			EnforceThreadSafety();
			_popupMenu.InsertCore(_popupMenu._itemCount, item);
		}

		public void Insert(int index, MenuItem item)
		{
			EnforceThreadSafety();
			_popupMenu.InsertCore(index, item);
		}

		public void Move(int oldIndex, int newIndex)
		{
			EnforceThreadSafety();
			_popupMenu.MoveCore(oldIndex, newIndex);
		}

		public bool Remove(MenuItem item)
		{
			EnforceThreadSafety();
			int index = Array.IndexOf(_popupMenu._items, item, 0, _popupMenu._itemCount);
			if (index >= 0)
			{
				_popupMenu.RemoveCore(index);
				return true;
			}
			return false;
		}

		public void RemoveAt(int index)
		{
			EnforceThreadSafety();
			_popupMenu.RemoveCore(index);
		}

		public void Clear()
		{
			EnforceThreadSafety();
		}

		public void CopyTo(MenuItem[] array, int arrayIndex)
		{
			EnforceThreadSafety();
			_popupMenu._items.AsSpan(0, _popupMenu._itemCount).CopyTo(array.AsSpan(arrayIndex)!);
		}

		public Enumerator GetEnumerator() => new();
		IEnumerator<MenuItem> IEnumerable<MenuItem>.GetEnumerator() => GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		[Conditional("DEBUG")]
		private void EnforceThreadSafety() => _popupMenu.EnforceThreadSafety();
	}

	private readonly nint _handle;
	private MenuItem?[] _items;
	private int _itemCount;

	internal unsafe PopupMenu(NotificationWindow notificationWindow)
		: base(notificationWindow)
	{
		_handle = CreatePopupMenu();
		if (_handle == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
		var menuInfo = new MenuInfo
		{
			Size = Unsafe.SizeOf<MenuInfo>(),
			Fields = MenuInfoFields.Style,
			Style = MenuStyles.CheckOrBitmap | MenuStyles.NotifyByPosition,
		};
		if (SetMenuInfo(_handle, &menuInfo) == 0)
		{
			int hr = Marshal.GetHRForLastWin32Error();
			DestroyMenu(_handle);
			GC.SuppressFinalize(this);
			Marshal.ThrowExceptionForHR(hr);
		}
		_items = [];
	}

	protected override void DisposeCore(NotificationWindow notificationWindow)
	{
	}

	internal nint Handle => _handle;

	public MenuItemCollection MenuItems => AsMenuItemCollection(this);

	private static MenuItemCollection AsMenuItemCollection(PopupMenu menu)
		=> Unsafe.As<PopupMenu, MenuItemCollection>(ref menu);

	// Provide a more direct way of moving items avoiding some inefficiencies.
	private unsafe void MoveCore(int oldIndex, int newIndex)
	{
		if ((uint)oldIndex > (uint)_itemCount) throw new ArgumentOutOfRangeException(nameof(oldIndex));
		if ((uint)newIndex > (uint)_itemCount) throw new ArgumentOutOfRangeException(nameof(newIndex));
		if (oldIndex == newIndex) return;
		var item = _items[oldIndex]!;
		item.MoveTo(newIndex);

		// Start and end indices of the items that need to be updated. 
		int start, end;
		if (newIndex < oldIndex)
		{
			start = newIndex;
			end = oldIndex;
			Array.Copy(_items, start, _items, start + 1, end - start);
			start++;
		}
		else
		{
			start = oldIndex;
			end = newIndex;
			Array.Copy(_items, start + 1, _items, start, end - start);
			end--;
		}
		_items[newIndex] = item;
		while (true)
		{
			_items[start]!.SetIndex(start);
			if (start == end) break;
			start++;
		}
	}

	private unsafe void InsertCore(int index, MenuItem item)
	{
		if ((uint)index > (uint)_itemCount) throw new ArgumentOutOfRangeException(nameof(index));
		item.AttachTo(this, index);
		if (_items.Length == _itemCount)
		{
			var newItems = new MenuItem?[_itemCount == 0 ? 10 : 2 * _itemCount];
			_items.AsSpan(0, index).CopyTo(newItems);
			_items.AsSpan(index, _itemCount - index).CopyTo(newItems.AsSpan(index + 1));
			_items = newItems;
		}
		else
		{
			Array.Copy(_items, index, _items, index + 1, _itemCount - index);
		}
		_items[index] = item;
		_itemCount++;
		for (int i = index + 1; i < _itemCount; i++)
		{
			_items[i]!.IncrementIndex();
		}
	}

	private void RemoveCore(int index)
	{
		var item = _items[index]!;
		item.Detach();
		Array.Copy(_items, index + 1, _items, index, --_itemCount - index);
		for (int i = index; i < _itemCount; i++)
		{
			_items[i]!.DecrementIndex();
		}
		_items[_itemCount] = null!;
	}

	[Conditional("DEBUG")]
	internal void EnforceThreadSafety() => NotificationWindow.EnforceThreadSafety();

	internal void OnClick(int itemIndex)
	{
		if (itemIndex >= _itemCount) return;
		_items[itemIndex]?.OnClick();
	}
}

internal abstract class MenuItem
{
	private PopupMenu? _menu;
	private int _index;
	public int Index => _index;
	public object? Tag { get; set; }

	public MenuItem()
	{
	}

	protected PopupMenu? Menu => _menu;

	protected virtual string? GetText() => null;

	internal virtual void AttachTo(PopupMenu menu, int index)
	{
		if (_menu is not null) throw new InvalidOperationException("MenuItem is already attached to a menu.");
		_menu = menu;
		_index = index;
		try
		{
			InsertMenuItem();
		}
		catch
		{
			_menu = null;
			_index = 0;
			throw;
		}
	}

	internal void MoveTo(int index)
	{
		if (_menu is null) throw new InvalidOperationException("MenuItem is not attached to any menu.");
		if (index == _index) return;
		if (RemoveMenu(_menu!.Handle, (uint)_index, RemoveMenuOptions.ByPosition) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
		_index = index;
		try
		{
			InsertMenuItem();
		}
		catch
		{
			_menu = null;
			_index = 0;
			throw;
		}
	}

	internal void Detach()
	{
		if (_menu is null) throw new InvalidOperationException("MenuItem is already detached from any menu.");
		if (RemoveMenu(_menu!.Handle, (uint)_index, RemoveMenuOptions.ByPosition) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
		_menu = null;
		_index = 0;
	}

	internal void IncrementIndex() => _index++;
	internal void DecrementIndex() => _index--;
	internal void SetIndex(int index) => _index = index;

	private unsafe void InsertMenuItem()
	{
		var menuItemInfo = new MenuItemInfo { Size = Unsafe.SizeOf<MenuItemInfo>() };
		InsertMenuItemCore(&menuItemInfo);
	}

	protected unsafe virtual void InsertMenuItemCore(MenuItemInfo* info)
	{
		if (NativeMethods.InsertMenuItem(_menu!.Handle, (uint)_index, 1, info) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
	}

	protected unsafe void UpdateMenuItem(MenuItemFields fields)
	{
		if (_menu is null) return;

		var menuItemInfo = new MenuItemInfo { Size = Unsafe.SizeOf<MenuItemInfo>() };
		UpdateMenuItemCore(&menuItemInfo, fields);
	}

	protected unsafe virtual void UpdateMenuItemCore(MenuItemInfo* info, MenuItemFields fields) => throw new NotImplementedException();

	protected unsafe void UpdateMenuItemCore(MenuItemInfo* info)
	{
		if (SetMenuItemInfo(_menu!.Handle, (uint)_index, 1, info) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
	}

	[Conditional("DEBUG")]
	internal void EnforceThreadSafety() => Menu?.EnforceThreadSafety();

	internal virtual void OnClick() { }
}

internal abstract class BaseTextMenuItem : MenuItem
{
	private string _text;
	private bool _isDisabled;

	public BaseTextMenuItem(string text)
	{
		ArgumentException.ThrowIfNullOrEmpty(text);
		_text = text;
	}

	public string Text
	{
		get => _text;
		set
		{
			ArgumentException.ThrowIfNullOrEmpty(value);
			EnforceThreadSafety();
			if (value != _text)
			{
				_text = value;
				UpdateMenuItem(MenuItemFields.String);
			}
		}
	}

	public bool IsEnabled
	{
		get => !_isDisabled;
		set
		{
			EnforceThreadSafety();
			if (value == _isDisabled)
			{
				_isDisabled = !value;
				UpdateMenuItem(MenuItemFields.State);
			}
		}
	}

	protected override string? GetText() => _text;

	protected override unsafe void InsertMenuItemCore(MenuItemInfo* info)
	{
		info->Fields |= MenuItemFields.State | MenuItemFields.String;
		info->State = _isDisabled ? MenuItemState.Grayed | MenuItemState.Disabled : 0;
		fixed (char* textPointer = _text)
		{
			info->TypeData = (nint)textPointer;
			info->CharacterCount = _text.Length;
			base.InsertMenuItemCore(info);
		}
	}

	protected override unsafe void UpdateMenuItemCore(MenuItemInfo* info, MenuItemFields fields)
	{
		if ((fields & MenuItemFields.State) != 0)
		{
			info->Fields |= MenuItemFields.State;
			info->State = _isDisabled ? MenuItemState.Grayed | MenuItemState.Disabled : 0;
		}

		if ((fields & MenuItemFields.String) != 0)
		{
			fixed (char* textPointer = _text)
			{
				info->Fields |= MenuItemFields.String;
				info->TypeData = (nint)textPointer;
				info->CharacterCount = _text.Length;
				UpdateMenuItemCore(info);
			}
		}
		else
		{
			UpdateMenuItemCore(info);
		}
	}
}

internal sealed class TextMenuItem : BaseTextMenuItem
{
	public event EventHandler? Click;

	public TextMenuItem(string text) : base(text)
	{
	}

	internal override void OnClick() => Click?.Invoke(this, EventArgs.Empty);
}

internal sealed class SubMenuMenuItem : BaseTextMenuItem
{
	private readonly PopupMenu _subMenu;

	public PopupMenu SubMenu => _subMenu;

	public SubMenuMenuItem(string text, PopupMenu subMenu) : base(text)
	{
		ArgumentNullException.ThrowIfNull(subMenu);
		_subMenu = subMenu;
	}

	internal override void AttachTo(PopupMenu menu, int index)
	{
		// NB: Should never happen in the current implementation, as we only always have exactly one window.
		if (_subMenu.NotificationWindow != menu?.NotificationWindow) throw new InvalidOperationException("The submenu is attached to a different window.");
		base.AttachTo(menu, index);
	}

	protected override unsafe void InsertMenuItemCore(MenuItemInfo* info)
	{
		info->Fields = MenuItemFields.SubMenu;
		info->SubMenuHandle = _subMenu.Handle;
		base.InsertMenuItemCore(info);
	}
}

internal sealed class SeparatorMenuItem : MenuItem
{
	public SeparatorMenuItem() { }

	protected override unsafe void InsertMenuItemCore(MenuItemInfo* info)
	{
		info->Fields = MenuItemFields.Type;
		info->Type = MenuItemType.Separator;
		base.InsertMenuItemCore(info);
	}
}

