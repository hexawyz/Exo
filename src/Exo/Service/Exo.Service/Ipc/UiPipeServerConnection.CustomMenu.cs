using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchCustomMenuChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _server.CustomMenuService.WatchChangesAsync(cancellationToken).ConfigureAwait(false))
			{
				var message = notification.Kind switch
				{
					WatchNotificationKind.Enumeration => ExoUiProtocolServerMessage.CustomMenuItemEnumeration,
					WatchNotificationKind.Addition => ExoUiProtocolServerMessage.CustomMenuItemAdd,
					WatchNotificationKind.Removal => ExoUiProtocolServerMessage.CustomMenuItemRemove,
					WatchNotificationKind.Update => ExoUiProtocolServerMessage.CustomMenuItemUpdate,
					_ => throw new UnreachableException()
				};
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int count = FillBuffer(buffer.Span, message, notification);
					await WriteAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
				}

				static int FillBuffer(Span<byte> buffer, ExoUiProtocolServerMessage message, MenuItemWatchNotification notification)
				{
					buffer[0] = (byte)message;
					return WriteNotificationData(buffer[1..], notification) + 1;
				}

				static int WriteNotificationData(Span<byte> buffer, MenuItemWatchNotification notification)
				{
					var writer = new BufferWriter(buffer);
					writer.Write(notification.ParentItemId);
					writer.Write(notification.Position);
					writer.Write(notification.MenuItem.ItemId);
					writer.Write((byte)notification.MenuItem.Type);
					if (notification.MenuItem.Type is MenuItemType.Default or MenuItemType.SubMenu)
					{
						writer.WriteVariableString((notification.MenuItem as TextMenuItem)?.Text ?? "");
					}
					return (int)writer.Length;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private void ProcessMenuItemInvocation(Guid commandId)
	{
	}

	private ValueTask<bool> ProcessCustomMenuUpdate(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		uint requestId = reader.ReadVariableUInt32();
		ImmutableArray<MenuItem> menuItems;
		try
		{
			menuItems = ReadMenuItems(ref reader);
		}
		catch (ArgumentException)
		{
			return WriteCustomMenuOperationStatusAndReturnAsync(requestId, CustomMenuOperationStatus.InvalidArgument, cancellationToken);
		}
		catch (MaximumDepthExceededException)
		{
			return WriteCustomMenuOperationStatusAndReturnAsync(requestId, CustomMenuOperationStatus.MaximumDepthExceeded, cancellationToken);
		}

		return ProcessCustomMenuUpdateAsync(requestId, menuItems, cancellationToken);
	}

	private async ValueTask<bool> ProcessCustomMenuUpdateAsync(uint requestId, ImmutableArray<MenuItem> menuItems, CancellationToken cancellationToken)
	{
		try
		{
			await _server.CustomMenuService.UpdateMenuAsync(menuItems, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception)
		{
			await WriteCustomMenuOperationStatusAsync(requestId, CustomMenuOperationStatus.Error, cancellationToken);
			return true;
		}
		await WriteCustomMenuOperationStatusAsync(requestId, CustomMenuOperationStatus.Success, cancellationToken);
		return true;
	}

	private async ValueTask<bool> WriteCustomMenuOperationStatusAndReturnAsync(uint requestId, CustomMenuOperationStatus status, CancellationToken cancellationToken)
	{
		await WriteCustomMenuOperationStatusAsync(requestId, status, cancellationToken).ConfigureAwait(false);
		return true;
	}

	private ValueTask WriteCustomMenuOperationStatusAsync(uint requestId, CustomMenuOperationStatus status, CancellationToken cancellationToken)
		=> WriteSimpleOperationStatusAsync(ExoUiProtocolServerMessage.CustomMenuOperationStatus, requestId, (byte)status, cancellationToken);

	private static ImmutableArray<MenuItem> ReadMenuItems(ref BufferReader reader)
		=> ReadMenuItems(ref reader, 0);

	// Having a depth limitation is necessary to avoid StackOverflowException, especially since we are processing outside data.
	// In addition to this, the limitation on packet length should ensure that we don't have many problems.
	const int MaxDepth = 20;

	private static ImmutableArray<MenuItem> ReadMenuItems(ref BufferReader reader, int depth)
	{
		uint count = reader.ReadVariableUInt32();
		if (count == 0) return [];
		var menuItems = new MenuItem[count];
		for (int i = 0; i < menuItems.Length; i++)
		{
			menuItems[i] = ReadMenuItem(ref reader, depth);
		}
		return ImmutableCollectionsMarshal.AsImmutableArray(menuItems);
	}

	private static MenuItem ReadMenuItem(ref BufferReader reader, int depth = 0)
	{
		var type = (MenuItemType)reader.ReadByte();
		switch (type)
		{
		case MenuItemType.Default:
			return new TextMenuItem(reader.ReadGuid(), reader.ReadVariableString() ?? throw new ArgumentException());
		case MenuItemType.SubMenu:
			if (depth == MaxDepth) throw new MaximumDepthExceededException();
			return new SubMenuMenuItem(reader.ReadGuid(), reader.ReadVariableString() ?? throw new ArgumentException(), ReadMenuItems(ref reader, depth + 1));
		case MenuItemType.Separator:
			return new SeparatorMenuItem(reader.ReadGuid());
		default:
			throw new ArgumentException();
		}
	}
}
