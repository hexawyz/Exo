using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Ipc;
using Exo.Memory;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Ipc;

internal sealed partial class UiPipeServerConnection : PipeServerConnection, IPipeServerConnection<UiPipeServerConnection>
{
	private static readonly UnboundedChannelOptions SensorChannelOptions = new() { SingleWriter = false, SingleReader = true, AllowSynchronousContinuations = true };

	public static UiPipeServerConnection Create(PipeServer<UiPipeServerConnection> server, NamedPipeServerStream stream)
	{
		var uiPipeServer = (UiPipeServer)server;
		return new
		(
			uiPipeServer.ConnectionLogger,
			server,
			stream,
			uiPipeServer.AssemblyLoader,
			uiPipeServer.CustomMenuService,
			uiPipeServer.ProgrammingService,
			uiPipeServer.ImageStorageService,
			uiPipeServer.DeviceRegistry,
			uiPipeServer.PowerService,
			uiPipeServer.MouseService,
			uiPipeServer.MonitorService,
			uiPipeServer.SensorService,
			uiPipeServer.CoolingService,
			uiPipeServer.LightingEffectMetadataService,
			uiPipeServer.LightingService,
			uiPipeServer.EmbeddedMonitorService,
			uiPipeServer.LightService
		);
	}

	private readonly IAssemblyLoader _assemblyLoader;
	private readonly CustomMenuService _customMenuService;
	private readonly ProgrammingService _programmingService;
	private readonly ImageStorageService _imageStorageService;
	private readonly DeviceRegistry _deviceRegistry;
	private readonly PowerService _powerService;
	private readonly MouseService _mouseService;
	private readonly MonitorService _monitorService;
	private readonly SensorService _sensorService;
	private readonly CoolingService _coolingService;
	private readonly LightingEffectMetadataService _lightingEffectMetadataService;
	private readonly LightingService _lightingService;
	private readonly EmbeddedMonitorService _embeddedMonitorService;
	private readonly LightService _lightService;
	private int _state;
	private readonly Dictionary<uint, SensorWatchState> _sensorWatchStates;
	private readonly Channel<SensorUpdate> _sensorUpdateChannel;
	private readonly Channel<SensorFavoritingRequest> _sensorFavoritingChannel;
	private string? _imageUploadImageName;
	private SharedMemory? _imageUploadSharedMemory;

	private UiPipeServerConnection
	(
		ILogger<UiPipeServerConnection> logger,
		PipeServer server,
		NamedPipeServerStream stream,
		IAssemblyLoader assemblyLoader,
		CustomMenuService customMenuService,
		ProgrammingService programmingService,
		ImageStorageService imageStorageService,
		DeviceRegistry deviceRegistry,
		PowerService powerService,
		MouseService mouseService,
		MonitorService monitorService,
		SensorService sensorService,
		CoolingService coolingService,
		LightingEffectMetadataService lightingEffectMetadataService,
		LightingService lightingService,
		EmbeddedMonitorService embeddedMonitorService,
		LightService lightService
	) : base(logger, server, stream)
	{
		_assemblyLoader = assemblyLoader;
		_customMenuService = customMenuService;
		_programmingService = programmingService;
		_imageStorageService = imageStorageService;
		_deviceRegistry = deviceRegistry;
		_powerService = powerService;
		_mouseService = mouseService;
		_monitorService = monitorService;
		_sensorService = sensorService;
		_coolingService = coolingService;
		_lightingEffectMetadataService = lightingEffectMetadataService;
		_lightingService = lightingService;
		_embeddedMonitorService = embeddedMonitorService;
		_lightService = lightService;
		using (var callingProcess = Process.GetProcessById(NativeMethods.GetNamedPipeClientProcessId(stream.SafePipeHandle)))
		{
			if (callingProcess.ProcessName != "Exo.Settings.Ui")
			{
				throw new UnauthorizedAccessException("The client is not authorized.");
			}
		}
		_sensorWatchStates = new();
		_sensorUpdateChannel = Channel.CreateUnbounded<SensorUpdate>(SensorChannelOptions);
		_sensorFavoritingChannel = Channel.CreateUnbounded<SensorFavoritingRequest>(SensorChannelOptions);
	}

