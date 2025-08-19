using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Exo.ColorFormats;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Lighting;
using Exo.Features.Lights;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Elgato.Lights;

public abstract partial class ElgatoLightDriver : Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<ILightDeviceFeature>,
	IDeviceSerialNumberFeature,
	ILightControllerFeature,
	IPolledLightControllerFeature
{
	private protected const string AccessoryInfoPath = "/elgato/accessory-info";
	private protected const string LightsPath = "/elgato/lights";
	// Putting this here as a reminder that none of the settings features have been implemented yet.
	private protected const string LightsSettingsPath = "/elgato/lights/settings";

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

		string? model = null;
		foreach (string s in textAttributes)
		{
			if (s.StartsWith("md="))
			{
				model = s[3..];
			}
		}

		ElgatoAccessoryInfo accessoryInfo;
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

			// The JSON format is mostly the same between all light types, but since they all rely on different fields, it is better to separate them.
			switch (accessoryInfo.ProductName)
			{
			case "Elgato Light Strip Pro":
				{
					ElgatoLights<ElgatoLedStripProLight> lights;
					try
					{
						using var stream = await httpClient.GetStreamAsync(LightsPath, cancellationToken).ConfigureAwait(false);
						lights = JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.ElgatoLightsElgatoLedStripProLight);

						return new(keys, new ElgatoLedStripProDriver(deviceLifetime, httpClient, lights.Lights, accessoryInfo.LedCount, instanceName, new DeviceConfigurationKey("elg", fullName, model ?? fullName, accessoryInfo.SerialNumber)));
					}
					catch (HttpRequestException ex) when (IsTimeout(ex))
					{
						// Repackage a timeout into a device offline exception to notify the caller.
						throw new DeviceOfflineException(instanceName);
					}
				}
			case "Elgato Light Strip":
				{
					ElgatoLights<ElgatoColorLight> lights;
					try
					{
						using var stream = await httpClient.GetStreamAsync(LightsPath, cancellationToken).ConfigureAwait(false);
						lights = JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.ElgatoLightsElgatoColorLight);

						return new(keys, new ElgatoColorLightDriver(deviceLifetime, httpClient, lights.Lights, instanceName, new DeviceConfigurationKey("elg", fullName, model ?? fullName, accessoryInfo.SerialNumber)));
					}
					catch (HttpRequestException ex) when (IsTimeout(ex))
					{
						// Repackage a timeout into a device offline exception to notify the caller.
						throw new DeviceOfflineException(instanceName);
					}
				}
			default:
				{
					ElgatoLights<ElgatoLight> lights;
					try
					{
						using var stream = await httpClient.GetStreamAsync(LightsPath, cancellationToken).ConfigureAwait(false);
						lights = JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.ElgatoLightsElgatoLight);
					}
					catch (HttpRequestException ex) when (IsTimeout(ex))
					{
						// Repackage a timeout into a device offline exception to notify the caller.
						throw new DeviceOfflineException(instanceName);
					}

					if ((uint)lights.NumberOfLights > (uint)LightIds.Count)
					{
						throw new InvalidOperationException($"Devices with {lights.NumberOfLights} lights are not yet supported. Please submit a support request to increase the limit.");
					}

					return new(keys, new ElgatoBasicLightDriver(deviceLifetime, httpClient, lights.Lights, instanceName, new DeviceConfigurationKey("elg", fullName, model ?? fullName, accessoryInfo.SerialNumber)));
				}
			}
		}
		catch
		{
			httpClient.Dispose();
			throw;
		}
	}

	private protected static bool IsTimeout(HttpRequestException ex) => ex.InnerException is SocketException sex && IsTimeout(sex);
	private protected static bool IsTimeout(SocketException ex) => ex.SocketErrorCode == SocketError.TimedOut;

	private protected static bool IsTimeoutOrConnectionReset(HttpRequestException ex) => ex.InnerException switch
	{
		IOException ioex => IsTimeoutOrConnectionReset(ioex),
		SocketException sex => IsTimeoutOrConnectionReset(sex),
		_ => false
	};

	private protected static bool IsTimeoutOrConnectionReset(IOException ex) => ex.InnerException is SocketException sex && IsTimeoutOrConnectionReset(sex);
	private protected static bool IsTimeoutOrConnectionReset(SocketException ex) => ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.TimedOut;

	// Define a reasonable status update interval to avoid too frequent HTTP requests.
	// For now, le'ts assume that refreshing more than once every 10s is unreasonnable.
	private protected static readonly ulong UpdateInterval = (ulong)(10 * Stopwatch.Frequency);

	private readonly Array _lights;
	private readonly HttpClient _httpClient;
	private readonly AsyncLock _lock;
	private readonly object _updateBufferStorage;
	private readonly Utf8JsonWriter _updateWriter;
	private readonly DnsSdDeviceLifetime _lifetime;
	private protected ulong LastUpdateTimestamp;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<ILightDeviceFeature> _lightFeatures;

	private protected ElgatoLightDriver
	(
		DnsSdDeviceLifetime lifetime,
		HttpClient httpClient,
		bool useFixedBuffer,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		LastUpdateTimestamp = (ulong)Stopwatch.GetTimestamp();
		Unsafe.SkipInit(out _lights);
		_httpClient = httpClient;
		_lifetime = lifetime;
		_genericFeatures = configurationKey.UniqueId is not null ?
			FeatureSet.Create<IGenericDeviceFeature, ElgatoLightDriver, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Empty<IGenericDeviceFeature>();
		_lightFeatures = FeatureSet.Create<ILightDeviceFeature, ElgatoLightDriver, ILightControllerFeature, IPolledLightControllerFeature>(this);
		_lock = new();
		// This is not ideal, as it requires allocating an array way larger than what is actually necessary, but it will do for now. (At least it will avoid further allocations)
		if (useFixedBuffer)
		{
			var w = new FixedSizeBufferWriter(1024);
			_updateBufferStorage = w;
			_updateWriter = new(w);
		}
		else
		{
			var s = new MemoryStream();
			_updateBufferStorage = s;
			_updateWriter = new(s);
		}
		_cancellationTokenSource = new();
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

	private protected void SetLights(Array lights) => Unsafe.AsRef(in _lights) = lights;
	private protected Array GetLights() => _lights;

	private protected HttpClient HttpClient => _httpClient;
	private protected AsyncLock Lock => _lock;
	private protected DnsSdDeviceLifetime Lifetime => _lifetime;

	private protected Utf8JsonWriter ResetAndGetUpdateWriter()
	{
		if (_updateBufferStorage is FixedSizeBufferWriter fsbw)
		{
			fsbw.Reset();
		}
		else
		{
			Unsafe.As<MemoryStream>(_updateBufferStorage).Position = 0;
		}

		_updateWriter.Reset();
		return _updateWriter;
	}

	private protected ArraySegment<byte> GetCurrentWriteBuffer()
	{
		if (_updateBufferStorage is FixedSizeBufferWriter fsbw)
		{
			return new(fsbw.GetBuffer(), 0, fsbw.Length);
		}
		else
		{
			var ms = Unsafe.As<MemoryStream>(_updateBufferStorage);
			return new(ms.GetBuffer(), 0, (int)ms.Position);
		}
	}

	private protected async void OnDeviceUpdated(object? sender, EventArgs e)
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

	private protected abstract Task RefreshLightsAsync(CancellationToken cancellationToken);

	private protected static uint InternalValueToTemperature(ushort value) => 1000000 / Math.Clamp((uint)value, 143, 344);

	private protected static ushort TemperatureToInternalValue(uint temperature) => (ushort)(1000000 / Math.Clamp(temperature, 2906, 6993));

	public override DeviceCategory DeviceCategory => DeviceCategory.Light;

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<ILightDeviceFeature> IDeviceDriver<ILightDeviceFeature>.Features => _lightFeatures;

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	ImmutableArray<ILight> ILightControllerFeature.Lights => ImmutableCollectionsMarshal.AsImmutableArray(Unsafe.As<ILight[]>(_lights));

	ValueTask IPolledLightControllerFeature.RequestRefreshAsync(CancellationToken cancellationToken) => new(RefreshLightsAsync(cancellationToken));

	internal abstract class LightState
	{
		private readonly ElgatoLightDriver _driver;

		protected LightState(ElgatoLightDriver driver) => _driver = driver;

		protected ElgatoLightDriver Driver => _driver;
	}
}

