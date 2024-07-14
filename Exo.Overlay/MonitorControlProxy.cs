using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Threading.Channels;
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
	private readonly object _lock;
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
				var sessionId = Guid.NewGuid();
				try
				{
					await Task.WhenAll
					(
						ProcessAdapterRequestsAsync(sessionId, monitorProxyService, cancellationToken),
						ProcessMonitorRequestsAsync(sessionId, monitorProxyService, cancellationToken),
						ProcessVcpGetRequestsAsync(sessionId, monitorProxyService, cancellationToken),
						ProcessVcpSetRequestsAsync(sessionId, monitorProxyService, cancellationToken),
						ProcessMonitorReleaseRequestsAsync(sessionId, monitorProxyService, cancellationToken)
					).ConfigureAwait(false);
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

	private async Task ProcessAdapterRequestsAsync(Guid sessionId, IMonitorControlProxyService monitorProxyService, CancellationToken cancellationToken)
	{
		try
		{
			var channel = Channel.CreateBounded<AdapterResponse>(BoundedChannelOptions);
			try
			{
				await foreach (var request in monitorProxyService.ProcessAdapterRequestsAsync(sessionId, channel.Reader.ReadAllAsync(cancellationToken), cancellationToken).ConfigureAwait(false))
				{
					ulong adapterId;
					try
					{
						var displayConfiguration = DisplayConfiguration.GetForActivePaths();
						foreach (var path in displayConfiguration.Paths)
						{
							if (path.SourceInfo.Adapter.GetDeviceName() == request.DeviceName)
							{
								adapterId = (ulong)path.SourceInfo.Adapter.Id;
								goto AdapterFound;
							}
						}
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.Error, AdapterId = 0 });
						continue;
					}
					channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.NotFound, AdapterId = 0 });
					continue;
				AdapterFound:;
					channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.Success, AdapterId = adapterId });
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

	private async Task ProcessMonitorRequestsAsync(Guid sessionId, IMonitorControlProxyService monitorProxyService, CancellationToken cancellationToken)
	{
		try
		{
			var channel = Channel.CreateBounded<MonitorResponse>(BoundedChannelOptions);
			try
			{
				await foreach (var request in monitorProxyService.ProcessMonitorRequestsAsync(sessionId, channel.Reader.ReadAllAsync(cancellationToken), cancellationToken).ConfigureAwait(false))
				{
					uint monitorHandle;
					PhysicalMonitor physicalMonitor;
					try
					{
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
								byte[]? cachedRawEdid;
								using (var deviceKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\{targetNameInformation.GetMonitorDeviceName()}\Device Parameters"))
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
									goto MonitorFound;
								}
								// If the physical monitor is not a match dispose it.
								currentPhysicalMonitor.Dispose();
							}
						}
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.Error, MonitorHandle = 0 });
						continue;
					}
				DisplayConfigurationMismatch:;
					channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.NotFound, MonitorHandle = 0 });
					continue;
				MonitorFound:;
					channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.Success, MonitorHandle = monitorHandle });
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

	private async Task ProcessCapabilitiesRequestsAsync(Guid sessionId, IMonitorControlProxyService monitorProxyService, CancellationToken cancellationToken)
	{
		try
		{
			var channel = Channel.CreateBounded<MonitorCapabilitiesResponse>(BoundedChannelOptions);
			try
			{
				await foreach (var request in monitorProxyService.ProcessCapabilitiesRequestsAsync(sessionId, channel.Reader.ReadAllAsync(cancellationToken), cancellationToken).ConfigureAwait(false))
				{
					ImmutableArray<byte> utf8Capabilities;
					try
					{
						lock (_lock)
						{
							if (_physicalMonitors.TryGetValue(request.MonitorHandle, out var physicalMonitor))
							{
								utf8Capabilities = physicalMonitor.GetCapabilitiesUtf8String().Span.ToImmutableArray();
								goto Success;
							}
						}
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.Error, Utf8Capabilities = [] });
						continue;
					}
					channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.NotFound, Utf8Capabilities = [] });
					continue;
				Success:;
					channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.Success, Utf8Capabilities = utf8Capabilities });
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

	private async Task ProcessVcpGetRequestsAsync(Guid sessionId, IMonitorControlProxyService monitorProxyService, CancellationToken cancellationToken)
	{
		try
		{
			var channel = Channel.CreateBounded<MonitorVcpGetResponse>(BoundedChannelOptions);
			try
			{
				await foreach (var request in monitorProxyService.ProcessVcpGetRequestsAsync(sessionId, channel.Reader.ReadAllAsync(cancellationToken), cancellationToken).ConfigureAwait(false))
				{
					VcpFeatureReply reply;
					try
					{
						lock (_lock)
						{
							if (_physicalMonitors.TryGetValue(request.MonitorHandle, out var physicalMonitor))
							{
								reply = physicalMonitor.GetVcpFeature(request.VcpCode);
								goto Success;
							}
						}
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.Error });
						continue;
					}
					channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.NotFound });
					continue;
				Success:;
					channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.Success, CurrentValue = reply.CurrentValue, MaximumValue = reply.MaximumValue, IsTemporary = reply.IsMomentary });
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

	private async Task ProcessVcpSetRequestsAsync(Guid sessionId, IMonitorControlProxyService monitorProxyService, CancellationToken cancellationToken)
	{
		try
		{
			var channel = Channel.CreateBounded<MonitorVcpSetResponse>(BoundedChannelOptions);
			try
			{
				await foreach (var request in monitorProxyService.ProcessVcpSetRequestsAsync(sessionId, channel.Reader.ReadAllAsync(cancellationToken), cancellationToken).ConfigureAwait(false))
				{
					try
					{
						lock (_lock)
						{
							if (_physicalMonitors.TryGetValue(request.MonitorHandle, out var physicalMonitor))
							{
								physicalMonitor.SetVcpFeature(request.VcpCode, request.Value);
								goto Success;
							}
						}
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.Error });
						continue;
					}
					channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.NotFound });
					continue;
				Success:;
					channel.Writer.TryWrite(new() { RequestId = request.RequestId, Status = ResponseStatus.Success });
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

	private async Task ProcessMonitorReleaseRequestsAsync(Guid sessionId, IMonitorControlProxyService monitorProxyService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var request in monitorProxyService.EnumerateMonitorsToReleaseAsync(sessionId, cancellationToken).ConfigureAwait(false))
			{
				try
				{
					lock (_lock)
					{
						if (_physicalMonitors.Remove(request.MonitorHandle, out var physicalMonitor))
						{
							physicalMonitor.Dispose();
						}
					}
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}
}
