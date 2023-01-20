using System.Buffers;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using DeviceTools.HumanInterfaceDevices;
using Exo.Devices.Logitech.HidPlusPlus;
using Exo.Devices.Logitech.HidPlusPlus.Features;

namespace Exo.Devices.Logitech;

public sealed class HidPlusPlusDevice : IDisposable, IAsyncDisposable
{
	private enum ReadTaskResult
	{
		EndOfStream = 0,
		TaskCanceled = 1,
		DeviceDisconnected = 2,
	}

	private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Create(20, 100);

	/// <summary>Creates and initializes the engine from a HID device stream.</summary>
	/// <param name="stream">The stream to use for communciation with the device.</param>
	/// <param name="softwareId">The software ID to use for tagging HID messages.</param>
	/// <param name="isSingleDevice">Indicates whether the device is a single device instead of a multiplexer such as an USB receiver.</param>
	/// <returns>An instance of <see cref="HidPlusPlusDevice"/> that can be used to access HID++ features of the device.</returns>
	public static async Task<HidPlusPlusDevice> CreateAsync(HidFullDuplexStream stream, HidPlusPlusProtocolFlavor protocolFlavor, byte defaultDeviceIndex, byte softwareId, TimeSpan requestTimeout)
	{
		try
		{
			// Creating the device should pose little to no problem, but we will do additional checks once the instance is created.
			var device = new HidPlusPlusDevice(stream, protocolFlavor, defaultDeviceIndex, softwareId, requestTimeout);
			try
			{
				// Protocol version check.
				// TODO: Make this check a bit better and explicitly list the supported versions.
				HidPlusPlusVersion protocolVersion;
				try
				{
					protocolVersion = await device.GetProtocolVersionAsync(255, default);
				}
				catch (HidPlusPlusException ex) when (ex.ErrorCode == 1)
				{
					throw new InvalidOperationException("Wrong protocol version.");
				}

				if (protocolVersion.Major is < 2 or > 4)
				{
					throw new Exception("Unsupported protocol version. Only HID++ 2.0 an later versions are supported.");
				}
				else if (protocolFlavor is not HidPlusPlusProtocolFlavor.Auto and not HidPlusPlusProtocolFlavor.HidPlusPlus2)
				{
					throw new Exception("Protocol flavor does not match.");
				}
			}
			catch
			{
				await device.DisposeAsync().ConfigureAwait(false);
				throw;
			}
			return device;
		}
		catch
		{
			await stream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	public const int ShortReportId = 0x10;
	public const int LongReportId = 0x11;
	public const int VeryLongReportId = 0x12;
	public const int ExtraLongReportId = 0x13;

	private readonly HidFullDuplexStream _stream;
	private readonly ChannelReader<HidPlusPlusLongMessage<HidPlusPlusRawParameters>> _broadcastMessageReader;
	// Tuple of Buffer; Identifying Byte count; TaskCompletionSource
	// In most cases, bytes of the request are supposed to be repeated in the response.
	// This is always the case for the header part (4 bytes), but not always stricly the case for the parameters.
	// This is notably not the case for the Ping/GetProtocolVersion message.
	private Tuple<byte[], int, TaskCompletionSource<HidPlusPlusLongMessage<HidPlusPlusRawParameters>>>? _currentSendOperation;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _readTask;
	private ReadOnlyDictionary<HidPlusPlusFeature, byte>? _cachedFeatures;

	private readonly TimeSpan _requestTimeout;

	private readonly HidPlusPlusProtocolFlavor _protocolFlavor;
	private readonly byte _defaultDeviceIndex;
	private readonly byte _softwareId;

	private HidPlusPlusDevice(HidFullDuplexStream stream, HidPlusPlusProtocolFlavor protocolFlavor, byte defaultDeviceIndex, byte softwareId, TimeSpan requestTimeout)
	{
		_stream = stream;
		_defaultDeviceIndex = defaultDeviceIndex;
		_softwareId = softwareId;
		_protocolFlavor = protocolFlavor;
		_requestTimeout = requestTimeout;
		_cancellationTokenSource = new CancellationTokenSource();
		var broadcastMessageChannel = Channel.CreateUnbounded<HidPlusPlusLongMessage<HidPlusPlusRawParameters>>();
		_readTask = ReadAsync(broadcastMessageChannel.Writer, _cancellationTokenSource.Token);
	}

	public void Dispose()
	{
		_cancellationTokenSource.Cancel();
		_stream.Dispose();
		try
		{
			_readTask.Wait();
		}
		catch { }
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _stream.DisposeAsync().ConfigureAwait(false);
		try
		{
			await _readTask.ConfigureAwait(false);
		}
		catch { }
	}

	private async Task<ReadTaskResult> ReadAsync(ChannelWriter<HidPlusPlusLongMessage<HidPlusPlusRawParameters>> broadcastMessageWriter, CancellationToken cancellationToken)
	{
		const uint ErrorDeviceNotConnected = 0x8007048F;
		try
		{
			while (true)
			{
				var buffer = BufferPool.Rent(20);
				int count = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

				if (count == 0)
				{
					broadcastMessageWriter.TryComplete();
					return ReadTaskResult.EndOfStream;
				}

				var message = Unsafe.As<byte, HidPlusPlusLongMessage<HidPlusPlusRawParameters>>(ref buffer[0]);

				// Handle errors first.
				// The documentation is not clear on how errors should be represented, but it seems that HID++ 2.0 will always use 0xFF as an error code, while HID++ 1.0 wil use 0x8F.
				// For now, HID++ 1.0 is not supported, but it is necessary to at least be able to validate error codes to detect a wrong HID++ version.
				// (As it seems that HID++ 1.0 uses a different usage page than HID++ 2.0, we should already be able to distinguish between them in that way)
				if (message.Header.ReportId == LongReportId && message.Header.FeatureIndex == 0xFF ||
					message.Header.ReportId == ShortReportId && message.Header.FeatureIndex == 0x8F)
				{
					var softwareId = message.Parameters.Byte0 & 0xF;

					if (softwareId == _softwareId)
					{
						var pending = Volatile.Read(ref _currentSendOperation);

						if (pending is not null && message.Header.FunctionAndSoftwareId == pending.Item1[2])
						{
							pending.Item3.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new HidPlusPlusException(message.Parameters.Byte1)));
						}
					}
				}
				else
				{
					var softwareId = message.Header.SoftwareId;
					if (softwareId == 0)
					{
						await broadcastMessageWriter.WriteAsync(message, cancellationToken).ConfigureAwait(false);
					}
					else if (softwareId == _softwareId)
					{
						var pending = Volatile.Read(ref _currentSendOperation);

						if (pending is not null && buffer.AsSpan(0, pending.Item2).SequenceEqual(pending.Item1.AsSpan(0, pending.Item2)))
						{
							// Clear the pending item preemptively.
							Interlocked.CompareExchange(ref _currentSendOperation, null, pending);

							pending.Item3.TrySetResult(message);
						}
					}
				}
			}
		}
		catch (IOException ex) when ((uint)ex.HResult == ErrorDeviceNotConnected)
		{
			broadcastMessageWriter.TryComplete(ex);

			return ReadTaskResult.DeviceDisconnected;
		}
		catch (OperationCanceledException)
		{
			broadcastMessageWriter.TryComplete();

			return ReadTaskResult.TaskCanceled;
		}
		catch (Exception ex)
		{
			broadcastMessageWriter.TryComplete(ex);
			throw;
		}
	}

