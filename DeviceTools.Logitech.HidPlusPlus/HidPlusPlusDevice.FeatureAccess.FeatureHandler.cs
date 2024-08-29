namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		// NB: This would probably be exposed somehow so that notifications can be registered externally, but the API needs to be reworked.
		// Notably, there is the problem of matching the notification handler with the device and getting the proper feature index.
		// Internally, this is handled by providing the information in the constructor, but externally, that would be a weird thing to do.
		private abstract class FeatureHandler : IAsyncDisposable
		{
			protected FeatureAccess Device { get; }
			protected byte FeatureIndex { get; }
			public abstract HidPlusPlusFeature Feature { get; }

			protected FeatureHandler(FeatureAccess device, byte featureIndex)
			{
				Device = device;
				FeatureIndex = featureIndex;
			}

			public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

			internal void HandleNotificationInternal(byte eventId, ReadOnlySpan<byte> response)
			{
				try
				{
					HandleNotification(eventId, response);
				}
				catch (Exception ex)
				{
					Device.Logger.FeatureAccessFeatureHandlerException(Feature, eventId, ex);
				}
			}

			public virtual Task InitializeAsync(int retryCount, CancellationToken cancellationToken) => Task.CompletedTask;

			public virtual void Reset() { }

			protected virtual void HandleNotification(byte eventId, ReadOnlySpan<byte> response) { }
		}
	}
}
