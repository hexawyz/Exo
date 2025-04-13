using System.Diagnostics;
using Exo.Settings.Ui.Ipc;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var info in _deviceRegistry.WatchAllAsync(cancellationToken).ConfigureAwait(false))
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int length = WriteUpdate(buffer.Span, info);
					await WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}

		static int WriteUpdate(Span<byte> buffer, in DeviceWatchNotification notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write
			(
				(byte)
				(
					notification.Kind switch
					{
						WatchNotificationKind.Enumeration => ExoUiProtocolServerMessage.DeviceEnumeration,
						WatchNotificationKind.Addition => ExoUiProtocolServerMessage.DeviceAdd,
						WatchNotificationKind.Removal => ExoUiProtocolServerMessage.DeviceRemove,
						WatchNotificationKind.Update => ExoUiProtocolServerMessage.DeviceUpdate,
						_ => throw new UnreachableException(),
					}
				)
			);
			WriteInformation(ref writer, notification.DeviceInformation);
			return (int)writer.Length;
		}

		static void WriteInformation(ref BufferWriter writer, DeviceStateInformation information)
		{
			writer.Write(information.Id);
			writer.WriteVariableString(information.FriendlyName);
			writer.WriteVariableString(information.UserFriendlyName);
			writer.Write((byte)information.Category);
			writer.WriteVariable((uint)information.FeatureIds.Count);
			foreach (var featureId in information.FeatureIds)
			{
				writer.Write(featureId);
			}
			writer.WriteVariable((uint)information.DeviceIds.Length);
			foreach (var deviceId in information.DeviceIds)
			{
				writer.Write((byte)deviceId.Source);
				writer.Write((byte)deviceId.VendorIdSource);
				writer.Write(deviceId.VendorId);
				writer.Write(deviceId.ProductId);
				writer.Write(deviceId.Version);
			}
			byte flags = 0;
			if (information.IsAvailable) flags |= 1;
			if (information.MainDeviceIdIndex is not null) flags |= 2;
			writer.Write(flags);
			if (information.MainDeviceIdIndex is not null) writer.WriteVariable((uint)information.MainDeviceIdIndex.GetValueOrDefault());
			writer.WriteVariableString(information.SerialNumber);
		}
	}
}
