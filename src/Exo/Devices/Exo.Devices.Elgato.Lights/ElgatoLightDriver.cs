using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Exo.Discovery;
using Exo.Features;

namespace Exo.Devices.Elgato.Lights;

public class ElgatoLightDriver : Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceSerialNumberFeature
{
	private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

	[DiscoverySubsystem<DnsSdDiscoverySubsystem>]
	[DnsSdServiceType("_elg._tcp")]
	public static async ValueTask<DriverCreationResult<DnsSdInstanceId>> CreateAsync
	(
		DnsSdDeviceLifetime deviceLifetime,
		ImmutableArray<DnsSdInstanceId> keys,
		string fullName,
		string instanceName,
		string hostName,
		ushort portNumber,
		ImmutableArray<string> textAttributes,
		CancellationToken cancellationToken
	)
	{
		// TODO: We should reuse the same message handler everywhere.
		var httpClient = new HttpClient() { BaseAddress = new Uri($"http://{hostName}:{portNumber}/") };

		ElgatoAccessoryInfo accessoryInfo;
		try
		{
			try
			{
				accessoryInfo = await httpClient.GetFromJsonAsync<ElgatoAccessoryInfo>("/elgato/accessory-info", JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException ex) when (ex.InnerException is SocketException sex && sex.SocketErrorCode == SocketError.TimedOut)
			{
				// Repackage a timeout into a device offline exception to notify the caller.
				throw new DeviceOfflineException(instanceName);
			}
		}
		catch
		{
			httpClient.Dispose();
			throw;
		}

		return new(keys, new ElgatoLightDriver(deviceLifetime, httpClient, instanceName, new DeviceConfigurationKey("elg", fullName, fullName, accessoryInfo.SerialNumber)));
	}

	private readonly DnsSdDeviceLifetime _lifetime;
	private readonly HttpClient _httpClient;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;

	private ElgatoLightDriver
	(
		DnsSdDeviceLifetime lifetime,
		HttpClient httpClient,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_lifetime = lifetime;
		_httpClient = httpClient;
		_genericFeatures = configurationKey.UniqueId is not null ?
			FeatureSet.Create<IGenericDeviceFeature, ElgatoLightDriver, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Empty<IGenericDeviceFeature>();
		lifetime.DeviceUpdated += OnDeviceUpdated;
	}

	public override ValueTask DisposeAsync()
	{
		_httpClient.Dispose();
		return ValueTask.CompletedTask;
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Light;

	public IDeviceFeatureSet<IGenericDeviceFeature> Features => _genericFeatures;

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	private void OnDeviceUpdated(object? sender, EventArgs e)
	{
	}
}

internal readonly struct ElgatoAccessoryInfo
{
	public required string ProductName { get; init; }
	public int HardwareBoardType { get; init; }
	public float HardwareRevision { get; init; }
	public required string MacAddress { get; init; }
	public int FirmwareBuildNumber { get; init; }
	public required string FirmwareVersion { get; init; }
	public required string SerialNumber { get; init; }
	public required string DisplayName { get; init; }
	public required ImmutableArray<string> Features { get; init; }
}

internal readonly struct ElgatoWifiInfo
{
	public required string Ssid { get; init; }
	public int FrequencyMHz { get; init; }
	public int Rssi { get; init; }
}
