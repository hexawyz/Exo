using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Ipc;
using Exo.Settings.Ui.Ipc;

namespace Exo.Service.Ipc;

internal sealed partial class UiPipeServerConnection : PipeServerConnection, IPipeServerConnection<UiPipeServerConnection>
{
	private static readonly UnboundedChannelOptions SensorChannelOptions = new() { SingleWriter = false, SingleReader = true, AllowSynchronousContinuations = true };

	public static UiPipeServerConnection Create(PipeServer<UiPipeServerConnection> server, NamedPipeServerStream stream)
	{
		var uiPipeServer = (UiPipeServer)server;
		return new
		(
			server,
			stream,
			uiPipeServer.ConnectionLogger,
			uiPipeServer.AssemblyLoader,
			uiPipeServer.CustomMenuService,
			uiPipeServer.DeviceRegistry,
			uiPipeServer.PowerService,
			uiPipeServer.MonitorService,
			uiPipeServer.SensorService,
			uiPipeServer.LightingEffectMetadataService,
			uiPipeServer.LightingService
		);
	}

	private readonly IAssemblyLoader _assemblyLoader;
	private readonly CustomMenuService _customMenuService;
	private readonly DeviceRegistry _deviceRegistry;
	private readonly PowerService _powerService;
	private readonly MonitorService _monitorService;
	private readonly SensorService _sensorService;
	private readonly LightingEffectMetadataService _lightingEffectMetadataService;
	private readonly LightingService _lightingService;
	private int _state;
	private readonly Dictionary<uint, SensorWatchState> _sensorWatchStates;
	private readonly Channel<SensorUpdate> _sensorUpdateChannel;
	private readonly Channel<SensorFavoritingRequest> _sensorFavoritingChannel;
	private readonly ILogger<UiPipeServerConnection> _logger;

	private UiPipeServerConnection
	(
		PipeServer server,
		NamedPipeServerStream stream,
		ILogger<UiPipeServerConnection> logger,
		IAssemblyLoader assemblyLoader,
		CustomMenuService customMenuService,
		DeviceRegistry deviceRegistry,
		PowerService powerService,
		MonitorService monitorService,
		SensorService sensorService,
		LightingEffectMetadataService lightingEffectMetadataService,
		LightingService lightingService
	) : base(server, stream)
	{
		_logger = logger;
		_assemblyLoader = assemblyLoader;
		_customMenuService = customMenuService;
		_deviceRegistry = deviceRegistry;
		_powerService = powerService;
		_monitorService = monitorService;
		_sensorService = sensorService;
		_lightingEffectMetadataService = lightingEffectMetadataService;
		_lightingService = lightingService;
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

	protected override ValueTask OnDisposedAsync() => ValueTask.CompletedTask;

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
		var lightingEffectsWatchTask = WatchLightingEffectsAsync(cancellationToken);

		var deviceWatchTask = WatchDevicesAsync(cancellationToken);
		var powerDeviceWatchTask = WatchPowerDevicesAsync(cancellationToken);
		var lightingDeviceWatchTask = WatchLightingDevicesAsync(cancellationToken);
		var lightingDeviceConfigurationWatchTask = WatchLightingDeviceConfigurationAsync(cancellationToken);
		var monitorDeviceWatchTask = WatchMonitorDevicesAsync(cancellationToken);
		var sensorDeviceWatchTask = WatchSensorDevicesAsync(cancellationToken);

		var monitorSettingWatchTask = WatchMonitorSettingsAsync(cancellationToken);

		var sensorWatchTask = WatchSensorUpdates(_sensorUpdateChannel.Reader, cancellationToken);
		var sensorConfigurationWatchTask = WatchSensorConfigurationUpdatesAsync(cancellationToken);
		var sensorFavoritingTask = ProcessSensorFavoritingAsync(_sensorFavoritingChannel.Reader, cancellationToken);

		await Task.WhenAll
		(
			metadataWatchTask,
			customMenuWatchTask,
			lightingEffectsWatchTask,
			deviceWatchTask,
			powerDeviceWatchTask,
			lightingDeviceWatchTask,
			lightingDeviceConfigurationWatchTask,
			monitorDeviceWatchTask,
			monitorSettingWatchTask,
			sensorDeviceWatchTask,
			sensorWatchTask,
			sensorConfigurationWatchTask,
			sensorFavoritingTask
		).ConfigureAwait(false);
	}

	protected override async Task ReadAndProcessMessagesAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		// This should act as the handshake.
		try
		{
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
				catch (Exception ex)
				{
					// TODO: Log
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
		case ExoUiProtocolClientMessage.UpdateSettings:
			goto Success;
		case ExoUiProtocolClientMessage.LightingDeviceConfiguration:
			ProcessLightingDeviceConfiguration(data, cancellationToken);
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
		}
	Failure:;
		return new(false);
	Success:;
		return new(true);
	}
}
