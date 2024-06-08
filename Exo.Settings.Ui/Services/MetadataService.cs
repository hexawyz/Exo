using System.Globalization;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Settings;
using Exo.Metadata;
using IMetadataService = Exo.Metadata.IMetadataService;

namespace Exo.Settings.Ui.Services;

internal sealed class MetadataService : IMetadataService, IConnectedState, IDisposable
{
	private StringMetadataResolver? _stringMetadataResolver;
	private DeviceMetadataResolver<LightingEffectMetadata>? _lightingEffectMetadataResolver;
	private DeviceMetadataResolver<LightingZoneMetadata>? _lightingZoneMetadataResolver;
	private DeviceMetadataResolver<SensorMetadata>? _sensorMetadataResolver;
	private DeviceMetadataResolver<CoolerMetadata>? _coolerMetadataResolver;

	private readonly SettingsServiceConnectionManager _connectionManager;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	public MetadataService(SettingsServiceConnectionManager connectionManager)
	{
		_cancellationTokenSource = new CancellationTokenSource();
		_stateRegistration = connectionManager.RegisterStateAsync(this).GetAwaiter().GetResult();
		_connectionManager = connectionManager;
	}

	public void Dispose()
	{
		_cancellationTokenSource.Cancel();
		_stateRegistration.Dispose();
		Reset();
	}

	private void Reset()
	{
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
		_stringMetadataResolver = new(await metadataService.GetMainStringsArchivePathAsync(cancellationToken));
		_lightingEffectMetadataResolver = new();
		_lightingZoneMetadataResolver = new();
		_sensorMetadataResolver = new();
		_coolerMetadataResolver = new();
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

	public string? GetStringAsync(CultureInfo? culture, Guid stringId)
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

	public bool TryGetSensorMetadataAsync(string driverKey, string compatibleId, Guid sensorId, out SensorMetadata value)
		=> TryGetMetadata(_sensorMetadataResolver, driverKey, compatibleId, sensorId, out value);

	public bool TryGetCoolerMetadataAsync(string driverKey, string compatibleId, Guid coolerId, out CoolerMetadata value)
		=> TryGetMetadata(_coolerMetadataResolver, driverKey, compatibleId, coolerId, out value);
}
