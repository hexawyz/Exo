using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Configuration;
using DeviceTools.DisplayDevices.Mccs;
using Exo.Contracts.Ui.Overlay;
using Exo.Ui;
using Microsoft.Win32;

namespace Exo.Overlay;

/// <summary>Implements the monitor control proxy that is used to execute monitor control requests from the service when needed.</summary>
/// <remarks>
/// <para>
/// This connects to the service, and starts multiple asynchronous operations to process monitor requests that can't be executed on the service.
/// This component would ideally not be needed, however it is a necessary evil to have monitor controls on computers where we have no suitable GPU driver that can be used from within the service.
/// </para>
/// <para>
/// As the display infrastructure is somewhat virtualized by Windows, the APIs can only be accessed from within an interactive application.
/// We assume that this helper will be run from within the proper user session, having access to all necessary monitors.
/// This can't be 100% guaranteed, but we do have more than decent chances of this being the case in modern day Windows installations. (They generally run only a single user session at a time)
/// </para>
/// </remarks>
internal sealed class MonitorControlProxy : IAsyncDisposable
{
	private static readonly BoundedChannelOptions BoundedChannelOptions = new(10)
	{
		AllowSynchronousContinuations = true,
		FullMode = BoundedChannelFullMode.Wait,
		SingleReader = true,
		SingleWriter = true,
	};

	private readonly ServiceConnectionManager _serviceConnectionManager;
	private readonly Dictionary<uint, PhysicalMonitor> _physicalMonitors;
	private readonly Lock _lock;
	private uint _lastMonitorHandle;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _runTask;

	public MonitorControlProxy(ServiceConnectionManager serviceConnectionManager)
	{
		_serviceConnectionManager = serviceConnectionManager;
		_physicalMonitors = new();
		_lock = new();
		_cancellationTokenSource = new();
		_runTask = RunAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;

		cts.Cancel();
		await _runTask.ConfigureAwait(false);
		cts.Dispose();
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var monitorProxyService = await _serviceConnectionManager.CreateServiceAsync<IMonitorControlProxyService>(cancellationToken).ConfigureAwait(false);
				try
				{
					await ProcessRequestsAsync(monitorProxyService, cancellationToken);
				}
				catch
				{
				}
				finally
				{
					lock (_lock)
					{
						foreach (var physicalMonitor in _physicalMonitors.Values)
						{
							physicalMonitor.Dispose();
						}
						_physicalMonitors.Clear();
					}
					_lastMonitorHandle = new();
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			// TODO: Log
		}
	}