	public Task<TOutputParameters> SendAsync<TOutputParameters>(byte deviceId, byte featureIndex, byte functionId, CancellationToken cancellationToken)
		where TOutputParameters : struct, IMessageResponseParameters
	{
		var message = new HidPlusPlusLongMessage<HidPlusPlusRawParameters>
		{
			Header =
			{
				ReportId = LongReportId,
				DeviceId = deviceId,
				FeatureIndex = featureIndex,
				FunctionAndSoftwareId = (byte)(functionId << 4 | _softwareId),
			},
		};

		return SendAsync<TOutputParameters>(message, cancellationToken);
	}

	public Task<TOutputParameters> SendAsync<TInputParameters, TOutputParameters>(byte deviceId, byte featureIndex, byte functionId, TInputParameters parameters, CancellationToken cancellationToken)
		where TInputParameters : struct, IMessageRequestParameters
		where TOutputParameters : struct, IMessageResponseParameters
	{
		var message = new HidPlusPlusLongMessage<TInputParameters>
		{
			Header =
			{
				ReportId = LongReportId,
				DeviceId = deviceId,
				FeatureIndex = featureIndex,
				FunctionAndSoftwareId = (byte)(functionId << 4 | _softwareId),
			},
			Parameters = parameters
		};

		return SendAsync<TOutputParameters>(Unsafe.As<HidPlusPlusLongMessage<TInputParameters>, HidPlusPlusLongMessage<HidPlusPlusRawParameters>>(ref message), cancellationToken);
	}

