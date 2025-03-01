using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using Exo.Discovery;
using Exo.Features;

namespace Exo.Devices.Elgato.Lights;

public class ElgatoLightDriver : Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceSerialNumberFeature
{
	private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
	{
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
	};

	private const string AccessoryInfoPath = "/elgato/accessory-info";
	private const string LightsPath = "/elgato/lights";

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
		ElgatoLights lights;
		try
		{
			try
			{
				accessoryInfo = await httpClient.GetFromJsonAsync<ElgatoAccessoryInfo>(AccessoryInfoPath, JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException ex) when (IsTimeout(ex))
			{
				// Repackage a timeout into a device offline exception to notify the caller.
				throw new DeviceOfflineException(instanceName);
			}

			if (accessoryInfo.Features.IsDefaultOrEmpty || !accessoryInfo.Features.Contains("lights"))
			{
				throw new InvalidOperationException();
			}

			try
			{
				lights = await httpClient.GetFromJsonAsync<ElgatoLights>(LightsPath, JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException ex) when (IsTimeout(ex))
			{
				// Repackage a timeout into a device offline exception to notify the caller.
				throw new DeviceOfflineException(instanceName);
			}

			// The below is code to set a light on.
			// It can perhaps be simplified a bit, but don't be fooled.
			// The device is very touchy on the kind of requests it accepts and will reject chunked encoding.
			// However, it does seemingly not require any particular header to be specified.

			if (lights.NumberOfLights != 1)
			{
				throw new InvalidOperationException("Support for devices with more than one light is not yet implemented.");
			}
		}
		catch
		{
			httpClient.Dispose();
			throw;
		}

		string? model = null;
		foreach (string s in textAttributes)
		{
			if (s.StartsWith("md="))
			{
				model = s[3..];
			}
		}

		return new(keys, new ElgatoLightDriver(deviceLifetime, httpClient, lights.Lights, instanceName, new DeviceConfigurationKey("elg", fullName, model ?? fullName, accessoryInfo.SerialNumber)));
	}

	private static bool IsTimeout(HttpRequestException ex) => ex.InnerException is SocketException sex && IsTimeout(sex);
	private static bool IsTimeout(SocketException ex) => ex.SocketErrorCode == SocketError.TimedOut;

	private static bool IsTimeoutOrConnectionReset(HttpRequestException ex) => ex.InnerException switch
	{
		IOException ioex => IsTimeoutOrConnectionReset(ioex),
		SocketException sex => IsTimeoutOrConnectionReset(sex),
		_ => false
	};

	private static bool IsTimeoutOrConnectionReset(IOException ex) => ex.InnerException is SocketException sex && IsTimeoutOrConnectionReset(sex);
	private static bool IsTimeoutOrConnectionReset(SocketException ex) => ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.TimedOut;

	private readonly LightState[] _lights;
	private readonly HttpClient _httpClient;
	private readonly AsyncLock _lock;
	private readonly MemoryStream _updateStream;
	private readonly DnsSdDeviceLifetime _lifetime;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;

	private ElgatoLightDriver
	(
		DnsSdDeviceLifetime lifetime,
		HttpClient httpClient,
		ImmutableArray<ElgatoLight> lights,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_lights = Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(lights)!, l => new LightState(l));
		_httpClient = httpClient;
		_lifetime = lifetime;
		_genericFeatures = configurationKey.UniqueId is not null ?
			FeatureSet.Create<IGenericDeviceFeature, ElgatoLightDriver, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Empty<IGenericDeviceFeature>();
		_lock = new();
		_updateStream = new(GC.AllocateUninitializedArray<byte>(128, false), 0, 128, true, true);
		_cancellationTokenSource = new();
		lifetime.DeviceUpdated += OnDeviceUpdated;
	}