	protected override ValueTask OnDisposedAsync()
	{
		if (Interlocked.Exchange(ref _imageUploadSharedMemory, null) is { } imageUploadSharedMemory)
		{
			imageUploadSharedMemory.Dispose();
		}
		return ValueTask.CompletedTask;
	}

	private async Task WatchEventsAsync(CancellationToken cancellationToken)
	{
		// The order here is somewhat important.
		// Ideally, we'd be able to guarantee that initial updates for all of those methods would be sent before the next one gets called.
		// In practice, we are only guaranteed that the first write (if any) of each of those methods will happen before the first write of the one called after.

		// The ultimate goal would be to be able to guarantee to the client that updates are provided in a consistent order.
		// However, that is not a straightforward goal to achieve.
		// It is fixable for initialization by reworking the watching logic, however, providing synchronization between different services would be a touch more complex.
		// e.g. We would need to always send a "new device" notification before we send a "new monitor" notification.

		var metadataWatchTask = WatchMetadataChangesAsync(cancellationToken);
		var customMenuWatchTask = WatchCustomMenuChangesAsync(cancellationToken);
		var programmingMetadataInitializationTask = InitializeProgrammingMetadataAsync(cancellationToken);
		var imageWatchTask = WatchImagesAsync(cancellationToken);
		var lightingEffectsWatchTask = WatchLightingEffectsAsync(cancellationToken);

		var deviceWatchTask = WatchDevicesAsync(cancellationToken);
		var powerDeviceWatchTask = WatchPowerDevicesAsync(cancellationToken);
		var mouseDeviceWatchTask = WatchMouseDevicesAsync(cancellationToken);
		var lightingDeviceWatchTask = WatchLightingDevicesAsync(cancellationToken);
		var embeddedMonitorDeviceWatchTask = WatchEmbeddedMonitorDevicesAsync(cancellationToken);
		var monitorDeviceWatchTask = WatchMonitorDevicesAsync(cancellationToken);
		var lightDeviceWatchTask = WatchLightDevicesAsync(cancellationToken);
		var sensorDeviceWatchTask = WatchSensorDevicesAsync(cancellationToken);
		var coolingDeviceWatchTask = WatchCoolingDevicesAsync(cancellationToken);

		var batteryStateWatchTask = WatchBatteryStateChangesAsync(cancellationToken);
		var lowPowerBatteryThresholdWatchTask = WatchLowPowerBatteryThresholdUpdatesAsync(cancellationToken);
		var idleSleepTimerWatchTask = WatchIdleSleepTimerUpdatesAsync(cancellationToken);
		var wirelessBrightnessWatchTask = WatchWirelessBrightnessUpdatesAsync(cancellationToken);

		var mouseDpiWatchTask = WatchMouseDpiAsync(cancellationToken);
		var mouseDpiPresetWatchTask = WatchMouseDpiPresetsAsync(cancellationToken);
		var mousePollingFrequencyWatchTask = WatchMousePollingFrequencyAsync(cancellationToken);

		var lightingDeviceConfigurationWatchTask = WatchLightingDeviceConfigurationAsync(cancellationToken);

		var embeddedMonitorConfigurationWatchTask = WatchEmbeddedMonitorConfigurationChangesAsync(cancellationToken);

		var monitorSettingWatchTask = WatchMonitorSettingsAsync(cancellationToken);

		var lightConfigurationWatchTask = WatchLightConfigurationChangesAsync(cancellationToken);

		var sensorWatchTask = WatchSensorUpdates(_sensorUpdateChannel.Reader, cancellationToken);
		var sensorConfigurationWatchTask = WatchSensorConfigurationUpdatesAsync(cancellationToken);
		var sensorFavoritingTask = ProcessSensorFavoritingAsync(_sensorFavoritingChannel.Reader, cancellationToken);

		var coolingConfigurationWatchTask = WatchCoolingConfigurationChangesAsync(cancellationToken);

		await Task.WhenAll
		(
			metadataWatchTask,
			customMenuWatchTask,
			programmingMetadataInitializationTask,
			imageWatchTask,
			lightingEffectsWatchTask,
			deviceWatchTask,
			powerDeviceWatchTask,
			mouseDeviceWatchTask,
			batteryStateWatchTask,
			lowPowerBatteryThresholdWatchTask,
			idleSleepTimerWatchTask,
			wirelessBrightnessWatchTask,
			mouseDpiWatchTask,
			mouseDpiPresetWatchTask,
			mousePollingFrequencyWatchTask,
			lightingDeviceWatchTask,
			lightingDeviceConfigurationWatchTask,
			embeddedMonitorDeviceWatchTask,
			embeddedMonitorConfigurationWatchTask,
			monitorDeviceWatchTask,
			monitorSettingWatchTask,
			sensorDeviceWatchTask,
			sensorWatchTask,
			sensorConfigurationWatchTask,
			sensorFavoritingTask,
			lightDeviceWatchTask,
			lightConfigurationWatchTask,
			coolingDeviceWatchTask,
			coolingConfigurationWatchTask
		).ConfigureAwait(false);
	}

	protected override async Task ReadAndProcessMessagesAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		try
		{
			// This should act as the handshake.
			await SendGitCommitIdAsync(cancellationToken).ConfigureAwait(false);
			using (var watchCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
			{
				Task? watchTask = null;
				{
					int count = await stream.ReadAsync(buffer, watchCancellationTokenSource.Token).ConfigureAwait(false);
					if (count == 0) return;
					if (!await ProcessMessageAsync(buffer.Span[..count], watchCancellationTokenSource.Token).ConfigureAwait(false)) return;
					if (_state > 0) watchTask = WatchEventsAsync(watchCancellationTokenSource.Token);
				}

				try
				{
					while (true)
					{
						int count = await stream.ReadAsync(buffer, watchCancellationTokenSource.Token).ConfigureAwait(false);
						if (count == 0) return;
						// Ignore all messages if the state is negative (it means that something wrong happened, likely that the SHA1 don't match)
						if (_state < 0) continue;
						// If the message processing does not indicate success, we can close the connection.
						if (!await ProcessMessageAsync(buffer.Span[..count], watchCancellationTokenSource.Token).ConfigureAwait(false)) return;
					}
				}
				finally
				{
					watchCancellationTokenSource.Cancel();
					if (watchTask is not null) await watchTask.ConfigureAwait(false);
				}
			}
		}
		finally
		{
			foreach (var sensorWatchState in _sensorWatchStates.Values)
			{
				await sensorWatchState.DisposeAsync();
			}
		}
	}

	private Task SendGitCommitIdAsync(CancellationToken cancellationToken)
		=> Program.GitCommitId.IsDefault ?
			SendGitCommitIdAsync(ImmutableCollectionsMarshal.AsImmutableArray(new byte[20]), cancellationToken) :
			SendGitCommitIdAsync(Program.GitCommitId, cancellationToken);

	private async Task SendGitCommitIdAsync(ImmutableArray<byte> version, CancellationToken cancellationToken)
	{
		if (version.IsDefault || version.Length != 20) throw new ArgumentException();

		using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = WriteBuffer[0..21];
			FillBuffer(buffer.Span, version);
			await WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
		}

