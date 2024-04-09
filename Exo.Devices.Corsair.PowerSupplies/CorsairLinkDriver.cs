using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.Numerics;
using Exo;
using Exo.Discovery;
using Exo.Features;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Corsair.PowerSupplies;

public sealed class CorsairLinkDriver : Driver, IDeviceDriver<ISensorDeviceFeature>, ISensorsFeature
{
	private const ushort CorsairVendorId = 0x1B1C;

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[ProductId(VendorIdSource.Usb, CorsairVendorId, 0x1C08)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILoggerFactory loggerFactory,
		ImmutableArray<SystemDevicePath> keys,
		ushort productId,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		ImmutableArray<DeviceObjectInformation> devices,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		if (devices.Length != 2) throw new InvalidOperationException("Expected exactly two devices.");
		if (deviceInterfaces.Length != 2) throw new InvalidOperationException("Expected exactly two device interfaces.");

		string? deviceName = null;
		foreach (var deviceInterface in deviceInterfaces)
		{
			if (deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid interfaceClassGuid) && interfaceClassGuid == DeviceInterfaceClassGuids.Hid)
			{
				deviceName = deviceInterface.Id;
			}
		}

		if (deviceName is null) throw new InvalidOperationException("HID device interface not found.");

		var stream = new HidFullDuplexStream(deviceName);
		CorsairLinkHidTransport transport;
		try
		{
			transport = await CorsairLinkHidTransport.CreateAsync(loggerFactory.CreateLogger<CorsairLinkHidTransport>(), stream, cancellationToken).ConfigureAwait(false);
			string friendlyName = await transport.ReadStringAsync(0x9A, cancellationToken).ConfigureAwait(false);
			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new CorsairLinkDriver
				(
					loggerFactory.CreateLogger<CorsairLinkDriver>(),
					transport,
					friendlyName,
					new DeviceConfigurationKey("Corsair", topLevelDeviceName, $"{CorsairVendorId:X4}{productId:X4}", null)
				)
			);
		}
		catch
		{
			await stream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private readonly CorsairLinkHidTransport _transport;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly ImmutableArray<ISensor> _sensors;
	private readonly ILogger<CorsairLinkDriver> _logger;

	public override DeviceCategory DeviceCategory => DeviceCategory.PowerSupply;

	public IDeviceFeatureSet<ISensorDeviceFeature> Features => _sensorFeatures;
	public ImmutableArray<ISensor> Sensors => _sensors;

	private CorsairLinkDriver
	(
		ILogger<CorsairLinkDriver> logger,
		CorsairLinkHidTransport transport,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_transport = transport;
		_logger = logger;
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, CorsairLinkDriver, ISensorsFeature>(this);
	}

	public override async ValueTask DisposeAsync()
	{
		await _transport.DisposeAsync().ConfigureAwait(false);
	}
}

// NB: This likely not the V1 protocol, but it is the one for HX1200i & similar devices.
internal sealed class CorsairLinkHidTransport : IAsyncDisposable
{
	private interface IPendingCommand
	{
		void WriteRequest(Span<byte> buffer);
		void ProcessResponse(ReadOnlySpan<byte> buffer);
		Task WaitAsync(CancellationToken cancellationToken);
		void Cancel();
	}

	private interface IPendingCommand<T> : IPendingCommand
	{
		new Task<T> WaitAsync(CancellationToken cancellationToken);
	}

	private abstract class ResultCommand<T> : TaskCompletionSource<T>, IPendingCommand<T>
	{
		public ResultCommand() { }

		public abstract void WriteRequest(Span<byte> buffer);
		public abstract void ProcessResponse(ReadOnlySpan<byte> buffer);

		Task IPendingCommand.WaitAsync(CancellationToken cancellationToken) => Task.WaitAsync(cancellationToken);
		public Task<T> WaitAsync(CancellationToken cancellationToken) => Task.WaitAsync(cancellationToken);

		public void Cancel() => TrySetCanceled();
	}

	private sealed class HandshakeCommand : ResultCommand<string>
	{
		public HandshakeCommand() { }

		public override void WriteRequest(Span<byte> buffer)
		{
			buffer[0] = 0xFE;
			buffer[1] = 0x03;
		}

		public override void ProcessResponse(ReadOnlySpan<byte> buffer)
		{
			if (buffer[0] == 0xFE && buffer[1] == 0x03)
			{
				var data = buffer[2..];
				int endIndex = data.IndexOf((byte)0);
				endIndex = endIndex < 0 ? data.Length : endIndex;
				TrySetResult(Encoding.UTF8.GetString(data[..endIndex]));
			}
		}
	}

	private abstract class ReadCommand<T> : ResultCommand<T>
	{
		private readonly byte _command;

		public ReadCommand(byte command) => _command = command;

		public sealed override void WriteRequest(Span<byte> buffer)
		{
			buffer[0] = 0x03;
			buffer[1] = _command;
		}