	public override ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			_lifetime.Dispose();
			_httpClient.Dispose();
		}
		return ValueTask.CompletedTask;
	}

	private async void OnDeviceUpdated(object? sender, EventArgs e)
	{
		await RefreshLightsAsync(_cancellationTokenSource!.Token).ConfigureAwait(false);
	}

	private async Task RefreshLightsAsync(CancellationToken cancellationToken)
	{
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			ElgatoLights lights;
			try
			{
				lights = await _httpClient.GetFromJsonAsync<ElgatoLights>(LightsPath, JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException ex) when (IsTimeoutOrConnectionReset(ex))
			{
				_lifetime.NotifyDeviceOffline();
				return;
			}
			Update(lights);
		}
	}

	private async Task SendUpdateAsync(ElgatoLightsUpdate update, CancellationToken cancellationToken)
	{
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var stream = _updateStream;
			stream.SetLength(0);
			JsonSerializer.Serialize(stream, update, JsonSerializerOptions);
			using var json = new ByteArrayContent(stream.GetBuffer(), 0, (int)stream.Position);
			using var request = new HttpRequestMessage(HttpMethod.Put, LightsPath) { Content = json };
			HttpResponseMessage response;
			try
			{
				response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException ex) when (IsTimeoutOrConnectionReset(ex))
			{
				_lifetime.NotifyDeviceOffline();
				return;
			}
			using (response)
			{
				Update(JsonSerializer.Deserialize<ElgatoLights>(await response.Content.ReadAsStreamAsync(), JsonSerializerOptions));
			}
		}
	}

	private Task SwitchLightAsync(bool isOn, CancellationToken cancellationToken)
		=> SendUpdateAsync(new() { Lights = [new() { On = isOn ? (byte)1 : (byte)0 }] }, cancellationToken);

	private async Task SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(brightness, 100);
		await SendUpdateAsync(new() { Lights = [new() { Brightness = brightness }] }, cancellationToken);
	}

	private async Task SetTemperatureAsync(ushort temperature, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(temperature, 143);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(temperature, 344);
		await SendUpdateAsync(new() { Lights = [new() { Temperature = temperature }] }, cancellationToken);
	}

	private void Update(ElgatoLights lights)
	{
		var src = lights.Lights;
		var dst = _lights;

		if (src.Length != dst.Length) throw new InvalidOperationException();

		for (int i = 0; i < src.Length; i++)
		{
			dst[i].Update(src[i]);
		}
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Light;

	public IDeviceFeatureSet<IGenericDeviceFeature> Features => _genericFeatures;

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	private sealed class LightState
	{
		private bool _isOn;
		private byte _brightness;
		private ushort _temperature;

		public LightState(ElgatoLight light) => Update(light);

		public void Update(ElgatoLight light)
		{
			_isOn = light.On != 0;
			_brightness = light.Brightness;
			_temperature = light.Temperature;
		}
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

internal readonly struct ElgatoLights
{
	public required int NumberOfLights { get; init; }
	public required ImmutableArray<ElgatoLight> Lights { get; init; }
}

internal readonly struct ElgatoLight
{
	// 0 - 1
	public required byte On { get; init; }
	// 3 - 100
	public required byte Brightness { get; init; }
	// 143 (7000K) - 344 (2900K)
	// This is likely non linear. UI only shows temperatures by intervals of 50K but will send any possible value in-between. Points are more dense on the blue side than on the red side.
	// 4950 maps to about 202 (Middle would be 243,5 if linear)
	// Formula given here seems to be correct: https://github.com/aaroncampbell/elgato-key-light-control
	// Which is that T° = 1000000 / Value
	public required ushort Temperature { get; init; }
}

internal readonly struct ElgatoLightsUpdate
{
	public int? NumberOfLights { get; init; }
	public ImmutableArray<ElgatoLightUpdate> Lights { get; init; }
}

internal readonly struct ElgatoLightUpdate
{
	public byte? On { get; init; }
	public byte? Brightness { get; init; }
	public ushort? Temperature { get; init; }
}