	private Task<TOutputParameters> SendAsync<TOutputParameters>(in HidPlusPlusLongMessage<HidPlusPlusRawParameters> message, CancellationToken cancellationToken)
		where TOutputParameters : struct, IMessageResponseParameters
	{
		var buffer = BufferPool.Rent(20);
		MemoryMarshal.Write(buffer, ref Unsafe.AsRef(message));

		var tuple = Tuple.Create(buffer, 4, new TaskCompletionSource<HidPlusPlusLongMessage<HidPlusPlusRawParameters>>());

		if (Interlocked.CompareExchange(ref _currentSendOperation, tuple, null) is not null)
		{
			throw new InvalidOperationException("Another Send operation is already running.");
		}

		return SendAsyncCore<TOutputParameters>(buffer, tuple, cancellationToken);
	}

	private async Task<TOutputParameters> SendAsyncCore<TOutputParameters>
	(
		byte[] buffer,
		Tuple<byte[], int, TaskCompletionSource<HidPlusPlusLongMessage<HidPlusPlusRawParameters>>> currentOperation,
		CancellationToken cancellationToken
	)
		where TOutputParameters : struct, IMessageResponseParameters
	{
		try
		{
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			var result = await currentOperation.Item3.Task.WaitAsync(_requestTimeout, cancellationToken).ConfigureAwait(false);
			return Unsafe.As<HidPlusPlusLongMessage<HidPlusPlusRawParameters>, HidPlusPlusLongMessage<TOutputParameters>>(ref result).Parameters;
		}
		finally
		{
			// Make sure that the current operation is forgotten.
			Interlocked.CompareExchange(ref _currentSendOperation, null, currentOperation);
			BufferPool.Return(buffer, false);
		}
	}

	public async Task<HidPlusPlusVersion> GetProtocolVersionAsync(byte deviceId, CancellationToken cancellationToken)
	{
		// This an arbitrarily chosen value to validate the (ping) response.
		// Logitech Options and G-Hub use 0x90 here.
		const byte Beacon = 0xA5;

		var getVersionResponse = await SendAsync<Root.GetVersion.Request, Root.GetVersion.Response>
		(
			deviceId,
			0,
			Root.GetVersion.FunctionId,
			new Root.GetVersion.Request { Beacon = Beacon },
			cancellationToken
		);

		if (getVersionResponse.Beacon != Beacon)
		{
			throw new Exception("Received an invalid response.");
		}

		return new HidPlusPlusVersion(getVersionResponse.Major, getVersionResponse.Minor);
	}

	public ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>> GetFeaturesAsync(byte deviceId, CancellationToken cancellationToken)
		=> deviceId == _defaultDeviceIndex && _cachedFeatures is not null ?
			new ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>>(_cachedFeatures) :
			new ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>>(GetFeaturesAsyncCore(deviceId, cancellationToken));

	private async Task<ReadOnlyDictionary<HidPlusPlusFeature, byte>> GetFeaturesAsyncCore(byte deviceId, CancellationToken cancellationToken)
	{
		var getCountResponse = await SendAsync<FeatureSet.GetCount.Response>(deviceId, 1, FeatureSet.GetCount.FunctionId, cancellationToken);

		if (getCountResponse.Count == 0)
		{
			throw new InvalidOperationException();
		}

		int count = getCountResponse.Count + 1;

		var features = new Dictionary<HidPlusPlusFeature, byte>(count)
		{
			{ HidPlusPlusFeature.Root, 0 }
		};

		for (int i = 1; i < count; i++)
		{
			var getFeatureIdResponse = await SendAsync<FeatureSet.GetFeatureId.Request, FeatureSet.GetFeatureId.Response>
			(
				deviceId,
				1,
				1,
				new FeatureSet.GetFeatureId.Request { Index = (byte)i },
				cancellationToken
			);

			features.Add(getFeatureIdResponse.FeatureId, (byte)i);
		}

		var readonlyFeatures = new ReadOnlyDictionary<HidPlusPlusFeature, byte>(features);

		if (deviceId == _defaultDeviceIndex)
		{
			Volatile.Write(ref _cachedFeatures, readonlyFeatures);
		}

		return readonlyFeatures;
	}

	public async Task<byte> GetFeatureIndexAsync(byte deviceId, HidPlusPlusFeature feature, CancellationToken cancellationToken)
	{
		var getFeatureResponse = await SendAsync<Root.GetFeature.Request, Root.GetFeature.Response>
		(
			deviceId,
			0,
			Root.GetFeature.FunctionId,
			new Root.GetFeature.Request { FeatureId = feature },
			cancellationToken
		);

		return getFeatureResponse.Index;
	}
}