	private async Task ProcessRequestsAsync(IMonitorControlProxyService monitorProxyService, CancellationToken cancellationToken)
	{
		try
		{
			var channel = Channel.CreateBounded<MonitorControlProxyResponse>(BoundedChannelOptions);
			try
			{
				await foreach (var request in monitorProxyService.ProcessRequestsAsync(channel.Reader.ReadAllAsync(cancellationToken), cancellationToken).ConfigureAwait(false))
				{
					MonitorControlProxyResponse response;
					try
					{
						response = request.Content.RequestType switch
						{
							MonitorControlProxyRequestResponseOneOfCase.Adapter => ProcessAdapterRequest(request.RequestId, request.Content.AdapterRequest!),
							MonitorControlProxyRequestResponseOneOfCase.Monitor => ProcessMonitorRequest(request.RequestId, request.Content.MonitorRequest!),
							MonitorControlProxyRequestResponseOneOfCase.MonitorRelease => ProcessMonitorReleaseRequest(request.RequestId, request.Content.MonitorReleaseRequest!),
							MonitorControlProxyRequestResponseOneOfCase.MonitorCapabilities => ProcessCapabilitiesRequest(request.RequestId, request.Content.MonitorCapabilitiesRequest!),
							MonitorControlProxyRequestResponseOneOfCase.MonitorVcpGet => ProcessVcpGetRequest(request.RequestId, request.Content.MonitorVcpGetRequest!),
							MonitorControlProxyRequestResponseOneOfCase.MonitorVcpSet => ProcessVcpSetRequest(request.RequestId, request.Content.MonitorVcpSetRequest!),
							_ => throw new InvalidOperationException("Unsupported request.")
						};
					}
					catch (VcpCodeNotSupportedException)
					{
						response = new() { RequestId = request.RequestId, Status = MonitorControlResponseStatus.InvalidVcpCode };
					}
					catch
					{
						response = new() { RequestId = request.RequestId, Status = MonitorControlResponseStatus.Error };
					}
					channel.Writer.TryWrite(response);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				channel.Writer.TryComplete();
			}
			catch (Exception ex)
			{
				channel.Writer.TryComplete(ex);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private MonitorControlProxyResponse ProcessAdapterRequest(uint requestId, AdapterRequest request)
	{
		ulong adapterId;
		var displayConfiguration = DisplayConfiguration.GetForActivePaths();
		foreach (var path in displayConfiguration.Paths)
		{
			var adapterDeviceInterfaceName = path.SourceInfo.Adapter.GetDeviceName();
			string adapterDeviceName = Device.GetDeviceInstanceId(adapterDeviceInterfaceName);
			if (adapterDeviceName == request.DeviceName)
			{
				adapterId = (ulong)path.SourceInfo.Adapter.Id;
				return new() { RequestId = requestId, Status = MonitorControlResponseStatus.Success, Content = new AdapterResponse { AdapterId = adapterId } };
			}
		}
		return new() { RequestId = requestId, Status = MonitorControlResponseStatus.NotFound };
	}

	private MonitorControlProxyResponse ProcessMonitorRequest(uint requestId, MonitorRequest request)
	{
		uint monitorHandle;
		PhysicalMonitor physicalMonitor;
		var displayConfiguration = DisplayConfiguration.GetForActivePaths();

		// First, build a more workable structure of the sources and targets from the display configuration.
		var targetsBySource = new List<(DisplayConfigurationPathSourceInfo Source, List<DisplayConfigurationPathTargetInfo> Targets)>();
		foreach (var path in DisplayConfiguration.GetForActivePaths().Paths)
		{
			if (targetsBySource.Count == 0 || path.SourceInfo != CollectionsMarshal.AsSpan(targetsBySource)[^1].Source)
			{
				targetsBySource.Add((path.SourceInfo, new() { path.TargetInfo }));
			}
			else
			{
				CollectionsMarshal.AsSpan(targetsBySource)[^1].Targets.Add(path.TargetInfo);
			}
		}

		var logicalMonitors = LogicalMonitor.GetAll();

		if (logicalMonitors.Length != targetsBySource.Count)
		{
			goto DisplayConfigurationMismatch;
		}

		for (int i = 0; i < logicalMonitors.Length; i++)
		{
			var logicalMonitor = logicalMonitors[i];
			if (targetsBySource[i].Source.GetDeviceName() != logicalMonitor.GetMonitorInformation().DeviceName)
			{
				goto DisplayConfigurationMismatch;
			}

			var targets = targetsBySource[i].Targets;
			var physicalMonitors = logicalMonitor.GetPhysicalMonitors();
			if (physicalMonitors.Length != targets.Count)
			{
				goto DisplayConfigurationMismatch;
			}

			for (int j = 0; j < physicalMonitors.Length; j++)
			{
				var currentPhysicalMonitor = physicalMonitors[j];
				var target = targets[j];

				var targetNameInformation = target.GetDeviceNameInformation();
				if (!targetNameInformation.IsEdidValid) continue;
				if (targetNameInformation.EdidVendorId.Value != request.EdidVendorId || targetNameInformation.EdidProductId != request.EdidProductId) continue;
				string monitorDeviceInterfaceName = targetNameInformation.GetMonitorDeviceName();
				string monitorDeviceName = Device.GetDeviceInstanceId(monitorDeviceInterfaceName);
				byte[]? cachedRawEdid;
				using (var deviceKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\{monitorDeviceName}\Device Parameters"))
				{
					if (deviceKey is null) continue;
					cachedRawEdid = deviceKey.GetValue("EDID") as byte[];
				}
				var edid = Edid.Parse(cachedRawEdid);
				if (edid.IdSerialNumber == request.IdSerialNumber && edid.SerialNumber == request.SerialNumber)
				{
					monitorHandle = Interlocked.Increment(ref _lastMonitorHandle);
					physicalMonitor = currentPhysicalMonitor;
					lock (_lock)
					{
						_physicalMonitors.Add(monitorHandle, physicalMonitor);
					}
					// Play nice and dispose all physical monitors after the successful one. (Worst case, they would get finalized)
					for (int k = j + 1; k < physicalMonitors.Length; k++)
					{
						physicalMonitors[k].Dispose();
					}
					return new() { RequestId = requestId, Status = MonitorControlResponseStatus.Success, Content = new MonitorResponse { MonitorHandle = monitorHandle } };
				}
				// If the physical monitor is not a match dispose it.
				currentPhysicalMonitor.Dispose();
			}
		}
	DisplayConfigurationMismatch:;
		return new() { RequestId = requestId, Status = MonitorControlResponseStatus.NotFound };
	}

	private MonitorControlProxyResponse ProcessCapabilitiesRequest(uint requestId, MonitorCapabilitiesRequest request)
	{
		ImmutableArray<byte> utf8Capabilities;
		lock (_lock)
		{
			if (_physicalMonitors.TryGetValue(request.MonitorHandle, out var physicalMonitor))
			{
				utf8Capabilities = physicalMonitor.GetCapabilitiesUtf8String().Span.ToImmutableArray();
				return new() { RequestId = requestId, Status = MonitorControlResponseStatus.Success, Content = new MonitorCapabilitiesResponse { Utf8Capabilities = utf8Capabilities } };
			}
		}
		return new() { RequestId = requestId, Status = MonitorControlResponseStatus.NotFound };
	}

	private MonitorControlProxyResponse ProcessVcpGetRequest(uint requestId, MonitorVcpGetRequest request)
	{
		VcpFeatureReply reply;
		lock (_lock)
		{
			if (_physicalMonitors.TryGetValue(request.MonitorHandle, out var physicalMonitor))
			{
				reply = physicalMonitor.GetVcpFeature(request.VcpCode);
				return new() { RequestId = requestId, Status = MonitorControlResponseStatus.Success, Content = new MonitorVcpGetResponse { CurrentValue = reply.CurrentValue, MaximumValue = reply.MaximumValue, IsMomentary = reply.IsMomentary } };
			}
		}
		return new() { RequestId = requestId, Status = MonitorControlResponseStatus.NotFound };
	}

	private MonitorControlProxyResponse ProcessVcpSetRequest(uint requestId, MonitorVcpSetRequest request)
	{
		lock (_lock)
		{
			if (_physicalMonitors.TryGetValue(request.MonitorHandle, out var physicalMonitor))
			{
				physicalMonitor.SetVcpFeature(request.VcpCode, request.Value);
				return new() { RequestId = requestId, Status = MonitorControlResponseStatus.Success, Content = new MonitorVcpSetResponse() };
			}
		}
		return new() { RequestId = requestId, Status = MonitorControlResponseStatus.NotFound };
	}

	private MonitorControlProxyResponse ProcessMonitorReleaseRequest(uint requestId, MonitorReleaseRequest request)
	{
		lock (_lock)
		{
			if (_physicalMonitors.Remove(request.MonitorHandle, out var physicalMonitor))
			{
				physicalMonitor.Dispose();
				return new() { RequestId = requestId, Status = MonitorControlResponseStatus.Success, Content = new MonitorReleaseResponse() };
			}
		}
		return new() { RequestId = requestId, Status = MonitorControlResponseStatus.NotFound };
	}
}
