using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Metadata;
using Exo.Service;

namespace Exo.Settings.Ui.Services;

internal sealed class MetadataService : ISettingsMetadataService, IDisposable
{
	private StringMetadataResolver? _stringMetadataResolver;
	private DeviceMetadataResolver<LightingEffectMetadata>? _lightingEffectMetadataResolver;
	private DeviceMetadataResolver<LightingZoneMetadata>? _lightingZoneMetadataResolver;
	private DeviceMetadataResolver<SensorMetadata>? _sensorMetadataResolver;
	private DeviceMetadataResolver<CoolerMetadata>? _coolerMetadataResolver;
	private readonly IEnumerable<ChannelReader<MetadataSourceChangeNotification>> _metadataSourceReaders;
	private object _availabilitySignal;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchChangesTask;

	public MetadataService(IEnumerable<ChannelReader<MetadataSourceChangeNotification>> metadataSourceReaders)
	{
		_metadataSourceReaders = metadataSourceReaders;
		_availabilitySignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_cancellationTokenSource = new CancellationTokenSource();
		_watchChangesTask = WatchChangesAsync(_cancellationTokenSource.Token);
	}

	public void Dispose()
	{
		_cancellationTokenSource.Cancel();
	}

	public Task WaitForAvailabilityAsync(CancellationToken cancellationToken)
	{
		if (_availabilitySignal is TaskCompletionSource tcs)
		{
			return tcs.Task.WaitAsync(cancellationToken);
		}
		else
		{
			return Unsafe.As<Task>(_availabilitySignal);
		}
	}

	private static void Dispose<T>(ref T? resolver)
		where T : class, IDisposable
		=> Interlocked.Exchange(ref resolver, null)?.Dispose();

	private async Task WatchChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			foreach (var reader in _metadataSourceReaders)
			{
				try
				{
					int state = 0;
					await foreach (var notification in reader.ReadAllAsync(cancellationToken))
					{
						switch (notification.NotificationKind)
						{
						case WatchNotificationKind.Enumeration:
							if (state == 0)
							{
								_stringMetadataResolver = new(notification.Sources.First(x => x.Category == MetadataArchiveCategory.Strings).ArchivePath);
								_lightingEffectMetadataResolver = new(notification.Sources.First(x => x.Category == MetadataArchiveCategory.LightingEffects).ArchivePath);
								_lightingZoneMetadataResolver = new();
								_sensorMetadataResolver = new();
								_coolerMetadataResolver = new();
								state = 1;
							}
							else if (state == 1)
							{
								AddArchives(notification.Sources);
							}
							else
							{
								throw new InvalidOperationException();
							}
							break;
						case WatchNotificationKind.Addition:
							if (state != 2) throw new InvalidOperationException();
							AddArchives(notification.Sources);
							break;
						case WatchNotificationKind.Removal:
							if (state != 2) throw new InvalidOperationException();
							RemoveArchives(notification.Sources);
							break;
						case WatchNotificationKind.Update:
							if (state != 1) throw new InvalidOperationException();
							if (_availabilitySignal is TaskCompletionSource tcs)
							{
								tcs.TrySetResult();
								_availabilitySignal = Task.CompletedTask;
							}
							break;
						}
					}
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					if (_availabilitySignal is TaskCompletionSource tcs)
					{
						tcs.TrySetException(ex);
					}
					throw;
				}
				finally
				{
					if (_availabilitySignal is TaskCompletionSource tcs)
					{
						tcs.TrySetCanceled();
					}
					_availabilitySignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
					Dispose(ref _stringMetadataResolver);
					Dispose(ref _lightingEffectMetadataResolver);
					Dispose(ref _lightingZoneMetadataResolver);
					Dispose(ref _sensorMetadataResolver);
					Dispose(ref _coolerMetadataResolver);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private void AddArchives(ImmutableArray<MetadataSourceInformation> sources)
	{
		if (sources.IsDefault) return;
		foreach (var source in sources)
		{
			GetResolver(source.Category).AddArchive(source.ArchivePath);
		}
	}

	private void RemoveArchives(ImmutableArray<MetadataSourceInformation> sources)
	{
		if (sources.IsDefault) return;
		foreach (var source in sources)
		{
			GetResolver(source.Category).RemoveArchive(source.ArchivePath);
		}
	}

	private MetadataResolver GetResolver(MetadataArchiveCategory category)
		=> category switch
		{
			MetadataArchiveCategory.Strings => _stringMetadataResolver!,
			MetadataArchiveCategory.LightingEffects => _lightingEffectMetadataResolver!,
			MetadataArchiveCategory.LightingZones => _lightingZoneMetadataResolver!,
			MetadataArchiveCategory.Sensors => _sensorMetadataResolver!,
			MetadataArchiveCategory.Coolers => _coolerMetadataResolver!,
			_ => throw new InvalidOperationException(),
		};

	public string? GetString(CultureInfo? culture, Guid stringId)
		=> _stringMetadataResolver?.GetStringAsync(culture, stringId);

	private static bool TryGetMetadata<T>(DeviceMetadataResolver<T>? resolver, string driverKey, string compatibleId, Guid lightingZoneId, out T value)
		where T : struct, IExoMetadata
	{
		if (resolver is not null) return resolver.TryGetData(driverKey, compatibleId, lightingZoneId, out value);

		value = default;
		return false;

	}
	public bool TryGetLightingEffectMetadata(string driverKey, string compatibleId, Guid lightingEffectId, out LightingEffectMetadata value)
		=> TryGetMetadata(_lightingEffectMetadataResolver, driverKey, compatibleId, lightingEffectId, out value);

	public bool TryGetLightingZoneMetadata(string driverKey, string compatibleId, Guid lightingZoneId, out LightingZoneMetadata value)
		=> TryGetMetadata(_lightingZoneMetadataResolver, driverKey, compatibleId, lightingZoneId, out value);

	public bool TryGetSensorMetadata(string driverKey, string compatibleId, Guid sensorId, out SensorMetadata value)
		=> TryGetMetadata(_sensorMetadataResolver, driverKey, compatibleId, sensorId, out value);

	public bool TryGetCoolerMetadata(string driverKey, string compatibleId, Guid coolerId, out CoolerMetadata value)
		=> TryGetMetadata(_coolerMetadataResolver, driverKey, compatibleId, coolerId, out value);
}
