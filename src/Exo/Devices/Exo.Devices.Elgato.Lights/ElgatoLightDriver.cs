using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Lights;

namespace Exo.Devices.Elgato.Lights;

public sealed partial class ElgatoLightDriver : Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<ILightDeviceFeature>,
	IDeviceSerialNumberFeature,
	ILightControllerFeature,
	IPolledLightControllerFeature
{
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
		ImmutableArray<string> ipAddresses,
		CancellationToken cancellationToken
	)
	{
		// Avoid relying on the domain name, as it seems that this can sometimes fail.
		var ipAddress = IPAddress.Parse(ipAddresses[0]);
		// TODO: We should reuse the same message handler everywhere.
		var httpClient = new HttpClient() { BaseAddress = new Uri(ipAddress.AddressFamily == AddressFamily.InterNetworkV6 ? $"http://[{ipAddress}]:{portNumber}/" : $"http://{ipAddress}:{portNumber}/") };
		httpClient.DefaultRequestHeaders.Host = hostName;

		ElgatoAccessoryInfo accessoryInfo;
		ElgatoLights lights;
		try
		{
			try
			{
				using var stream = await httpClient.GetStreamAsync(AccessoryInfoPath, cancellationToken).ConfigureAwait(false);
				accessoryInfo = JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.ElgatoAccessoryInfo);
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
				using var stream = await httpClient.GetStreamAsync(LightsPath, cancellationToken).ConfigureAwait(false);
				lights = JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.ElgatoLights);
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

			if ((uint)lights.NumberOfLights > (uint)LightIds.Count)
			{
				throw new InvalidOperationException($"Devices with {lights.NumberOfLights} lights are not yet supported. Please submit a support request to increase the limit.");
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

	// Define a reasonable status update interval to avoid too frequent HTTP requests.
	// For now, le'ts assume that refreshing more than once every 10s is unreasonnable.
	private static readonly ulong UpdateInterval = (ulong)(10 * Stopwatch.Frequency);

	private readonly LightState[] _lights;
	private readonly HttpClient _httpClient;
	private readonly AsyncLock _lock;
	private readonly FixedSizeBufferWriter _updateBufferWriter;
	private readonly Utf8JsonWriter _updateWriter;
	private readonly DnsSdDeviceLifetime _lifetime;
	private ulong _lastUpdateTimestamp;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<ILightDeviceFeature> _lightFeatures;

	private ElgatoLightDriver
	(
		DnsSdDeviceLifetime lifetime,
		HttpClient httpClient,
		ImmutableArray<ElgatoLight> lights,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		var lightStates = new LightState[lights.Length];
		for (int i = 0; i < lights.Length; i++)
		{
			lightStates[i] = new(this, lights[i], (uint)i);
		}
		_lastUpdateTimestamp = (ulong)Stopwatch.GetTimestamp();
		_lights = lightStates;
		_httpClient = httpClient;
		_lifetime = lifetime;
		_genericFeatures = configurationKey.UniqueId is not null ?
			FeatureSet.Create<IGenericDeviceFeature, ElgatoLightDriver, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Empty<IGenericDeviceFeature>();
		_lightFeatures = FeatureSet.Create<ILightDeviceFeature, ElgatoLightDriver, ILightControllerFeature, IPolledLightControllerFeature>(this);
		_lock = new();
		// This is not ideal, as it requires allocating an array way larger than what is actually necessary, but it will do for now. (At least it will avoid further allocations)
		_updateBufferWriter = new(1024);
		_updateWriter = new(_updateBufferWriter);
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
		var cancellationToken = _cancellationTokenSource!.Token;
		try
		{
			await RefreshLightsAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			// TODO: Log.
		}
	}

	private async Task RefreshLightsAsync(CancellationToken cancellationToken)
	{
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if ((ulong)Stopwatch.GetTimestamp() - _lastUpdateTimestamp < UpdateInterval) return;

			ElgatoLights lights;
			try
			{
				using var stream = await _httpClient.GetStreamAsync(LightsPath, cancellationToken).ConfigureAwait(false);
				lights = JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.ElgatoLights);
			}
			catch (HttpRequestException ex) when (IsTimeoutOrConnectionReset(ex))
			{
				_lifetime.NotifyDeviceOffline();
				return;
			}
			UpdateLightStates(lights);
		}
	}

	private Task SendUpdateAsync(uint index, ElgatoLightUpdate update, CancellationToken cancellationToken)
	{
		var lights = new ElgatoLightUpdate[index + 1];
		lights[index] = update;
		return SendUpdateAsync(new ElgatoLightsUpdate { Lights = ImmutableCollectionsMarshal.AsImmutableArray(lights) }, cancellationToken);
	}

	private async Task SendUpdateAsync(ElgatoLightsUpdate update, CancellationToken cancellationToken)
	{
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var bufferWriter = _updateBufferWriter;
			_updateBufferWriter.Reset();
			var writer = _updateWriter;
			_updateWriter.Reset();
			JsonSerializer.Serialize(writer, update, SourceGenerationContext.Default.ElgatoLightsUpdate);
			using var json = new ByteArrayContent(bufferWriter.GetBuffer(), 0, bufferWriter.Length);
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
			using (var readStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
			{
				UpdateLightStates(JsonSerializer.Deserialize(readStream, SourceGenerationContext.Default.ElgatoLights));
			}
		}
	}

	private Task SwitchLightAsync(uint index, bool isOn, CancellationToken cancellationToken)
		=> SendUpdateAsync(index, new() { On = isOn ? (byte)1 : (byte)0 }, cancellationToken);

	private async Task SetBrightnessAsync(uint index, byte brightness, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(brightness, 100);
		await SendUpdateAsync(index, new() { Brightness = brightness }, cancellationToken);
	}

	private async Task SetTemperatureAsync(uint index, ushort temperature, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(temperature, 143);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(temperature, 344);
		await SendUpdateAsync(index, new() { Temperature = temperature }, cancellationToken);
	}

	private void UpdateLightStates(ElgatoLights lights)
	{
		_lastUpdateTimestamp = (ulong)Stopwatch.GetTimestamp();

		var src = lights.Lights;
		var dst = _lights;

		if (src.Length != dst.Length) throw new InvalidOperationException();

		for (int i = 0; i < src.Length; i++)
		{
			dst[i].Update(src[i]);
		}
	}

	private static uint InternalValueToTemperature(ushort value) => 1000000 / Math.Clamp((uint)value, 143, 344);

	private static ushort TemperatureToInternalValue(uint temperature) => (ushort)(1000000 / Math.Clamp(temperature, 2906, 6993));

	public override DeviceCategory DeviceCategory => DeviceCategory.Light;

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<ILightDeviceFeature> IDeviceDriver<ILightDeviceFeature>.Features => _lightFeatures;

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	ImmutableArray<ILight> ILightControllerFeature.Lights => ImmutableCollectionsMarshal.AsImmutableArray(Unsafe.As<ILight[]>(_lights));

	ValueTask IPolledLightControllerFeature.RequestRefreshAsync(CancellationToken cancellationToken) => new(RefreshLightsAsync(cancellationToken));

	private sealed class LightState : ILight, ILightBrightness, ILightTemperature, ILight<TemperatureAdjustableDimmableLightState>
	{
		private readonly ElgatoLightDriver _driver;
		private event LightChangeHandler<TemperatureAdjustableDimmableLightState>? Changed;
		private bool _isOn;
		private byte _brightness;
		private ushort _temperature;
		private readonly uint _index;

		public LightState(ElgatoLightDriver driver, ElgatoLight light, uint index)
		{
			_driver = driver;
			_index = index;
			Update(light);
		}

		internal void Update(ElgatoLight light)
		{
			bool isOn = light.On != 0;
			bool isChanged = isOn ^ _isOn;
			_isOn = isOn;
			if (_brightness != light.Brightness)
			{
				_brightness = light.Brightness;
				isChanged = true;
			}
			if (_temperature != light.Temperature)
			{
				_temperature = light.Temperature;
				isChanged = true;
			}
			if (isChanged && Changed is { } changed)
			{
				// NB: This will be called inside the device lock. Just to keep in mind in case this cause problems.
				// Probably another reason to migrate from events to event queues :(
				changed.Invoke(_driver, State);
			}
		}

		private TemperatureAdjustableDimmableLightState State => new(_isOn, _brightness, InternalValueToTemperature(_temperature));

		Guid ILight.Id => LightIds[(int)_index];

		bool ILight.IsOn => _isOn;

		ValueTask ILight.SwitchAsync(bool isOn, CancellationToken cancellationToken)
			=> new(_driver.SwitchLightAsync(_index, isOn, cancellationToken));

		byte ILightBrightness.Value => _brightness;

		// We avoid sending updates to the device for brightness values that are already the (cached) current one. (Only downside is if the cached value is very outdated)
		ValueTask ILightBrightness.SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
			=> brightness != _brightness ? new(_driver.SetBrightnessAsync(_index, brightness, cancellationToken)) : ValueTask.CompletedTask;

		// It is simpler to straight up map the temperature values to what the conversion formula gives, which is a K value between 2906 to 6993.
		// That way, we are able to expose enough granularity to the UI.
		uint ILightTemperature.Minimum => 2906;
		uint ILightTemperature.Maximum => 6993;
		uint ILightTemperature.Value => InternalValueToTemperature(_temperature);

		// We avoid sending updates to the device for temperature values that are already the (cached) current one. (Only downside is if the cached value is very outdated)
		// This should be especially useful for temperature, as when driven by a UI, the UI will not be able to tell that two values end up in the same bucket.
		ValueTask ILightTemperature.SetTemperatureAsync(uint temperature, CancellationToken cancellationToken)
			=> TemperatureToInternalValue(temperature) is ushort value && value != _temperature ? new(_driver.SetTemperatureAsync(_index, value, cancellationToken)) : ValueTask.CompletedTask;

		TemperatureAdjustableDimmableLightState ILight<TemperatureAdjustableDimmableLightState>.CurrentState => new(_isOn, _brightness, InternalValueToTemperature(_temperature));

		event LightChangeHandler<TemperatureAdjustableDimmableLightState> ILight<TemperatureAdjustableDimmableLightState>.Changed
		{
			add => Changed += value;
			remove => Changed -= value;
		}

		ValueTask ILight<TemperatureAdjustableDimmableLightState>.UpdateAsync(TemperatureAdjustableDimmableLightState state, CancellationToken cancellationToken)
			=> new(_driver.SendUpdateAsync(_index, new() { On = state.IsOn ? (byte)1 : (byte)0, Brightness = state.Brightness, Temperature = TemperatureToInternalValue(state.Temperature) }, cancellationToken));
	}
}

internal sealed class FixedSizeBufferWriter : IBufferWriter<byte>
{
	private readonly byte[] _data;
	private int _length;

	public FixedSizeBufferWriter(int capacity) => _data = GC.AllocateUninitializedArray<byte>(capacity, false);

	public void Reset() => _length = 0;

	public int Capacity => _data.Length;
	public int Length => _length;

	public byte[] GetBuffer() => _data;

	void IBufferWriter<byte>.Advance(int count)
	{
		int length = _length + count;
		if (length > _data.Length) throw new InvalidOperationException("This operation would go over the allocated buffer size.");
		_length = length;
	}

	Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint) => sizeHint > 0 ? _data.AsMemory(_length, sizeHint) : _data.AsMemory(_length);

	Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint) => sizeHint > 0 ? _data.AsSpan(_length, sizeHint) : _data.AsSpan(_length);
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

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ElgatoAccessoryInfo), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ElgatoLights), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ElgatoLightsUpdate), GenerationMode = JsonSourceGenerationMode.Serialization)]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
