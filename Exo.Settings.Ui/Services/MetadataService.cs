using System.Globalization;
using System.Runtime.CompilerServices;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Settings;
using Exo.Metadata;

namespace Exo.Settings.Ui.Services;

internal sealed class MetadataService : ISettingsMetadataService, IConnectedState, IDisposable
{
	private StringMetadataResolver? _stringMetadataResolver;
	private DeviceMetadataResolver<LightingEffectMetadata>? _lightingEffectMetadataResolver;
	private DeviceMetadataResolver<LightingZoneMetadata>? _lightingZoneMetadataResolver;
	private DeviceMetadataResolver<SensorMetadata>? _sensorMetadataResolver;
	private DeviceMetadataResolver<CoolerMetadata>? _coolerMetadataResolver;
	private object _availabilitySignal;

	private readonly SettingsServiceConnectionManager _connectionManager;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	public MetadataService(SettingsServiceConnectionManager connectionManager)
	{
		_cancellationTokenSource = new CancellationTokenSource();
		_stateRegistration = connectionManager.RegisterStateAsync(this).GetAwaiter().GetResult();
		_connectionManager = connectionManager;
		_availabilitySignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
	}

	public void Dispose()
	{
		_cancellationTokenSource.Cancel();
		_stateRegistration.Dispose();
		Reset();
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

	private void Reset()
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

	private static void Dispose<T>(ref T? resolver)
		where T : class, IDisposable
		=> Interlocked.Exchange(ref resolver, null)?.Dispose();

	async Task IConnectedState.RunAsync(CancellationToken cancellationToken)
	{
		if (_cancellationTokenSource.IsCancellationRequested) return;
		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken))
		{
			await WatchChangesAsync(cancellationToken);
		}
	}

	private async Task WatchChangesAsync(CancellationToken cancellationToken)
	{
		var metadataService = await _connectionManager.GetMetadataServiceAsync(cancellationToken);
		_stringMetadataResolver = new(await metadataService.GetMainStringArchivePathAsync(cancellationToken));
		_lightingEffectMetadataResolver = new(await metadataService.GetMainLightingEffectArchivePathAsync(cancellationToken));
		_lightingZoneMetadataResolver = new();
		_sensorMetadataResolver = new();
		_coolerMetadataResolver = new();
		if (_availabilitySignal is TaskCompletionSource tcs)
		{
			tcs.TrySetResult();
			_availabilitySignal = Task.CompletedTask;
		}
		await foreach (var notification in metadataService.WatchMetadataSourceChangesAsync(cancellationToken))
		{
			MetadataResolver resolver = notification.Category switch
			{
				MetadataArchiveCategory.Strings => _stringMetadataResolver,
				MetadataArchiveCategory.LightingEffects => _lightingEffectMetadataResolver,
				MetadataArchiveCategory.LightingZones => _lightingZoneMetadataResolver,
				MetadataArchiveCategory.Sensors => _sensorMetadataResolver,
				MetadataArchiveCategory.Coolers => _coolerMetadataResolver,
				_ => throw new InvalidOperationException(),
			};

			switch (notification.NotificationKind)
			{
			case WatchNotificationKind.Enumeration:
			case WatchNotificationKind.Addition:
				resolver.AddArchive(notification.ArchivePath);
				break;
			case WatchNotificationKind.Removal:
				resolver.RemoveArchive(notification.ArchivePath);
				break;
			}
		}
	}

	void IConnectedState.Reset() => Reset();

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