		static void FillBuffer(Span<byte> buffer, ImmutableArray<byte> version)
		{
			buffer[0] = (byte)ExoUiProtocolServerMessage.GitVersion;
			ImmutableCollectionsMarshal.AsArray(version)!.CopyTo(buffer[1..]);
		}
	}

	private ValueTask<bool> ProcessMessageAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
		=> ProcessMessageAsync((ExoUiProtocolClientMessage)data[0], data[1..], cancellationToken);

	private ValueTask<bool> ProcessMessageAsync(ExoUiProtocolClientMessage message, ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		if (_state == 0 && message != ExoUiProtocolClientMessage.GitVersion) goto Failure;
		switch (message)
		{
		case ExoUiProtocolClientMessage.NoOp:
			goto Success;
		case ExoUiProtocolClientMessage.GitVersion:
			if (data.Length != 20) goto Failure;
			_state = Program.GitCommitId.IsDefaultOrEmpty || !data.SequenceEqual(ImmutableCollectionsMarshal.AsArray(Program.GitCommitId)!) ? -1 : 1;
			goto Success;
		case ExoUiProtocolClientMessage.InvokeMenuCommand:
			if (data.Length != 16) goto Failure;
			ProcessMenuItemInvocation(Unsafe.ReadUnaligned<Guid>(in data[0]));
			goto Success;
		case ExoUiProtocolClientMessage.CustomMenuUpdate:
			ProcessCustomMenuUpdate(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.ImageAddBegin:
			return ProcessImageAddBeginAsync(data, cancellationToken);
		case ExoUiProtocolClientMessage.ImageAddCancel:
			return ProcessImageAddCancelAsync(data, cancellationToken);
		case ExoUiProtocolClientMessage.ImageAddEnd:
			return ProcessImageAddEndAsync(data, cancellationToken);
		case ExoUiProtocolClientMessage.ImageRemove:
			return ProcessImageRemoveAsync(data, cancellationToken);
		case ExoUiProtocolClientMessage.UpdateSettings:
			goto Success;
		case ExoUiProtocolClientMessage.LowPowerBatteryThreshold:
			ProcessLowPowerBatteryThreshold(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.IdleSleepTimer:
			ProcessIdleSleepTimer(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.WirelessBrightness:
			ProcessWirelessBrightness(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.MouseActiveDpiPreset:
			ProcessMouseActiveDpiPreset(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.MouseDpiPresets:
			ProcessMouseDpiPresets(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.MousePollingFrequency:
			ProcessMousePollingFrequency(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.LightingDeviceConfiguration:
			ProcessLightingDeviceConfiguration(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.EmbeddedMonitorBuiltInGraphics:
			ProcessEmbeddedMonitorBuiltInGraphics(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.EmbeddedMonitorImage:
			ProcessEmbeddedMonitorImage(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.LightSwitch:
			ProcessLightSwitch(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.LightBrightness:
			ProcessLightBrightness(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.LightTemperature:
			ProcessLightTemperature(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.MonitorSettingSet:
			ProcessMonitorSettingSet(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.MonitorSettingRefresh:
			ProcessMonitorSettingRefresh(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.SensorStart:
			return ProcessSensorRequestAsync(data, cancellationToken);
		case ExoUiProtocolClientMessage.SensorFavorite:
			ProcessSensorFavoriteRequest(data);
			goto Success;
		case ExoUiProtocolClientMessage.CoolerSetAutomatic:
			ProcessCoolerSetAutomatic(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.CoolerSetFixed:
			ProcessCoolerSetFixed(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.CoolerSetSoftwareCurve:
			ProcessCoolerSetSoftwareCurve(data, cancellationToken);
			goto Success;
		case ExoUiProtocolClientMessage.CoolerSetHardwareCurve:
			ProcessCoolerSetHardwareCurve(data, cancellationToken);
			goto Success;
		}
	Failure:;
		return new(false);
	Success:;
		return new(true);
	}

	private async void WriteSimpleOperationStatus(ExoUiProtocolServerMessage message, uint requestId, byte status, CancellationToken cancellationToken)
	{
		try
		{
			await WriteSimpleOperationStatusAsync(message, requestId, status, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
		}
	}

	private async ValueTask WriteSimpleOperationStatusAsync(ExoUiProtocolServerMessage message, uint requestId, byte status, CancellationToken cancellationToken)
	{
		using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			await UnsafeWriteSimpleOperationStatusAsync(message, requestId, status, cancellationToken).ConfigureAwait(false);
		}
	}

	private async ValueTask UnsafeWriteSimpleOperationStatusAsync(ExoUiProtocolServerMessage message, uint requestId, byte status, CancellationToken cancellationToken)
	{
		var buffer = WriteBuffer;
		nuint length = Write(buffer.Span, message, requestId, status);
		await WriteAsync(buffer[..(int)length], cancellationToken).ConfigureAwait(false);

		static nuint Write(Span<byte> buffer, ExoUiProtocolServerMessage message, uint requestId, byte status)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)message);
			writer.WriteVariable(requestId);
			writer.Write(status);
			return writer.Length;
		}
	}
}