		public sealed override void ProcessResponse(ReadOnlySpan<byte> buffer)
		{
			if (buffer[0] == 3 && buffer[1] == _command)
			{
				ProcessResult(buffer[2..]);
			}
		}

		protected abstract void ProcessResult(ReadOnlySpan<byte> data);
	}

	private sealed class StringReadCommand : ReadCommand<string>
	{
		public StringReadCommand(byte command) : base(command) { }

		protected override void ProcessResult(ReadOnlySpan<byte> data)
		{
			int endIndex = data.IndexOf((byte)0);
			endIndex = endIndex < 0 ? data.Length : endIndex;
			TrySetResult(Encoding.UTF8.GetString(data[..endIndex]));
		}
	}

	private sealed class ByteReadCommand : ReadCommand<byte>
	{
		public ByteReadCommand(byte command) : base(command) { }

		protected override void ProcessResult(ReadOnlySpan<byte> data) => TrySetResult(data[0]);
	}

	private sealed class Linear11ReadCommand : ReadCommand<Linear11>
	{
		public Linear11ReadCommand(byte command) : base(command) { }

		protected override void ProcessResult(ReadOnlySpan<byte> data) => TrySetResult(Linear11.FromRawValue(LittleEndian.ReadUInt16(data[0])));
	}

	public static async ValueTask<CorsairLinkHidTransport> CreateAsync(ILogger<CorsairLinkHidTransport> logger, HidFullDuplexStream stream, CancellationToken cancellationToken)
	{
		var transport = new CorsairLinkHidTransport(logger, stream);
		try
		{
			transport._handshakeDeviceName = await transport.HandshakeAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await transport.DisposeAsync().ConfigureAwait(false);
			throw;
		}
		return transport;
	}

	// The message length is hardcoded to 64 bytes + report ID.
	private const int MessageLength = 65;

	private static readonly object DisposedSentinel = new();

	private readonly HidFullDuplexStream _stream;
	private readonly byte[] _buffers;
	private object? _currentWaitState;
	private readonly ILogger<CorsairLinkHidTransport> _logger;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _readTask;
	private string? _handshakeDeviceName;

	private CorsairLinkHidTransport(ILogger<CorsairLinkHidTransport> logger, HidFullDuplexStream stream)
	{
		_stream = stream;
		_logger = logger;
		_buffers = GC.AllocateUninitializedArray<byte>(2 * MessageLength, true);
		_buffers[65] = 0; // Zero-initialize the write report ID.
		_cancellationTokenSource = new();
		_readTask = ReadAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			if (Interlocked.Exchange(ref _currentWaitState, DisposedSentinel) is IPendingCommand pendingCommand)
			{
				pendingCommand.Cancel();
			}
			await _readTask.ConfigureAwait(false);
			_stream.Dispose();
			cts.Dispose();
		}
	}

	private async Task ReadAsync(CancellationToken cancellationToken)
	{
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, 0, MessageLength);
			while (true)
			{
				// Data is received in fixed length packets, so we expect to always receive exactly the number of bytes that the buffer can hold.
				var remaining = buffer;
				do
				{
					int count;
					try
					{
						count = await _stream.ReadAsync(remaining, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						return;
					}

					if (count == 0)
					{
						return;
					}

					remaining = remaining[count..];
				}
				while (remaining.Length != 0);

				(Volatile.Read(ref _currentWaitState) as IPendingCommand)?.ProcessResponse(buffer.Span[1..]);
			}
		}
		catch
		{
			// TODO: Log
		}
	}

	private async ValueTask<T> ExecuteCommandAsync<T>(IPendingCommand<T> waitState, CancellationToken cancellationToken)
	{
		if (Interlocked.CompareExchange(ref _currentWaitState, waitState, null) is { } oldState)
		{
			ObjectDisposedException.ThrowIf(ReferenceEquals(oldState, DisposedSentinel), typeof(CorsairLinkHidTransport));
			throw new InvalidOperationException("An operation is already running.");
		}

		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
		try
		{
			waitState.WriteRequest(buffer.Span[1..]);
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			return await waitState.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			Interlocked.CompareExchange(ref _currentWaitState, null, waitState);
		}
	}

	private ValueTask<string> HandshakeAsync(CancellationToken cancellationToken) => ExecuteCommandAsync(new HandshakeCommand(), cancellationToken);

	public ValueTask<byte> ReadByteAsync(byte command, CancellationToken cancellationToken) => ExecuteCommandAsync(new ByteReadCommand(command), cancellationToken);

	public ValueTask<Linear11> ReadLinear11Async(byte command, CancellationToken cancellationToken) => ExecuteCommandAsync(new Linear11ReadCommand(command), cancellationToken);

	public ValueTask<string> ReadStringAsync(byte command, CancellationToken cancellationToken) => ExecuteCommandAsync(new StringReadCommand(command), cancellationToken);
}
