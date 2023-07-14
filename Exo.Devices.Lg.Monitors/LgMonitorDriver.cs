using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Features.LightingFeatures;
using Exo.Features.MonitorFeatures;

namespace Exo.Devices.Lg.Monitors;

[ProductId(VendorIdSource.Usb, 0x043E, 0x9A8A)]
public class LgMonitorDriver : HidDriver, IDeviceDriver<IMonitorDeviceFeature>
{
	private static readonly Property[] RequestedDeviceInterfaceProperties = new Property[]
	{
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,
	};

	public static async Task<LgMonitorDriver> CreateAsync(string deviceName, CancellationToken cancellationToken)
	{
		// By retrieving the containerId, we'll be able to get all HID devices interfaces of the physical device at once.
		var containerId = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceInterface, deviceName, Properties.System.Devices.ContainerId, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// The display name of the container can be used as a default value for the device friendly name.
		string friendlyName = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// Make a device query to fetch all the matching HID device interfaces at once.
		var deviceInterfaces = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.DeviceInterface,
			RequestedDeviceInterfaceProperties,
			Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.Hid &
				Properties.System.Devices.ContainerId == containerId &
				Properties.System.DeviceInterface.Hid.VendorId == 0x043E,
			cancellationToken
		).ConfigureAwait(false);

		if (deviceInterfaces.Length != 2)
		{
			throw new InvalidOperationException("Expected two HID device interfaces.");
		}

