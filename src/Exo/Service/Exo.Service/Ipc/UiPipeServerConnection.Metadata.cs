using System.Diagnostics;
using System.Numerics;
using Exo.Primitives;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchMetadataChangesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<AssemblyChangeNotification>.CreateAsync(_server.AssemblyLoader, cancellationToken).ConfigureAwait(false))
		{
			try
			{
				await WriteInitialDataAsync(watcher, cancellationToken).ConfigureAwait(false);
				await WriteConsumedDataAsync(watcher, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
		}

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<AssemblyChangeNotification> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int count;
					foreach (var notification in initialData)
					{
						count = WriteNotification(buffer.Span, notification);
						await WriteAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
					}
					// Signal the end of initial enumeration.
					count = WriteNotification(buffer.Span, new(WatchNotificationKind.Update, "", MetadataArchiveCategories.None));
					await WriteAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<AssemblyChangeNotification> watcher, CancellationToken cancellationToken)
		{
			while (await watcher.Reader.WaitToReadAsync().ConfigureAwait(false) && !cancellationToken.IsCancellationRequested)
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var notification))
					{
						int count = WriteNotification(buffer.Span, notification);
						if (cancellationToken.IsCancellationRequested) return;
						await WriteAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int WriteNotification(Span<byte> buffer, AssemblyChangeNotification notification)
		{
			var writer = new BufferWriter(buffer);
			switch (notification.NotificationKind)
			{
			case WatchNotificationKind.Enumeration:
				writer.Write((byte)ExoUiProtocolServerMessage.MetadataSourcesEnumeration);
				break;
			case WatchNotificationKind.Addition:
				writer.Write((byte)ExoUiProtocolServerMessage.MetadataSourcesAdd);
				break;
			case WatchNotificationKind.Removal:
				writer.Write((byte)ExoUiProtocolServerMessage.MetadataSourcesRemove);
				break;
			case WatchNotificationKind.Update:
				writer.Write((byte)ExoUiProtocolServerMessage.MetadataSourcesUpdate);
				goto Completed;
			default: throw new UnreachableException();
			}
			const MetadataArchiveCategories AllCategories =
				MetadataArchiveCategories.Strings |
				MetadataArchiveCategories.LightingEffects |
				MetadataArchiveCategories.LightingZones |
				MetadataArchiveCategories.Sensors |
				MetadataArchiveCategories.Coolers;
			writer.WriteVariable((uint)BitOperations.PopCount((nuint)notification.AvailableMetadataArchives & (nuint)AllCategories));
			WriteSourceIfPresent(ref writer, notification, MetadataArchiveCategory.Strings);
			WriteSourceIfPresent(ref writer, notification, MetadataArchiveCategory.LightingEffects);
			WriteSourceIfPresent(ref writer, notification, MetadataArchiveCategory.LightingZones);
			WriteSourceIfPresent(ref writer, notification, MetadataArchiveCategory.Sensors);
			WriteSourceIfPresent(ref writer, notification, MetadataArchiveCategory.Coolers);
		Completed:;
			return (int)writer.Length;
		}

		static void WriteSourceIfPresent(ref BufferWriter writer, AssemblyChangeNotification notification, MetadataArchiveCategory category)
		{
			if ((notification.AvailableMetadataArchives & (MetadataArchiveCategories)(1 << (int)category)) != 0)
			{
				WriteSource(ref writer, category, notification.GetArchivePath(category));
			}
		}

		static void WriteSource(ref BufferWriter writer, MetadataArchiveCategory category, string archivePath)
		{
			writer.Write((byte)category);
			writer.WriteVariableString(archivePath);
		}
	}
}
