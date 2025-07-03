using System.Buffers;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Lights;

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

						return new(keys, new ElgatoLedStripProDriver(deviceLifetime, httpClient, lights.Lights, instanceName, new DeviceConfigurationKey("elg", fullName, model ?? fullName, accessoryInfo.SerialNumber)));
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
	private readonly FixedSizeBufferWriter _updateBufferWriter;
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
		_updateBufferWriter = new(1024);
		_updateWriter = new(_updateBufferWriter);
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
	private protected FixedSizeBufferWriter UpdateBufferWriter => _updateBufferWriter;
	private protected Utf8JsonWriter UpdateWriter => _updateWriter;
	private protected DnsSdDeviceLifetime Lifetime => _lifetime;

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
		ImmutableArray<TLight> lights,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(lifetime, httpClient, friendlyName, configurationKey)
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
		return SendUpdateAsync(new ElgatoLightsUpdate<TLightUpdate> { Lights = ImmutableCollectionsMarshal.AsImmutableArray(lights) }, cancellationToken);
	}

	private async Task SendUpdateAsync(ElgatoLightsUpdate<TLightUpdate> update, CancellationToken cancellationToken)
	{
		using (await Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			// The below is code to send an update to the device.
			// It can perhaps be simplified a bit, but don't be fooled.
			// The device is very touchy on the kind of requests it accepts and will reject chunked encoding.
			// However, it does seemingly not require any particular header to be specified.
			var bufferWriter = UpdateBufferWriter;
			UpdateBufferWriter.Reset();
			var writer = UpdateWriter;
			UpdateWriter.Reset();
			JsonSerializer.Serialize(writer, update, LightsUpdateJsonTypeInfo);
			using var json = new ByteArrayContent(bufferWriter.GetBuffer(), 0, bufferWriter.Length);
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
	) : base(lifetime, httpClient, lights, friendlyName, configurationKey)
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
	) : base(lifetime, httpClient, lights, friendlyName, configurationKey)
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

internal sealed class ElgatoLedStripProDriver : ElgatoLightDriver<ElgatoLedStripProLight, ElgatoLedStripProLightUpdate, ElgatoLedStripProDriver.LedStripProLightState>
{
	public ElgatoLedStripProDriver
	(
		DnsSdDeviceLifetime lifetime,
		HttpClient httpClient,
		ImmutableArray<ElgatoLedStripProLight> lights,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(lifetime, httpClient, lights, friendlyName, configurationKey)
	{
	}

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

	internal sealed class LedStripProLightState : LightState<HsbColorLightState>, ILightBrightness, ILightHue, ILightSaturation
	{
		private byte _brightness;
		private ushort _hue;
		private byte _saturation;
		private string? _effectId;

		public LedStripProLightState(ElgatoLedStripProDriver driver, ElgatoLedStripProLight light, uint index)
			: base(driver, light, index)
		{
		}

		private new ElgatoLedStripProDriver Driver => Unsafe.As<ElgatoLedStripProDriver>(base.Driver);

		internal override void Update(ElgatoLedStripProLight light)
		{
			bool isOn = light.On != 0;
			bool isChanged = isOn ^ IsOn;
			IsOn = isOn;
			if (_brightness != light.Brightness)
			{
				_brightness = light.Brightness;
				isChanged = true;
			}
			// Effect and Color are mutually exclusive, but we don't need to make the code more complicated here.
			// If effect ID is specified, then hue and saturation won't be. Same in the opposed way.
			if (_effectId != light.Id)
			{
				_effectId = light.Id;
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

		// The active effect ID *MUST* be sent when switching the light on, otherwise, any active effect will be reverted to a default color.
		protected override ValueTask SwitchAsync(bool isOn, CancellationToken cancellationToken)
			=> new(Driver.SendUpdateAsync(Index, new() { On = isOn ? (byte)1 : (byte)0, Id = _effectId }, cancellationToken));

		private async Task SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(brightness, 100);
			await Driver.SendUpdateAsync(Index, new() { Brightness = brightness, Id = _effectId }, cancellationToken);
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
	public int LedCount { get; init; }
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
	// 0 - 100
	public required byte Brightness { get; init; }
	// 0 - 359 ?
	public byte Hue { get; init; }
	// 0 - 100
	public byte Saturation { get; init; }
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

internal readonly struct ElgatoLedStripFrame
{
	public required byte[] RgbRaw { get; init; }
	public required ushort Duration { get; init; }
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
	public byte? Brightness { get; init; }
	public ushort? Hue { get; init; }
	public byte? Saturation { get; init; }
	public string? Id { get; init; }
	public string? Name { get; init; }
	public JsonObject? MetaData { get; init; }
	public ElgatoLedStripFrame[]? SceneSet { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(ElgatoAccessoryInfo), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ElgatoLights<ElgatoLight>), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ElgatoLights<ElgatoColorLight>), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ElgatoLights<ElgatoLedStripProLight>), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ElgatoLightsUpdate<ElgatoLightUpdate>), GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(ElgatoLightsUpdate<ElgatoColorLightUpdate>), GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(ElgatoLightsUpdate<ElgatoLedStripProLightUpdate>), GenerationMode = JsonSourceGenerationMode.Serialization)]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