internal abstract class ElgatoLightDriver<TLight, TLightUpdate, TLightState> : ElgatoLightDriver
	where TLight : struct
	where TLightUpdate : struct
	where TLightState : ElgatoLightDriver<TLight, TLightUpdate, TLightState>.LightState
{
	private protected ElgatoLightDriver
	(
		DnsSdDeviceLifetime lifetime,
		HttpClient httpClient,
		bool useFixedBuffer,
		ImmutableArray<TLight> lights,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(lifetime, httpClient, useFixedBuffer, friendlyName, configurationKey)
	{
		SetLights(CreateLightStates(lights));
		lifetime.DeviceUpdated += new(OnDeviceUpdated);
	}

	protected abstract JsonTypeInfo<ElgatoLights<TLight>> LightsJsonTypeInfo { get; }
	protected abstract JsonTypeInfo<ElgatoLightsUpdate<TLightUpdate>> LightsUpdateJsonTypeInfo { get; }

	protected TLightState[] Lights => Unsafe.As<TLightState[]>(GetLights());

	protected abstract TLightState[] CreateLightStates(ImmutableArray<TLight> lights);

	private protected override async Task RefreshLightsAsync(CancellationToken cancellationToken)
	{
		using (await Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if ((ulong)Stopwatch.GetTimestamp() - LastUpdateTimestamp < UpdateInterval) return;

			ElgatoLights<TLight> lights;
			try
			{
				using var stream = await HttpClient.GetStreamAsync(LightsPath, cancellationToken).ConfigureAwait(false);
				lights = JsonSerializer.Deserialize(stream, LightsJsonTypeInfo);
			}
			catch (HttpRequestException ex) when (IsTimeoutOrConnectionReset(ex))
			{
				Lifetime.NotifyDeviceOffline();
				return;
			}
			UpdateLightStates(lights);
		}
	}

	private protected Task SendUpdateAsync(uint index, TLightUpdate update, CancellationToken cancellationToken)
	{
		var lights = new TLightUpdate[index + 1];
		lights[index] = update;
		return SendUpdateAsync(new ElgatoLightsUpdate<TLightUpdate> { Lights = ImmutableCollectionsMarshal.AsImmutableArray(lights), NumberOfLights = (int)(index + 1) }, cancellationToken);
	}

	private async Task SendUpdateAsync(ElgatoLightsUpdate<TLightUpdate> update, CancellationToken cancellationToken)
	{
		using (await Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			// The below is code to send an update to the device.
			// It can perhaps be simplified a bit, but don't be fooled.
			// The device is very touchy on the kind of requests it accepts and will reject chunked encoding.
			// However, it does seemingly not require any particular header to be specified.
			var writer = ResetAndGetUpdateWriter();
			JsonSerializer.Serialize(writer, update, LightsUpdateJsonTypeInfo);
			var buffer = GetCurrentWriteBuffer();
			using var json = new ByteArrayContent(buffer.Array!, buffer.Offset, buffer.Count);
			using var request = new HttpRequestMessage(HttpMethod.Put, LightsPath) { Content = json };
			HttpResponseMessage response;
			try
			{
				response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException ex) when (IsTimeoutOrConnectionReset(ex))
			{
				Lifetime.NotifyDeviceOffline();
				return;
			}
			using (response)
			using (var readStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
			{
				UpdateLightStates(JsonSerializer.Deserialize(readStream, LightsJsonTypeInfo));
			}
		}
	}

	private void UpdateLightStates(ElgatoLights<TLight> lights)
	{
		LastUpdateTimestamp = (ulong)Stopwatch.GetTimestamp();

		var src = lights.Lights;
		var dst = Lights;

		if (src.Length != dst.Length) throw new InvalidOperationException();

		for (int i = 0; i < src.Length; i++)
		{
			dst[i].Update(src[i]);
		}
	}

	internal abstract new class LightState : ElgatoLightDriver.LightState
	{
		public LightState(ElgatoLightDriver driver)
			: base(driver)
		{
		}

		internal abstract void Update(TLight light);
	}

	internal abstract class LightState<TState> : LightState, ILight<TState>
		where TState : struct, ILightState
	{
		private event LightChangeHandler<TState>? _changed;
		private readonly uint _index;
		protected bool IsOn;

		public LightState(ElgatoLightDriver<TLight, TLightUpdate, TLightState> driver, TLight light, uint index)
			: base(driver)
		{
			_index = index;
			Update(light);
		}

		protected uint Index => _index;

		protected new ElgatoLightDriver<TLight, TLightUpdate, TLightState> Driver => Unsafe.As<ElgatoLightDriver<TLight, TLightUpdate, TLightState>>(base.Driver);

		Guid ILight.Id => LightIds[(int)Index];
		bool ILight.IsOn => IsOn;

		protected abstract ValueTask SwitchAsync(bool isOn, CancellationToken cancellationToken);

		ValueTask ILight.SwitchAsync(bool isOn, CancellationToken cancellationToken)
			=> SwitchAsync(isOn, cancellationToken);

		protected LightChangeHandler<TState>? Changed => _changed;

		event LightChangeHandler<TState> ILight<TState>.Changed
		{
			add => _changed += value;
			remove => _changed -= value;
		}

		protected abstract ValueTask UpdateAsync(TState state, CancellationToken cancellationToken);

		ValueTask ILight<TState>.UpdateAsync(TState state, CancellationToken cancellationToken)
			=> UpdateAsync(state, cancellationToken);

		protected abstract TState CurrentState { get; }

		TState ILight<TState>.CurrentState => CurrentState;
	}
}

internal sealed class ElgatoBasicLightDriver : ElgatoLightDriver<ElgatoLight, ElgatoLightUpdate, ElgatoBasicLightDriver.BasicLightState>
{
	public ElgatoBasicLightDriver
	(
		DnsSdDeviceLifetime lifetime,
		HttpClient httpClient,
		ImmutableArray<ElgatoLight> lights,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(lifetime, httpClient, true, lights, friendlyName, configurationKey)
	{
	}

	protected override JsonTypeInfo<ElgatoLights<ElgatoLight>> LightsJsonTypeInfo => SourceGenerationContext.Default.ElgatoLightsElgatoLight;
	protected override JsonTypeInfo<ElgatoLightsUpdate<ElgatoLightUpdate>> LightsUpdateJsonTypeInfo => SourceGenerationContext.Default.ElgatoLightsUpdateElgatoLightUpdate;

	protected override BasicLightState[] CreateLightStates(ImmutableArray<ElgatoLight> lights)
	{
		var lightStates = new BasicLightState[lights.Length];
		for (int i = 0; i < lights.Length; i++)
		{
			lightStates[i] = new(this, lights[i], (uint)i);
		}
		return lightStates;
	}

	internal sealed class BasicLightState : LightState<TemperatureAdjustableDimmableLightState>, ILightBrightness, ILightTemperature
	{
		private byte _brightness;
		private ushort _temperature;

		public BasicLightState(ElgatoBasicLightDriver driver, ElgatoLight light, uint index)
			: base(driver, light, index)
		{
		}

		private new ElgatoBasicLightDriver Driver => Unsafe.As<ElgatoBasicLightDriver>(base.Driver);

		internal override void Update(ElgatoLight light)
		{
			bool isOn = light.On != 0;
			bool isChanged = isOn ^ IsOn;
			IsOn = isOn;
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
				changed.Invoke(Driver, CurrentState);
			}
		}

		byte ILightBrightness.Value => _brightness;

		// It is simpler to straight up map the temperature values to what the conversion formula gives, which is a K value between 2906 to 6993.
		// That way, we are able to expose enough granularity to the UI.
		uint ILightTemperature.Minimum => 2906;
		uint ILightTemperature.Maximum => 6993;
		uint ILightTemperature.Value => InternalValueToTemperature(_temperature);

		protected override ValueTask SwitchAsync(bool isOn, CancellationToken cancellationToken)
			=> new(Driver.SendUpdateAsync(Index, new() { On = isOn ? (byte)1 : (byte)0 }, cancellationToken));

		private async Task SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(brightness, 100);
			await Driver.SendUpdateAsync(Index, new() { Brightness = brightness }, cancellationToken);
		}

		private async Task SetTemperatureAsync(ushort temperature, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(temperature, 143);
			ArgumentOutOfRangeException.ThrowIfGreaterThan(temperature, 344);
			await Driver.SendUpdateAsync(Index, new() { Temperature = temperature }, cancellationToken);
		}

		protected override TemperatureAdjustableDimmableLightState CurrentState => new(IsOn, _brightness, InternalValueToTemperature(_temperature));

		protected override ValueTask UpdateAsync(TemperatureAdjustableDimmableLightState state, CancellationToken cancellationToken)
			=> new(Driver.SendUpdateAsync(Index, new ElgatoLightUpdate() { On = state.IsOn ? (byte)1 : (byte)0, Brightness = state.Brightness, Temperature = TemperatureToInternalValue(state.Temperature) }, cancellationToken));

		// We avoid sending updates to the device for brightness values that are already the (cached) current one. (Only downside is if the cached value is very outdated)
		ValueTask ILightBrightness.SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
			=> brightness != _brightness ? new(SetBrightnessAsync(brightness, cancellationToken)) : ValueTask.CompletedTask;

		// We avoid sending updates to the device for temperature values that are already the (cached) current one. (Only downside is if the cached value is very outdated)
		// This should be especially useful for temperature, as when driven by a UI, the UI will not be able to tell that two values end up in the same bucket.
		ValueTask ILightTemperature.SetTemperatureAsync(uint temperature, CancellationToken cancellationToken)
			=> TemperatureToInternalValue(temperature) is ushort value && value != _temperature ? new(SetTemperatureAsync(value, cancellationToken)) : ValueTask.CompletedTask;
	}
}

internal sealed class ElgatoColorLightDriver : ElgatoLightDriver<ElgatoColorLight, ElgatoColorLightUpdate, ElgatoColorLightDriver.ColorLightState>
{
	public ElgatoColorLightDriver
	(
		DnsSdDeviceLifetime lifetime,
		HttpClient httpClient,
		ImmutableArray<ElgatoColorLight> lights,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(lifetime, httpClient, true, lights, friendlyName, configurationKey)
	{
	}

	protected override JsonTypeInfo<ElgatoLights<ElgatoColorLight>> LightsJsonTypeInfo => SourceGenerationContext.Default.ElgatoLightsElgatoColorLight;
	protected override JsonTypeInfo<ElgatoLightsUpdate<ElgatoColorLightUpdate>> LightsUpdateJsonTypeInfo => SourceGenerationContext.Default.ElgatoLightsUpdateElgatoColorLightUpdate;

	protected override ColorLightState[] CreateLightStates(ImmutableArray<ElgatoColorLight> lights)
	{
		var lightStates = new ColorLightState[lights.Length];
		for (int i = 0; i < lights.Length; i++)
		{
			lightStates[i] = new(this, lights[i], (uint)i);
		}
		return lightStates;
	}

	internal sealed class ColorLightState : LightState<HsbColorLightState>, ILightBrightness, ILightHue, ILightSaturation
	{
		private byte _brightness;
		private ushort _hue;
		private byte _saturation;

		public ColorLightState(ElgatoColorLightDriver driver, ElgatoColorLight light, uint index)
			: base(driver, light, index)
		{
		}

		private new ElgatoColorLightDriver Driver => Unsafe.As<ElgatoColorLightDriver>(base.Driver);

		internal override void Update(ElgatoColorLight light)
		{
			bool isOn = light.On != 0;
			bool isChanged = isOn ^ IsOn;
			IsOn = isOn;
			if (_brightness != light.Brightness)
			{
				_brightness = light.Brightness;
				isChanged = true;
			}
			if (_hue != light.Hue)
			{
				_hue = light.Hue;
				isChanged = true;
			}
			if (_saturation != light.Saturation)
			{
				_saturation = light.Saturation;
				isChanged = true;
			}
			if (isChanged && Changed is { } changed)
			{
				// NB: This will be called inside the device lock. Just to keep in mind in case this cause problems.
				// Probably another reason to migrate from events to event queues :(
				changed.Invoke(Driver, CurrentState);
			}
		}

		byte ILightBrightness.Value => _brightness;
		ushort ILightHue.Value => _hue;
		byte ILightSaturation.Value => _saturation;

		protected override ValueTask SwitchAsync(bool isOn, CancellationToken cancellationToken)
			=> new(Driver.SendUpdateAsync(Index, new() { On = isOn ? (byte)1 : (byte)0 }, cancellationToken));

		private async Task SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(brightness, 100);
			await Driver.SendUpdateAsync(Index, new() { Brightness = brightness }, cancellationToken);
		}

		private async Task SetHueAsync(ushort hue, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(hue, 360);
			await Driver.SendUpdateAsync(Index, new() { Hue = hue }, cancellationToken);
		}
		private async Task SetSaturationAsync(byte saturation, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(saturation, 100);
			await Driver.SendUpdateAsync(Index, new() { Saturation = saturation }, cancellationToken);
		}

		protected override HsbColorLightState CurrentState => new(IsOn, _hue, _saturation, _brightness);

		protected override ValueTask UpdateAsync(HsbColorLightState state, CancellationToken cancellationToken)
			=> new(Driver.SendUpdateAsync(Index, new() { On = state.IsOn ? (byte)1 : (byte)0, Brightness = state.Brightness, Hue = state.Hue, Saturation = state.Saturation }, cancellationToken));

		// We avoid sending updates to the device for brightness values that are already the (cached) current one. (Only downside is if the cached value is very outdated)
		ValueTask ILightBrightness.SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
			=> brightness != _brightness ? new(SetBrightnessAsync(brightness, cancellationToken)) : ValueTask.CompletedTask;

		ValueTask ILightHue.SetHueAsync(ushort hue, CancellationToken cancellationToken)
			=> hue != _hue ? new(SetHueAsync(hue, cancellationToken)) : ValueTask.CompletedTask;

		ValueTask ILightSaturation.SetSaturationAsync(byte saturation, CancellationToken cancellationToken)
			=> saturation != _saturation ? new(SetSaturationAsync(saturation, cancellationToken)) : ValueTask.CompletedTask;
	}
}

internal sealed class ElgatoLedStripProDriver :
	ElgatoLightDriver<ElgatoLedStripProLight, ElgatoLedStripProLightUpdate, ElgatoLedStripProDriver.LedStripProLightState>,
	IDeviceDriver<ILightingDeviceFeature>
{
	private const string ExoEffectId = "com.exo.effect";
	// From the Elgato docs.
	private const int MaximumFrameCount = 900;
	private readonly IDeviceFeatureSet<ILightingDeviceFeature> _lightingFeatures;
	private readonly ushort _ledCount;

	public ElgatoLedStripProDriver
	(
		DnsSdDeviceLifetime lifetime,
		HttpClient httpClient,
		ImmutableArray<ElgatoLedStripProLight> lights,
		ushort ledCount,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(lifetime, httpClient, false, lights, friendlyName, configurationKey)
	{
		_ledCount = ledCount;
		_lightingFeatures = FeatureSet.Create<
			ILightingDeviceFeature,
			LedStripProLightState,
			IUnifiedLightingFeature,
			ILightingPersistenceFeature,
			ILightingBrightnessFeature,
			ILightingDynamicBrightnessChanges,
			ILightingDynamicEffectChanges>(Lights[0]);
	}

	IDeviceFeatureSet<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;

	protected override JsonTypeInfo<ElgatoLights<ElgatoLedStripProLight>> LightsJsonTypeInfo => SourceGenerationContext.Default.ElgatoLightsElgatoLedStripProLight;
	protected override JsonTypeInfo<ElgatoLightsUpdate<ElgatoLedStripProLightUpdate>> LightsUpdateJsonTypeInfo => SourceGenerationContext.Default.ElgatoLightsUpdateElgatoLedStripProLightUpdate;

	protected override LedStripProLightState[] CreateLightStates(ImmutableArray<ElgatoLedStripProLight> lights)
	{
		var lightStates = new LedStripProLightState[lights.Length];
		for (int i = 0; i < lights.Length; i++)
		{
			lightStates[i] = new(this, lights[i], (uint)i);
		}
		return lightStates;
	}

	private static ILightingEffect ParseEffect(ExoEffectMetaData metaData)
	{
		if (metaData.EffectId != default)
		{
			try
			{
				var effect = EffectSerializer.UnsafeDeserialize(metaData.EffectId, metaData.EffectData);
				if (effect is not null) return effect;
			}
			catch
			{
				// TODO: Log ?
			}
		}
		return NotApplicableEffect.SharedInstance;
	}

	private static ExoEffectMetaData ParseEffectMetadata(string effectId, JsonObject? metaData)
	{
		if (effectId == ExoEffectId)
		{
			try
			{
				return JsonSerializer.Deserialize(metaData, SourceGenerationContext.Default.ExoEffectMetaData);
			}
			catch
			{
				// TODO: Log ?
				goto EffectNotRecognized;
			}
		}
	EffectNotRecognized:;
		return new ExoEffectMetaData() { EffectId = default, EffectData = [] };
	}

	private static ExoEffectMetaData SerializeEffect(ILightingEffect effect)
	{
		// Probably could just use the LightingEffect class for serialization, but it depends on whether it will be kept at all.
		var serializedEffect = EffectSerializer.GetEffect(effect);
		return new() { EffectId = serializedEffect.EffectId, EffectData = serializedEffect.EffectData };
	}

	// The Light Strip Pro does not support any native effects.
	// All effects are a prerendered sequence of LEDs, which is a very flexible way of implementing effects, but 
	private static ElgatoLedStripProLightUpdate GenerateEffectUpdate(ILightingEffect effect, int ledCount, byte brightness)
	{
		switch (effect)
		{
		case DisabledEffect:
			return new() { On = 0 };
		case StaticColorEffect staticColorEffect:
			var hsv = HsvColor.FromRgb(staticColorEffect.Color);
			return new() { On = 1, Brightness = HsvColor.GetStandardValueByte(hsv.V), Hue = HsvColor.GetStandardHueUInt16(hsv.H), Saturation = HsvColor.GetStandardSaturationByte(hsv.S) };
		case IProgrammableLightingEffect<RgbColor> programmableEffect:
			return new()
			{
				On = 1,
				Brightness = brightness,
				Id = ExoEffectId,
				Name = "Exo Effect",
				SceneSet = Array.ConvertAll
				(
					ImmutableCollectionsMarshal.AsArray(programmableEffect.GetEffectFrames(ledCount, MaximumFrameCount))!,
					x => new ElgatoLedStripFrame(x)
				),
				MetaData = SerializeEffect(programmableEffect),
			};
		default:
			throw new InvalidOperationException($"Effects of type {effect.GetType()} are not supported.");
		}
	}

	internal sealed class LedStripProLightState :
		LightState<HsbColorLightState>,
		ILightBrightness,
		ILightHue,
		ILightSaturation,
		IUnifiedLightingFeature,
		ILightingPersistenceFeature,
		ILightingBrightnessFeature,
		ILightingDynamicBrightnessChanges,
		ILightingDynamicEffectChanges,
		ILightingZoneEffect<DisabledEffect>,
		ILightingZoneEffect<StaticColorEffect>,
		IProgrammableAddressableLightingZone<RgbColor>
	{
		// Values are scaled up in the same ways as in HsvColor in order to preserve an accurate representation of colors and direct compatibility with HsvColor.
		private ushort _hue;
		private byte _brightness;
		private byte _saturation;
		private string? _effectId;
		private ILightingEffect _effect;
		private ExoEffectMetaData _effectMetaData;
		private event EffectChangeHandler? EffectChanged;
		private event BrightnessChangeHandler? BrightnessChanged;

		public LedStripProLightState(ElgatoLedStripProDriver driver, ElgatoLedStripProLight light, uint index)
			: base(driver, light, index)
		{
			// The effect is initialized by a call to Update() in the base constructor.
			Unsafe.SkipInit(out _effect);
		}

		private new ElgatoLedStripProDriver Driver => Unsafe.As<ElgatoLedStripProDriver>(base.Driver);

		LightingPersistenceMode ILightingPersistenceFeature.PersistenceMode => LightingPersistenceMode.AlwaysPersisted;
		bool ILightingPersistenceFeature.HasDeviceManagedLighting => true;
		bool ILightingPersistenceFeature.HasDynamicPresence => true;

		// NB: Despite internally storing the HSV components as expanded values, we will only expose them as integers outside.
		// This could change in the future if necessary, but it should not be a problem excepted for the few extra conversions.

		byte ILightingBrightnessFeature.MaximumBrightness => 100;
		byte ILightingBrightnessFeature.CurrentBrightness
		{
			get => HsvColor.GetStandardValueByte(_brightness);
			set
			{
				_brightness = HsvColor.GetScaledValue(value);
				SetBrightnessAsync(value, default).GetAwaiter().GetResult();
			}
		}

		byte ILightBrightness.Maximum => 100;
		byte ILightBrightness.Value => HsvColor.GetStandardValueByte(_brightness);

		ushort ILightHue.Maximum => 359;
		ushort ILightHue.Value => HsvColor.GetStandardHueUInt16(_hue);

		byte ILightSaturation.Maximum => 100;
		byte ILightSaturation.Value => HsvColor.GetStandardSaturationByte(_saturation);

		bool IUnifiedLightingFeature.IsUnifiedLightingEnabled => true;

		Guid ILightingZone.ZoneId => LedStripProZoneId;

		ILightingEffect ILightingZone.GetCurrentEffect()
			=> IsOn ? _effect : DisabledEffect.SharedInstance;

		internal override void Update(ElgatoLedStripProLight light)
		{
			bool isOn = light.On != 0;
			bool wasOn = IsOn;
			bool isBrightnessChanged = false;
			bool isEffectChanged = false;
			IsOn = isOn;
			var hue = HsvColor.GetScaledHue(light.Hue);
			var saturation = HsvColor.GetScaledSaturation(light.Saturation);
			var brightness = HsvColor.GetScaledValue(light.Brightness);
			if (_hue != hue)
			{
				_hue = hue;
				isEffectChanged = true;
			}
			if (_saturation != saturation)
			{
				_saturation = saturation;
				isEffectChanged = true;
			}
			if (_brightness != brightness)
			{
				_brightness = brightness;
				isBrightnessChanged = true;
			}
			// Effect and Color are mutually exclusive, but we don't need to make the code more complicated here.
			// If effect ID is specified, then hue and saturation won't be. Same in the opposed way.
			if (_effectId != light.Id)
			{
				_effectId = light.Id;
				isEffectChanged = true;
				if (light.Id is not null)
				{
					_effect = ParseEffect(_effectMetaData = ParseEffectMetadata(light.Id, light.MetaData));
					goto NotifyChanges;
				}
			}
			else if (light.Id is not null)
			{
				var metaData = ParseEffectMetadata(light.Id, light.MetaData);
				if (metaData != _effectMetaData)
				{
					_effectMetaData = metaData;
					_effect = ParseEffect(metaData);
					goto NotifyChanges;
				}
				else if (isBrightnessChanged)
				{
					goto NotifyChanges;
				}
				return;
			}
			else if (!(isEffectChanged || isBrightnessChanged))
			{
				return;
			}
			else if (_effectId is not null)
			{
				goto NotifyChanges;
			}
			_effect = new StaticColorEffect(new HsvColor(hue, saturation, brightness).ToRgb());
		NotifyChanges:;
			if (Changed is { } changed)
			{
				// NB: This will be called inside the device lock. Just to keep in mind in case this cause problems.
				// Probably another reason to migrate from events to event queues :(
				changed.Invoke(Driver, CurrentState);
			}
			if (isBrightnessChanged)
			{
				NotifyBrightnessChanged(brightness);
			}
			if (isOn ^ wasOn || isOn && (isEffectChanged || isBrightnessChanged))
			{
				NotifyEffectChanged(this, IsOn ? _effect : DisabledEffect.SharedInstance);
			}
		}

		// The active effect ID *MUST* be sent when switching the light on, otherwise, any active effect will be reverted to a default color.
		protected override ValueTask SwitchAsync(bool isOn, CancellationToken cancellationToken)
			=> new(Driver.SendUpdateAsync(Index, new() { On = isOn ? (byte)1 : (byte)0, Id = _effectId }, cancellationToken));

		private async Task SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(brightness, 100);
			// TODO: Enable these updates but make sure we don't forget sending any update events.
			//_brightness = HsvColor.GetScaledValue(brightness);
			await Driver.SendUpdateAsync
			(
				Index,
				_effectId is not null ?
					new() { Brightness = brightness, Id = _effectId } :
					new() { Brightness = brightness, Hue = HsvColor.GetStandardHueUInt16(_hue), Saturation = HsvColor.GetStandardSaturationByte(_saturation) },
				cancellationToken
			);
		}

		private async Task SetHueAsync(ushort hue, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(hue, 360);
			// TODO: Enable these updates but make sure we don't forget sending any update events.
			//_hue = HsvColor.GetScaledHue(hue);
			await Driver.SendUpdateAsync
			(
				Index,
				new() { On = IsOn ? (byte)1 : (byte)0, Brightness = HsvColor.GetStandardValueByte(_brightness), Hue = hue, Saturation = HsvColor.GetStandardSaturationByte(_saturation) },
				cancellationToken
			);
		}
		private async Task SetSaturationAsync(byte saturation, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(saturation, 100);
			// TODO: Enable these updates but make sure we don't forget sending any update events.
			//_saturation = HsvColor.GetScaledSaturation(saturation);
			await Driver.SendUpdateAsync
			(
				Index,
				new() { On = IsOn ? (byte)1 : (byte)0, Brightness = HsvColor.GetStandardValueByte(_brightness), Hue = HsvColor.GetStandardHueUInt16(_hue), Saturation = saturation },
				cancellationToken
			);
		}

		protected override HsbColorLightState CurrentState => new(IsOn, _hue, _saturation, _brightness);

		protected override ValueTask UpdateAsync(HsbColorLightState state, CancellationToken cancellationToken)
			=> new(Driver.SendUpdateAsync(Index, new() { On = state.IsOn ? (byte)1 : (byte)0, Brightness = state.Brightness, Hue = state.Hue, Saturation = state.Saturation }, cancellationToken));

		// We avoid sending updates to the device for brightness values that are already the (cached) current one. (Only downside is if the cached value is very outdated)
		ValueTask ILightBrightness.SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
			=> brightness != _brightness ? new(SetBrightnessAsync(brightness, cancellationToken)) : ValueTask.CompletedTask;

		ValueTask ILightHue.SetHueAsync(ushort hue, CancellationToken cancellationToken)
			=> hue != _hue ? new(SetHueAsync(hue, cancellationToken)) : ValueTask.CompletedTask;

		ValueTask ILightSaturation.SetSaturationAsync(byte saturation, CancellationToken cancellationToken)
			=> saturation != _saturation ? new(SetSaturationAsync(saturation, cancellationToken)) : ValueTask.CompletedTask;

		event BrightnessChangeHandler ILightingDynamicBrightnessChanges.BrightnessChanged
		{
			add => BrightnessChanged += value;
			remove => BrightnessChanged -= value;
		}

		event EffectChangeHandler ILightingDynamicEffectChanges.EffectChanged
		{
			add => EffectChanged += value;
			remove => EffectChanged -= value;
		}

		private void NotifyBrightnessChanged(byte brightness)
			=> BrightnessChanged?.Invoke(Driver, brightness);

		private void NotifyEffectChanged(ILightingZone zone, ILightingEffect effect)
			=> EffectChanged?.Invoke(Driver, zone, effect);

		// TODO: Refactor the ApplyEffect logic to better handle the metadata updates.
		// We shouldn't regenerate effect metadata twice, which currently is the case.
		// Since there are only a handful of scenarios, we should probably get rid of GenerateEffectUpdate.
		private void ApplyEffect(ILightingEffect effect)
			=> Driver.SendUpdateAsync(Index, GenerateEffectUpdate(_effect = effect, Driver._ledCount, HsvColor.GetStandardValueByte(_brightness)), default).GetAwaiter().GetResult();

		private void Switch(bool isOn)
			=> SwitchAsync(isOn, default).GetAwaiter().GetResult();

		void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect)
		{
			if (IsOn)
			{
				Switch(false);
			}
		}

		void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect)
		{
			if (_effect is StaticColorEffect oldEffect &&
				oldEffect.Color == effect.Color)
			{
				if (!IsOn) Switch(true);
				return;
			}
			_effectMetaData = default;
			ApplyEffect(effect);
		}

		void IProgrammableAddressableLightingZone<RgbColor>.ApplyEffect(IProgrammableLightingEffect<RgbColor> effect)
		{
			if (ReferenceEquals(_effect, effect)) return;
			if (effect.GetType() != _effect.GetType())
			{
				var effectMetaData = SerializeEffect(effect);

				if (effectMetaData != _effectMetaData)
				{
					_effectMetaData = effectMetaData;
					_effect = effect;
					ApplyEffect(effect);
				}
			}
		}

		bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => _effect.TryGetEffect(out effect);
		bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => _effect.TryGetEffect(out effect);

		int IProgrammableAddressableLightingZone.MaximumFrameCount => MaximumFrameCount;
		int IAddressableLightingZone.AddressableLightCount => Driver._ledCount;
		AddressableLightingZoneCapabilities IAddressableLightingZone.Capabilities => AddressableLightingZoneCapabilities.Programmable;
		Type IAddressableLightingZone.ColorType => typeof(RgbColor);
	}
}

