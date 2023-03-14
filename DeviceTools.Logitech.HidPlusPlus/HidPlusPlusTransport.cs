using System.Buffers;
using System.Collections;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

namespace DeviceTools.Logitech.HidPlusPlus;

// I really wished to avoid having an inner class, but this seems clearer to separate between Hid++ 1.0 and HID++ 2.0.
// The transport will be mostly version-agnostic, while the wrapper will be specialized for either version.
public sealed class HidPlusPlusTransport
{
	public const int ShortReportId = 0x10;
	public const int LongReportId = 0x11;
	public const int VeryLongReportId = 0x12;
	public const int ExtraLongReportId = 0x13;

	private enum ReadTaskResult
	{
		EndOfStream = 0,
		TaskCanceled = 1,
		DeviceDisconnected = 2,
	}

	// This is the private implementation of the device state, which must not be exposed publicly.
	private sealed class DeviceState : IDisposable
	{
		public byte ProtocolFlavor;
		public PendingOperation? PendingOperation;
		public HidPlusPlusRawNotificationHandler? NotificationHandler;
		public object? CustomState;

		public void Dispose()
		{
			if (Interlocked.Exchange(ref PendingOperation, null) is { } operation)
			{
				operation.TrySetCanceled();
			}
		}
	}

	// This is the public API to access device state. It will lazily access DeviceState objects.
	public readonly struct DeviceConfiguration
	{
		private readonly HidPlusPlusTransport? _transport;

		internal DeviceConfiguration(HidPlusPlusTransport transport, byte deviceIndex)
		{
			_transport = transport;
			DeviceIndex = deviceIndex;
		}

		/// <summary>Gets the device index of the device.</summary>
		public byte DeviceIndex { get; }

		/// <summary>Gets a value indicating if this device configuration is the default one.</summary>
		public bool IsDefault => _transport?.TryGetDeviceState(DeviceIndex) is null;

		/// <summary>Triggered when a notification has been received for the device.</summary>
		/// <remarks>
		/// <para>
		/// In the cases of <see cref="HidPlusPlusProtocolFlavor.Unknown"/> or <see cref="HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess"/>,
		/// notifications can be either HID++ 1.0 (RAP) or HID++ 2.0 (FAP) messages. Processing must be done accordingly in order to avoid ambiguities.
		/// </para>
		/// <para>
		/// If no handler has been set up for the device, notifications will be raised on the transport's <see cref="HidPlusPlusTransport.NotificationReceived"/> event.
		/// </para>
		/// </remarks>
		public event HidPlusPlusRawNotificationHandler NotificationReceived
		{
			add
			{
				if (_transport is null) throw new InvalidOperationException();

				AddHandler(ref _transport.GetOrCreateDeviceState(DeviceIndex).NotificationHandler, value);
			}
			remove
			{
				if (_transport?.TryGetDeviceState(DeviceIndex) is { } state)
				{
					RemoveHandler(ref state.NotificationHandler, value);
				}
			}
		}

		/// <summary>Gets or sets the protocol flavor currently used by the device.</summary>
		/// <remarks>
		/// <para>
		/// The value <see cref="HidPlusPlusProtocolFlavor.Unknown"/> is allowed, and indicates that the transport functions in an ambiguous mode,
		/// where incoming messages are interpreted with best effort according to the ambiguity.
		/// </para>
		/// <para>
		/// HID++ 2.0 devices on an Unifying receiver (HID++ 1.0) should use the special value <see cref="HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess"/> to indicate that they require
		/// special processing. The USB receiver will send wireless notifications using their device index, which would conflict with the HID++ 2.0 interpretation of messages.
		/// </para>
		/// <para>
		/// While the underlying transport of the HID++ protocols is mostly the same across versions, knowing the actual protocol flavor helps resolving a few ambiguities in input message parsing.
		/// The protocol version should generally be assigned after having sent an autodetect request on the transport, which will allow discriminating between the two.
		/// This class supports the ambiguous working mode in order to allow such an autodetection, but it is not intended to be used in that ambiguous mode for a long duration.
		/// </para>
		/// <para>
		/// Ambiguities regarding message parsing mainly involve error notifications where the SUB_ID / Feature Index values used by the two protocol flavors could have a different meaning in the other.
		/// e.g. There could be a feature at index 0x8F on an HID++ 2.0 device, and SUB_ID 0xFF is SYNC for HID++ 1.0.
		/// </para>
		/// <para>
		/// Changes of the protocol flavor for a given device should be kept to the minimum necessary, as it will affect the quality of the message parsing.
		/// Especially, having the wrong protocol flavor may lead to some device messages being lost due to incorrect interpretation.
		/// Generally, the protocol flavor should not be changed once the device is detected for the first time.
		/// Cases where it would be applicable to change the protocol flavor would be for example, when device pairing is changed on an USB receiver.
		/// </para>
		/// </remarks>
		public readonly HidPlusPlusProtocolFlavor ProtocolFlavor
		{
			get
			{
				return _transport?.TryGetDeviceState(DeviceIndex) is { } state ?
					(HidPlusPlusProtocolFlavor)Volatile.Read(ref state.ProtocolFlavor) :
					default;
			}
			set
			{
				if ((byte)value is > 3) throw new ArgumentOutOfRangeException(nameof(value));

				// Avoid preemptively allocating the device state if we set the protocol flavor to its default value.
				DeviceState? state;
				if (value == HidPlusPlusProtocolFlavor.Unknown)
				{
					if (_transport is null || (state = _transport.TryGetDeviceState(DeviceIndex)) is null)
					{
						return;
					}
				}
				else
				{
					if (_transport is null) throw new InvalidOperationException();
					state = _transport.GetOrCreateDeviceState(DeviceIndex);
				}

				Volatile.Write(ref state.ProtocolFlavor, (byte)value);
			}
		}

		/// <summary>Gets a reference to a custom state that can be stored per-device.</summary>
		public ref object? CustomState
		{
			get
			{
				if (_transport is null) throw new InvalidOperationException();
				return ref _transport.GetOrCreateDeviceState(DeviceIndex).CustomState;
			}
		}

		/// <summary>See <see cref="ProtocolFlavor"/>.</summary>
		/// <remarks>https://github.com/dotnet/csharplang/discussions/2068</remarks>
		public void SetProtocolFlavor(HidPlusPlusProtocolFlavor value) => ProtocolFlavor = value;
	}