		// Find the top-level device by requesting devices with children.
		// The device tree should be very simple in this case, so we expect this to directly return the top level device. It would not work on more complex scenarios.
		var devices = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.Device,
			Array.Empty<Property>(),
			Properties.System.Devices.ContainerId == containerId & Properties.System.Devices.Children.Exists(),
			cancellationToken
		).ConfigureAwait(false);

		if (devices.Length != 3)
		{
			throw new InvalidOperationException("Expected three parent devices.");
		}

		string[] deviceNames = new string[deviceInterfaces.Length + 1];
		string? deviceInterfaceName = null;
		string topLevelDeviceName = devices[0].Id;

		// Set the top level device name as the last device name now.
		deviceNames[^1] = topLevelDeviceName;

		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];
			deviceNames[i] = deviceInterface.Id;

			if (!deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage))
			{
				throw new InvalidOperationException($"No HID Usage Page associated with the device interface {deviceInterface.Id}.");
			}

			if ((usagePage & 0xFFFE) != 0xFF00)
			{
				throw new InvalidOperationException($"Unexpected HID Usage Page associated with the device interface {deviceInterface.Id}.");
			}

			if (!deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId))
			{
				throw new InvalidOperationException($"No HID Usage ID associated with the device interface {deviceInterface.Id}.");
			}

			if (usagePage == 0xFF00 && usageId == 0x01)
			{
				deviceInterfaceName = deviceInterface.Id;
			}
		}

		if (deviceInterfaceName is null)
		{
			throw new InvalidOperationException($"Could not find device interface with correct HID usages on the device interface {devices[0].Id}.");
		}

		byte sessionId = (byte)Random.Shared.Next(1, 256);
		var transport = await HidI2cTransport.CreateAsync(new HidFullDuplexStream(deviceInterfaceName), sessionId, HidI2cTransport.DefaultDeviceAddress, cancellationToken).ConfigureAwait(false);
		return new LgMonitorDriver
		(
			transport,
			Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
			friendlyName
		);
	}

	private readonly HidI2cTransport _transport;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;
	private readonly IDeviceFeatureCollection<IMonitorDeviceFeature> _monitorFeatures;

	private LgMonitorDriver
	(
		HidI2cTransport transport,
		ImmutableArray<string> deviceNames,
		string friendlyName
	) : base(deviceNames, friendlyName, default)
	{
		_transport = transport;
		_monitorFeatures = FeatureCollection.Empty<IMonitorDeviceFeature>();
		_allFeatures = FeatureCollection.Empty<IDeviceFeature>();
	}

	IDeviceFeatureCollection<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;
	public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class HidI2cTransport : IAsyncDisposable
{
	public const int DefaultDeviceAddress = 0x37;

	// The message length is hardcoded to 64 bytes + report ID.
	private const int MessageLength = 65;

	private const int WriteStateReady = 0;
	private const int WriteStateReserved = 1;
	private const int WriteStateDisposed = -1;

	private abstract class ResponseWaitState
	{
		public abstract void Complete(ReadOnlySpan<byte> message);
	}

	private abstract class ResponseWaitState<T> : ResponseWaitState
	{
		public TaskCompletionSource<T> TaskCompletionSource { get; } = new();
	}

	private sealed class HandshakeResponseWaitState : ResponseWaitState<bool>
	{
		public override void Complete(ReadOnlySpan<byte> message)
		{
			if (message[0] == 0x0c && message[3] == 0x00)
			{
				if (message.Slice(9, 3).SequenceEqual("HID"u8))
				{
					TaskCompletionSource.TrySetResult(true);
				}
				else
				{
					TaskCompletionSource.TrySetResult(false);
				}
			}
		}
	}

	private readonly HidFullDuplexStream _stream;
	private readonly byte[] _buffers;
	private readonly byte _sessionId;
	private readonly byte _deviceAddress;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _readAsyncTask;
	private readonly ConcurrentDictionary<byte, ResponseWaitState> _pendingOperations;
	private int _writeState;

	/// <summary>Creates a new instance of the class <see cref="HidI2cTransport"/>.</summary>
	/// <param name="stream">A stream to use for receiving and sending messages.</param>
	/// <param name="sessionId">A byte value to be used for identifying requests done by the instance.</param>
	/// <param name="cancellationToken"></param>
	public static Task<HidI2cTransport> CreateAsync(HidFullDuplexStream stream, byte sessionId, CancellationToken cancellationToken)
		=> CreateAsync(stream, sessionId, DefaultDeviceAddress, cancellationToken);

	/// <summary>Creates a new instance of the class <see cref="HidI2cTransport"/>.</summary>
	/// <param name="stream">A stream to use for receiving and sending messages.</param>
	/// <param name="sessionId">A byte value to be used for identifying requests done by the instance.</param>
	/// <param name="deviceAddress">The address of the I2C device. Defaults to <c>0x37</c>.</param>
	/// <param name="cancellationToken"></param>
	public static async Task<HidI2cTransport> CreateAsync(HidFullDuplexStream stream, byte sessionId, byte deviceAddress, CancellationToken cancellationToken)
	{
		var transport = new HidI2cTransport(stream, sessionId, deviceAddress);
		try
		{
			await transport.HandshakeAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await transport.DisposeAsync().ConfigureAwait(false);
			throw;
		}
		return transport;
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _stream.DisposeAsync().ConfigureAwait(false);
		await _readAsyncTask.ConfigureAwait(false);
	}

	private HidI2cTransport(HidFullDuplexStream stream, byte sessionId, byte deviceAddress)
	{
		_stream = stream;
		_sessionId = sessionId;
		_deviceAddress = deviceAddress;
		_buffers = GC.AllocateUninitializedArray<byte>(2 * MessageLength, true);
		_buffers[65] = 0; // Zero-initialize the write report ID.
		_cancellationTokenSource = new CancellationTokenSource();
		_readAsyncTask = ReadAsync(_cancellationTokenSource.Token);
		_pendingOperations = new();
	}

	private byte BeginWrite()
	{
		int oldState = Interlocked.CompareExchange(ref _writeState, WriteStateReserved, WriteStateReady);
		if (oldState != WriteStateReady)
		{
			if (oldState == WriteStateDisposed) throw new ObjectDisposedException(nameof(HidI2cTransport));
			else throw new InvalidOperationException("A write operation is already pending.");
		}
		return (byte)(oldState >>> 24);
	}

	private void EndWrite(byte sequenceNumber)
	{
		Interlocked.CompareExchange(ref _writeState, (sequenceNumber + 1) << 24 | WriteStateReady, sequenceNumber << 24 | WriteStateReserved);
	}

	private async Task HandshakeAsync(CancellationToken cancellationToken)
	{
		bool result;
		byte sequenceNumber = BeginWrite();
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);

			WriteHandshakeRequest(buffer.Span[1..], _sessionId, 0);

			var waitState = new HandshakeResponseWaitState();

			_pendingOperations.TryAdd(sequenceNumber, waitState);

			try
			{
				await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				result = await waitState.TaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				_pendingOperations.TryRemove(new(sequenceNumber, waitState));
				throw;
			}
		}
		finally
		{
			EndWrite(sequenceNumber);
		}

		if (!result)
		{
			throw new InvalidDataException("The device handshake failed because invalid data was returned.");
		}
	}

	private static void WriteHandshakeRequest(Span<byte> buffer, byte sessionId, byte sequenceNumber)
	{
		buffer.Clear();

		buffer[0] = 0x0C;
		buffer[1] = sequenceNumber;
		buffer[2] = sessionId;
		buffer[3] = 0x01;
		buffer[4] = 0x80;
		buffer[5] = 0x1a;
		buffer[6] = 0x06;
	}

	private async Task ReadAsync(CancellationToken cancellationToken)
	{
		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, 0, 65);
		try
		{
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

					remaining = remaining.Slice(count);
				}
				while (remaining.Length != 0);

				ProcessReadMessage(buffer.Span[1..]);
			}
		}
		catch
		{
			// TODO: Log the exception
		}
	}

	private void ProcessReadMessage(ReadOnlySpan<byte> message)
	{
		if (message[2] != _sessionId) return;

		byte sequenceNumber = message[1];

		if (_pendingOperations.TryRemove(sequenceNumber, out var state))
		{
			state.Complete(message);
		}
	}
}