internal sealed class FixedSizeBufferWriter : IBufferWriter<byte>
{
	private readonly byte[] _data;
	private int _length;

	public FixedSizeBufferWriter(int capacity) => _data = capacity > 0 ? GC.AllocateUninitializedArray<byte>(capacity, false) : [];

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
	public ushort LedCount { get; init; }
}

internal readonly struct ElgatoWifiInfo
{
	public required string Ssid { get; init; }
	public int FrequencyMHz { get; init; }
	public int Rssi { get; init; }
}

internal readonly struct ElgatoLights<TLight>
	where TLight : struct
{
	public int NumberOfLights { get; init; }
	public required ImmutableArray<TLight> Lights { get; init; }
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

internal readonly struct ElgatoColorLight
{
	// 0 - 1
	public required byte On { get; init; }
	// 0 - 100
	public required byte Brightness { get; init; }
	// 0 - 359 ?
	public byte Hue { get; init; }
	// 0 - 100
	public byte Saturation { get; init; }
}

// This can represent either a color or an effect and its parameters
internal readonly struct ElgatoLedStripProLight
{
	// 0 - 1
	public required byte On { get; init; }
	// [0..100]
	public required float Brightness { get; init; }
	// [0..360[
	public float Hue { get; init; }
	// [0..100]
	public float Saturation { get; init; }
	// Id of the effect, in java namespace style, limited to 36 characters per Elgato official documentation.
	// This is an arbitrary string used to identify the current effect. The hardware has no care for it at all. (At least it shouldn't and doesn't seem to)
	public string? Id { get; init; }
	// User-friendly name of the effect. (Whatever language, not translated so English by default)
	public string? Name { get; init; }
	// Custom parameters of the effect. Also unused by the hardware, but helpful for the software to read back the current effect.
	public JsonObject? MetaData { get; init; }
	// Only present on newer firmware versions.
	// Presumably, 2 indicates that the scene is not saved to Flash and 0 indicates that it is totally saved. 1 always occurs in-between so it might indicate that it is being saved.
	// Previous firmwares were presumably always saving direct to flash, thus causing a noticeable delay (freeze) when switching scenes.
	// I'm assuming these new FW will improve the lifetime of the Flash slightly, but it would be better to be able to specify whether the effect should be persisted or not.
	public byte SceneSaveStatus { get; init; }
}

internal readonly struct ElgatoLightsUpdate<TLight>
	where TLight : struct
{
	public int? NumberOfLights { get; init; }
	public ImmutableArray<TLight> Lights { get; init; }
}

internal readonly struct ElgatoLightUpdate
{
	public byte? On { get; init; }
	public byte? Brightness { get; init; }
	public ushort? Temperature { get; init; }
}

internal readonly struct ElgatoColorLightUpdate
{
	public byte? On { get; init; }
	public byte? Brightness { get; init; }
	public ushort? Hue { get; init; }
	public byte? Saturation { get; init; }
}

internal readonly struct ElgatoLedStripProLightUpdate
{
	public byte? On { get; init; }
	// NB: It seems that start parameters would take floating point values, but somehow, the actual light parameters are coerced to integers ?
	public byte? Brightness { get; init; }
	public ushort? Hue { get; init; }
	public byte? Saturation { get; init; }
	public string? Id { get; init; }
	public string? Name { get; init; }
	public ExoEffectMetaData? MetaData { get; init; }
	public ElgatoLedStripFrame[]? SceneSet { get; init; }
}

[JsonConverter(typeof(JsonConverter))]
internal readonly struct ElgatoLedStripFrame
{
	private readonly LightingEffectFrame<RgbColor> _frame;

	public ElgatoLedStripFrame(LightingEffectFrame<RgbColor> frame) => _frame = frame;

	// TODO: Retry that in the future when JsonConverter is supported on properties.
	//[JsonConverter(typeof(RgbColorReadOnlyMemoryConverter))]
	public ReadOnlyMemory<RgbColor> RgbRaw => _frame.Colors;

	public ushort Duration => _frame.Duration;

	public sealed class JsonConverter : JsonConverter<ElgatoLedStripFrame>
	{
		public override ElgatoLedStripFrame Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> throw new NotImplementedException();

		public override void Write(Utf8JsonWriter writer, ElgatoLedStripFrame value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();
			writer.WriteBase64String("rgbRaw", MemoryMarshal.Cast<RgbColor, byte>(value.RgbRaw.Span));
			writer.WriteNumber("duration", value.Duration);
			writer.WriteEndObject();
		}
	}
}

//internal sealed class RgbColorReadOnlyMemoryConverter : JsonConverter<ReadOnlyMemory<RgbColor>>
//{
//	public override ReadOnlyMemory<RgbColor> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
//		=> throw new NotImplementedException();

//	public override void Write(Utf8JsonWriter writer, ReadOnlyMemory<RgbColor> value, JsonSerializerOptions options)
//		=> writer.WriteBase64StringValue(MemoryMarshal.Cast<RgbColor, byte>(value.Span));
//}

// This will be used as the internal format for effects.
// In order to fully support dynamic effects, we can proceed in a few different ways.
// Either we update the whole effect system to serialize to JsonObject and provide custom string IDs in order to have a more "compatible" implementation of effects.
// Or we set all effects using the same ID and store the raw effect data as serialized bytes using the default effect serializer.
// The second approach is less "native" regarding as to how the device is supposed to work, but it will be much easier to work with on our side.
internal readonly struct ExoEffectMetaData : IEquatable<ExoEffectMetaData>
{
	public required Guid EffectId { get; init; }
	public required byte[] EffectData { get; init; }

	public override bool Equals(object? obj) => obj is ExoEffectMetaData data && Equals(data);
	public bool Equals(ExoEffectMetaData other) => EffectId.Equals(other.EffectId) && EffectData.AsSpan().SequenceEqual(other.EffectData);
	public override int GetHashCode() => HashCode.Combine(EffectId, EffectData.Length);

	public static bool operator ==(ExoEffectMetaData left, ExoEffectMetaData right) => left.Equals(right);
	public static bool operator !=(ExoEffectMetaData left, ExoEffectMetaData right) => !(left == right);
}

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(ElgatoAccessoryInfo), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ElgatoLights<ElgatoLight>), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ElgatoLights<ElgatoColorLight>), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ElgatoLights<ElgatoLedStripProLight>), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ElgatoLightsUpdate<ElgatoLightUpdate>), GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(ElgatoLightsUpdate<ElgatoColorLightUpdate>), GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(ElgatoLightsUpdate<ElgatoLedStripProLightUpdate>), GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(ElgatoLightsUpdate<ExoEffectMetaData>), GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization)]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