	/// <summary>A collection of device configurations associated with the transport.</summary>
	/// <remarks>This collection must be used to access properties related to a specific device index.</remarks>
	public readonly struct DeviceConfigurationCollection : IReadOnlyList<DeviceConfiguration>
	{
		public struct Enumerator : IEnumerator<DeviceConfiguration>
		{
			private readonly HidPlusPlusTransport _transport;
			private int _index;

			public Enumerator(HidPlusPlusTransport transport)
			{
				_transport = transport;
				_index = -1;
			}

			public DeviceConfiguration Current => new(_transport, (byte)_index);
			object IEnumerator.Current => Current;

			public bool MoveNext() => unchecked((uint)++_index) < 256;

			public void Reset() => _index = -1;

			public void Dispose() { }
		}

		private readonly HidPlusPlusTransport _transport;

		internal DeviceConfigurationCollection(HidPlusPlusTransport transport) => _transport = transport;

		public DeviceConfiguration this[int index] => new(_transport, checked((byte)index));

		public int Count => 256;

		public Enumerator GetEnumerator() => new Enumerator(_transport);
		IEnumerator<DeviceConfiguration> IEnumerable<DeviceConfiguration>.GetEnumerator() => GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private static void AddHandler(ref HidPlusPlusRawNotificationHandler? storage, HidPlusPlusRawNotificationHandler? handler)
	{
		var @base = Volatile.Read(ref storage);
		while (true)
		{
			if (ReferenceEquals(@base, @base = Interlocked.CompareExchange(ref storage, (HidPlusPlusRawNotificationHandler?)Delegate.Combine(@base, handler), @base)))
			{
				break;
			}
		}
	}

	private static void RemoveHandler(ref HidPlusPlusRawNotificationHandler? storage, HidPlusPlusRawNotificationHandler? handler)
	{
		var @base = Volatile.Read(ref storage);
		while (true)
		{
			if (ReferenceEquals(@base, @base = Interlocked.CompareExchange(ref storage, (HidPlusPlusRawNotificationHandler?)Delegate.Remove(@base, handler), @base)))
			{
				break;
			}
		}
	}

	// The buffers in these pools are lazily allocated, so if the corresponding size is not used, no memory will be allocated.
	// We allocate a bit more buffers for smaller messages, as the number of buffers required concurrently is likely to be a bit greater for them.
	private static readonly BufferPool ShortBufferPool = new BufferPool(7, 80);
	private static readonly BufferPool LongBufferPool = new BufferPool(20, 40);
	private static readonly BufferPool VeryLongBufferPool = new BufferPool(64, 20);

	private readonly HidFullDuplexStream? _shortMessageStream;
	private readonly HidFullDuplexStream? _longMessageStream;
	private readonly HidFullDuplexStream? _veryLongMessageStream;

	private HidPlusPlusRawNotificationHandler? _defaultNotificationHandler;

	private DeviceStates<DeviceState> _deviceStates;
	private CancellationTokenSource? _disposeCancellationTokenSource;
	private readonly Task _readTask;

	private readonly SupportedReports _supportedReports;
	private readonly byte _softwareId;

	// TODO: Reimplement requests timeouts. It could possibly break if we don't do this.
	private readonly TimeSpan _requestTimeout;

	/// <summary>Initializes a new instance of the class <see cref="HidPlusPlusTransport"/>.</summary>
	/// <remarks>
	/// <para>
	/// Requiring up to three different streams is not a convenient way to operate, however Windows will split devices on top level collection, so we are left with little choice here.
	/// It is possible that some future devices will choose to have a single top level collection, which would allow us to receive all reports on the same stream, but currently, we'll expect each
	/// stream to handle only one specific report of one specific length.
	/// </para>
	/// <para>
	/// Not all devices support all message lengths. But at least short messages or long messages will be supported by a given device.
	/// It is the responsibility of the caller to properly analyze the device and to provide a stream for each supported message length. Proper operation cannot be guaranteed otherwise.
	/// </para>
	/// <para>
	/// Knowing which reports are supported by a device can typically be done by looking at each device interface for the device and their HID Usage Page and Usage ID.
	/// For HID++ 1.0, FF00 / 0001 will typically identify the device interface to use for short messages, and FF00 / 0002 the one for long messages.
	/// For HID++ 2.0, the scheme is a bit smarter and indicates the whole set of supported reports in the HID Usage, under Usage Page FF43.
	/// e.g. FF43 / 0202 indicates the device interface for long messages of a device supporting only long messages.
	/// The most significant byte indicates the supported reports/message lengths, and the least significant byte, which one is supported by the device interface.
	/// </para>
	/// </remarks>
	/// <param name="shortMessageStream">A stream to use for receiving or sending short messages.</param>
	/// <param name="longMessageStream">A stream to use for receiving or sending long messages.</param>
	/// <param name="veryLongMessageStream">A stream to use for receiving or sending very long messages.</param>
	/// <param name="featureAccessSoftwareId">The software ID to use for Feature Access Protocol (HID++ 2.0).</param>
	/// <param name="requestTimeout">A timeout to apply for messages sent to the device.</param>
	/// <exception cref="ArgumentException">None of the required streams were provided.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="featureAccessSoftwareId"/> has a value outside the allowed range of 1..15.</exception>
	public HidPlusPlusTransport
	(
		HidFullDuplexStream? shortMessageStream,
		HidFullDuplexStream? longMessageStream,
		HidFullDuplexStream? veryLongMessageStream,
		byte featureAccessSoftwareId,
		TimeSpan requestTimeout
	)
	{
		// Hoping this is a reasonable assumption here.
		if (_shortMessageStream is null && longMessageStream is null) throw new ArgumentException("Must at least provide a stream for short or long messages.");
		if (featureAccessSoftwareId is 0 or > 15) throw new ArgumentOutOfRangeException(nameof(featureAccessSoftwareId));

		_shortMessageStream = shortMessageStream;
		_longMessageStream = longMessageStream;
		_veryLongMessageStream = veryLongMessageStream;
		_requestTimeout = requestTimeout;
		_supportedReports = (_shortMessageStream is not null ? SupportedReports.Short : 0) |
			(_longMessageStream is not null ? SupportedReports.Long : 0) |
			(_veryLongMessageStream is not null ? SupportedReports.VeryLong : 0);
		_softwareId = featureAccessSoftwareId;
		_disposeCancellationTokenSource = new CancellationTokenSource();
		_readTask = ReadAsync(_disposeCancellationTokenSource.Token);
	}

	public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposeCancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			cts.Dispose();
			if (_shortMessageStream is not null)
			{
				await _shortMessageStream.DisposeAsync().ConfigureAwait(false);
			}
			if (_longMessageStream is not null)
			{
				await _longMessageStream.DisposeAsync().ConfigureAwait(false);
			}
			if (_veryLongMessageStream is not null)
			{
				await _veryLongMessageStream.DisposeAsync().ConfigureAwait(false);
			}
			try
			{
				await _readTask.ConfigureAwait(false);
			}
			catch { }
		}
	}

	/// <summary>Triggered when a notification has been received and was not handled by any device-specific handlers.</summary>
	/// <remarks>
	/// By default, this event will be triggered for every notification, including broadcast notifications and device-specific notifications.
	/// In the case of HID++ 2.0, notifications will be filtered based on <see cref="FeatureAccessSoftwareId"/>.
	/// </remarks>
	public event HidPlusPlusRawNotificationHandler NotificationReceived
	{
		add => AddHandler(ref _defaultNotificationHandler, value);
		remove => RemoveHandler(ref _defaultNotificationHandler, value);
	}

	/// <summary>Gets the reports supported by this transport.</summary>
	public SupportedReports SupportedReports => _supportedReports;

	/// <summary>Gets the software ID used by this transport to receive and send HID++ 2.0 (FAP) messages.</summary>
	public byte FeatureAccessSoftwareId => _softwareId;

	/// <summary>Gets the collection of device configurations for this transport.</summary>
	public DeviceConfigurationCollection Devices => new(this);

	public Task WaitForCompletionAsync() => _readTask;

	private DeviceState GetOrCreateDeviceState(byte deviceIndex)
	{
		ref var storage = ref _deviceStates.GetReference(deviceIndex);

		if (Volatile.Read(ref storage) is { } state)
		{
			return state;
		}
		else
		{
			state = new();
			return Interlocked.CompareExchange(ref storage, state, null) ?? state;
		}
	}

	private DeviceState? TryGetDeviceState(byte deviceIndex)
	{
		ref var storage = ref _deviceStates.TryGetReference(deviceIndex);

		return Unsafe.IsNullRef(ref storage) ? null : Volatile.Read(ref storage);
	}

	private async Task<ReadTaskResult> ReadAsync(CancellationToken cancellationToken)
	{
		const uint ErrorDeviceNotConnected = 0x8007048F;

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		var tasks = CreateReadTasks(cts.Token);

		// We ultimately want to wait for all the tasks, but if one of the task fails independently, we want to cancel all others in case it is a bug.
		var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);

		// Force other tasks to complete if they aren't already. (Most of the time, they will complete in the same way in a close time period)
		cts.Cancel();

		// This value will be overwritten in all the cases that matter.
		ReadTaskResult? result = null;

		switch (completedTask.Status)
		{
		case TaskStatus.RanToCompletion:
			result = ReadTaskResult.EndOfStream;
			break;
		case TaskStatus.Canceled:
			result = ReadTaskResult.TaskCanceled;
			break;
		case TaskStatus.Faulted:
			var exception = completedTask.Exception!;

			if (exception.InnerExceptions.Count == 1)
			{
				var ex = exception.InnerException;

				if (ex is IOException { HResult: unchecked((int)ErrorDeviceNotConnected) })
				{
					result = ReadTaskResult.DeviceDisconnected;
				}
			}
			break;
		}

		if (tasks.Length == 1)
		{
			if (result is null)
			{
				await completedTask.ConfigureAwait(false);
			}
		}
		else
		{
			try
			{
				await Task.WhenAll(tasks).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// The goal of this code is to propagate exceptions in a way that make sense. (e.g. not having an AggregateException exposing cancellations or 3 IOException for device disconnection)
				// I don't know if it can be made simpler.
				if (result is null)
				{
					if (ex is AggregateException aex)
					{
						if (aex.InnerExceptions.Count == 1 && aex.InnerException is not OperationCanceledException)
						{
							ExceptionDispatchInfo.Throw(aex.InnerException!);
						}

						List<Exception>? nonCancellationExceptions = null;

						foreach (var iex in aex.InnerExceptions)
						{
							if (iex is not OperationCanceledException)
							{
								(nonCancellationExceptions ??= new(tasks.Length)).Add(iex);
							}
						}

						if (nonCancellationExceptions is not null && nonCancellationExceptions.Count != aex.InnerExceptions.Count)
						{
							if (nonCancellationExceptions.Count == 1)
							{
								ExceptionDispatchInfo.Throw(nonCancellationExceptions[0]);
							}
							else
							{
								throw new AggregateException(nonCancellationExceptions);
							}
						}
					}
					throw;
				}
			}
		}

		// The compiler won't be able to follow the flow, but unless there is a mistake in the code above, the cases where result is null should already have exited by throwing an exception.
		return result.GetValueOrDefault();
	}

	private Task[] CreateReadTasks(CancellationToken cancellationToken)
	{
		int taskCount = 0;

		if (_shortMessageStream is not null) taskCount++;
		if (_longMessageStream is not null) taskCount++;
		if (_veryLongMessageStream is not null) taskCount++;

		var tasks = new Task[taskCount];
		var semaphore = taskCount > 1 ? new SemaphoreSlim(1) : null;

		int index = 0;

		if (_shortMessageStream is not null) tasks[index++] = ReadAsync(ShortBufferPool, _shortMessageStream, semaphore, cancellationToken);
		if (_longMessageStream is not null) tasks[index++] = ReadAsync(LongBufferPool, _longMessageStream, semaphore, cancellationToken);
		if (_veryLongMessageStream is not null) tasks[index++] = ReadAsync(VeryLongBufferPool, _veryLongMessageStream, semaphore, cancellationToken);

		return tasks;
	}

	// Read the messages from one of the streams, synchronizing with a semaphore when necessary.
	// When there is only one stream (e.g. simple Bluetooth HID++ 2.0 device with only long messages), no synchronization is needed.
	// It is simpler to process messages one at a time (as they are supposed to come, before Windows splits them up in multiple devices)
	private async Task ReadAsync(BufferPool pool, HidFullDuplexStream stream, SemaphoreSlim? semaphore, CancellationToken cancellationToken)
	{
		// Start by yielding the execution, so that we don't progress too much before the other tasks are started.
		await Task.Yield();

		using var buffer = pool.Rent();

		try
		{
			while (true)
			{
				var memory = buffer.Memory;
				int count = await stream.ReadAsync(buffer.Memory, cancellationToken).ConfigureAwait(false);

				if (count == 0)
				{
					return;
				}

				memory = memory[..count];

				// I'm not happy about doing the processing in there, but using a semaphore for synchronization (implicit queuing) seems simpler than moving the processing loop outside.
				if (semaphore is null)
				{
					ProcessReadMessage(memory.Span);
				}
				else
				{
					await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
					try
					{
						ProcessReadMessage(memory.Span);
					}
					finally
					{
						semaphore.Release();
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private void ProcessReadMessage(ReadOnlySpan<byte> message)
	{
		// Interpret the message header in a more accessible format.
		ref readonly var header = ref Unsafe.As<byte, RawMessageHeader>(ref Unsafe.AsRef(message[0]));

		var deviceState = TryGetDeviceState(header.DeviceIndex);
		// Cache the protocol flavor, as it could change from Unknown to a defined value, but we need to operate on a contant.
		var protocolFlavor = deviceState is not null ? (HidPlusPlusProtocolFlavor)Volatile.Read(ref deviceState.ProtocolFlavor) : default;
		// Also read the pending current send operation for the current device.
		var currentOperation = deviceState is not null ? Volatile.Read(ref deviceState.PendingOperation) : null;

		// Handle errors first.
		// From the HID++ 1.0 documentation, error messages are always a short message, with SUB_ID 8F. (They are tied to register set/get operations)
		// For HID++ 2.0, errors will be reported with feature index FF, but they could be any kind of length depending on what is supported?
		// There is an ambiguity on the interpretation of these messages depending on the protocol version, so we'll catch both of them here and inspect what should be done more in depth.
		// This means that processing of non-error messages 8F and FF will be somewhat slower compared to other non-error messages, but it is a good code and performance compromise,
		// considering that they are not *that* likely to occur in the real world. (e.g. An HID++ 2.0 device would need to expose at least 144 features)
		if (header.SubIdOrFeatureIndex == 0xFF)
		{
			// SUB_ID FF is SYNC for HID++ 1.0 (RAP)
			if (protocolFlavor is HidPlusPlusProtocolFlavor.RegisterAccess) goto NotAnError;
		}
		else if (header.SubIdOrFeatureIndex == 0x8F && header.ReportId == ShortReportId)
		{
			// Feature Index 8F is a potentially allowed value for HID++ 2.0 (FAP).
			// In case the protocol flavor is unknown, we compare the SUB_ID value to the current send operation in order to estimate if this is and HID++ 1.0 error code or HID++ 2.0 response.
			if (protocolFlavor is HidPlusPlusProtocolFlavor.FeatureAccess or HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess ||
				protocolFlavor is HidPlusPlusProtocolFlavor.Unknown && currentOperation is { Header: { SubIdOrFeatureIndex: 0x8F } })
			{
				goto NotAnError;
			}
		}
		else
		{
			goto NotAnError;
		}

		// If the error can be matched with the sent message header, propagate it as an exception.
		// 
		if (currentOperation is not null &&
			header.AddressOrFunctionIdAndSoftwareId == currentOperation.Header.SubIdOrFeatureIndex &&
			message[4] == currentOperation.Header.AddressOrFunctionIdAndSoftwareId)
		{
			byte errorCode = message[5];

			Exception ex = header.SubIdOrFeatureIndex == 0x8F ?
				new global::DeviceTools.Logitech.HidPlusPlus.HidPlusPlus1Exception((global::DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.ErrorCode)errorCode) :
				new global::DeviceTools.Logitech.HidPlusPlus.HidPlusPlus2Exception((global::DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.ErrorCode)errorCode);

			Volatile.Write(ref deviceState!.PendingOperation, null);
			currentOperation.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(ex));
		}

		// Processing of the (error) message is complete.
		return;

	// From now on, messages are identified as not being an error.
	// It is easier to use a label for this, as it makes the error parsing/detection code leaner and cleaner.
	NotAnError:;
		// First try to match the message with the current pending operation.
		if (currentOperation is not null)
		{
			if (currentOperation.Header.EqualsWithoutReportId(header))
			{
				Volatile.Write(ref deviceState!.PendingOperation, null);
				currentOperation.TrySetResult(message);
			}
		}
		// If the message is not a match with the pending operation, it may be a notification.
		// Generally, there should not be two pending operations on the HID++ device, and it would return errors until the first operation is completed.
		else
		{
			byte potentialSoftwareId = (byte)(header.AddressOrFunctionIdAndSoftwareId & 0xF);

			// Try to determine if the message may be a notification for each protocol favor.
			// It is only easy to determine this when the protocol flavor is not ambiguous. These heuristics may need to be tuned to produce better results.
			// If the message is nto a notification, this switch must return from the method.
			switch (protocolFlavor)
			{
			case HidPlusPlusProtocolFlavor.Unknown:
				// Try to evict messages that would match neither HID++ 1.0 nor HID++ 2.0 profiles.
				// It is far from being perfect, but it should at least reduce clutter a bit.
				if (header.SubIdOrFeatureIndex >= 0x80 && potentialSoftwareId != 0 && potentialSoftwareId != _softwareId)
				{
					return;
				}
				break;
			case HidPlusPlusProtocolFlavor.RegisterAccess:
				// For HID++ 1.0, SUB_ID 00..7F are notifications.
				if (header.SubIdOrFeatureIndex >= 0x80)
				{
					return;
				}
				break;
			case HidPlusPlusProtocolFlavor.FeatureAccess:
				if (potentialSoftwareId != 0 && potentialSoftwareId != _softwareId)
				{
					return;
				}
				break;
			case HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess:
				if (potentialSoftwareId != 0 && potentialSoftwareId != _softwareId && (SubId)header.SubIdOrFeatureIndex is not SubId.DeviceDisconnect and not SubId.DeviceConnect)
				{
					return;
				}
				break;
			default:
				return;
			}

			// Raise the event if the message has been determined to be a notification (Not a 100% guarantee but we will try to forward it anyway)
			HidPlusPlusRawNotificationHandler? handler = null;
			if (deviceState is not null) handler = Volatile.Read(ref deviceState.NotificationHandler);
			if (handler is null) handler = Volatile.Read(ref _defaultNotificationHandler);

			try
			{
				handler?.Invoke(message);
			}
			catch
			{
				// TODO: Log / Handle
			}
		}
	}

	// This method was previously implemented as one version for each input message length, but having it handle everything seems better.
	// It being non generic will also help with generic code creep, although generic code is still used for callers.
	internal Task SendAsync(
		byte deviceIndex,
		byte subIdOrFeatureIndex,
		byte addressOrFunctionIdAndSoftwareId,
		ReadOnlySpan<byte> parameters,
		Func<RawMessageHeader, PendingOperation> operationFactory,
		CancellationToken cancellationToken)
	{
		RawMessageHeader header;
		PendingOperation operation;
		HidFullDuplexStream stream;
		IMemoryOwner<byte> buffer;
		byte reportId;
		BufferPool bufferPool;

		// We support fallback to long messages, in case the device only supports long messages, as is the case with some HID++ 2.0 devices.
		// In case these devices are connected through an HID++ 1.0 receiver, it will automatically handle short messages and transfer them to the device.
		// NB: We do allow empty parameters to be passed here in all cases, hence the awkward L - N <= 0 syntax below.
		if (parameters.Length - 3 <= 0 && _shortMessageStream is not null)
		{
			reportId = ShortReportId;
			stream = _shortMessageStream;
			bufferPool = ShortBufferPool;
		}
		else if (parameters.Length - 16 <= 0 && _longMessageStream is not null)
		{
			reportId = LongReportId;
			stream = _longMessageStream;
			bufferPool = LongBufferPool;
		}
		else if (parameters.Length - 60 <= 0 && _veryLongMessageStream is not null)
		{
			// We implicitly support devices that would support only very long reports here.
			// This is currently disabled in the constructor, but it would be easy to allow.
			reportId = VeryLongReportId;
			stream = _veryLongMessageStream;
			bufferPool = VeryLongBufferPool;
		}
		else
		{
			// This should not happen. We check that necessary streams are available in the constructor, and callers of this method will ensure that correct parameter size is enforced.
			throw new InvalidOperationException("Invalid message size.");
		}

		header = new RawMessageHeader(reportId, deviceIndex, subIdOrFeatureIndex, addressOrFunctionIdAndSoftwareId);
		operation = operationFactory(header);
		buffer = bufferPool.Rent();

		var remaining = buffer.Memory.Span;
		Unsafe.WriteUnaligned(ref remaining[0], header);
		remaining = remaining[4..];
		parameters.CopyTo(remaining);
		remaining = remaining[parameters.Length..];

		// Carefully clean extra bytes in the buffer, in case we are sending a message in a larger payload.
		remaining.Clear();

		SendAsyncCore(buffer, stream, operation, cancellationToken);

		return operation.WaitAsync();
	}

	// Yeah, I know async void is bad and all that, but, we catch all exceptions here, and we make sure to provide feedback to the PendingOperation object.
	// This will avoid useless copy operations between tasks, as some messages could be quite long and we don't allocate a separate object for them.
	// For short messages (7 bytes; 3 bytes of relevant parameter data), allocating an object would be overkill, for long messages (20 bytes; 16 of parameter data) it is certainly ambiguous,
	// and for very large (64 bytes), allocating an object could be better.
	// Awaiting this method would require to basically unwrap a ValueTask<Task<Parameters>>, which would require more allocations and copying of the data from one task to another.
	// So in this case, async void should be an appropriate design.
	private async void SendAsyncCore(IMemoryOwner<byte> message, HidFullDuplexStream stream, PendingOperation operation, CancellationToken cancellationToken)
	{
		try
		{
			using (message)
			{
				// The pending operation will be unset in the ReadAsync method, so the current method should actually be quite short
				await SetPendingOperationAsync(operation.Header.DeviceIndex, operation, cancellationToken).ConfigureAwait(false);
				await stream.WriteAsync(message.Memory).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			operation.TrySetException(ex);
		}
	}

	private async ValueTask SetPendingOperationAsync(byte deviceIndex, PendingOperation operation, CancellationToken cancellationToken)
	{
		var state = GetOrCreateDeviceState(deviceIndex);
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (Interlocked.CompareExchange(ref state.PendingOperation, operation, null) is { } pending)
			{
				await pending.WaitAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			else
			{
				break;
			}
		}
	}

	internal void EnsureSortMessageSupport()
	{
		if (_shortMessageStream is null) throw new NotSupportedException("Short messages are not supported by this device.");
	}

	internal void EnsureLongMessageSupport()
	{
		if (_shortMessageStream is null) throw new NotSupportedException("Long messages are not supported by this device.");
	}

	internal void EnsureVeryLongMessageSupport()
	{
		if (_shortMessageStream is null) throw new NotSupportedException("Very long messages are not supported by this device.");
	}
}
