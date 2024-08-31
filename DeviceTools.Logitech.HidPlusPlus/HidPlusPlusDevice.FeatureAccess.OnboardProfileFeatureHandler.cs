using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;
using DeviceTools.Logitech.HidPlusPlus.Profiles;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class OnboardProfileFeatureHandler : FeatureHandler
		{
			private struct ProfileState
			{
				public byte Index;
				public bool IsEnabled;
				public bool IsLoaded;
				public Profile RawProfile;
			}

			private enum MessageKind
			{
				Initialize,
				Reset,
				ProfileChange,
				DpiChange,
				SwitchDeviceMode,
			}

			private readonly struct Message(MessageKind message, byte payload, object? taskCompletionSource, CancellationToken cancellationToken)
			{
				public readonly MessageKind Kind = message;
				public readonly byte Payload = payload;
				public readonly object? TaskCompletionSource = taskCompletionSource;
				public readonly CancellationToken CancellationToken = cancellationToken;
			}

			[Flags]
			private enum FeatureState : byte
			{
				None = 0x00,
				Initialized = 0x01,
				Supported = 0x02,
			}

			private static readonly UnboundedChannelOptions UnboundedChannelOptions = new() { AllowSynchronousContinuations = true, SingleReader = true, SingleWriter = false };

			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.OnboardProfiles;

			private OnBoardProfiles.GetInfo.Response _information;
			private ProfileState[] _profileStates;
			private readonly ChannelWriter<Message> _messageWriter;
			private byte[] _sectorBuffer;
			private DeviceMode _deviceMode;
			private FeatureState _state;
			private byte _currentDpiIndex;
			private sbyte _currentProfileIndex;
			private CancellationTokenSource? _cancellationTokenSource;
			private readonly Task _runTask;

			public OnboardProfileFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
				_sectorBuffer = [];
				_profileStates = [];
				var channel = Channel.CreateUnbounded<Message>();
				_messageWriter = channel;
				_cancellationTokenSource = new();
				_runTask = RunAsync(channel, HidPlusPlusTransportExtensions.DefaultRetryCount, _cancellationTokenSource.Token);
			}

			public override async ValueTask DisposeAsync()
			{
				if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
				{
					_messageWriter.TryComplete();
					cts.Cancel();
					await _runTask.ConfigureAwait(false);
					cts.Dispose();
				}
			}

			private FeatureState State
			{
				get => (FeatureState)Volatile.Read(ref Unsafe.As<FeatureState, byte>(ref _state));
				set => Volatile.Write(ref Unsafe.As<FeatureState, byte>(ref _state), (byte)value);
			}

			public bool IsInitialized => (State & FeatureState.Initialized) != 0;
			public bool IsSupported => (State & FeatureState.Supported) != 0;

			public DeviceMode DeviceMode => _deviceMode;

			public byte ProfileCount => _information.ProfileCount;

			public byte? CurrentProfileIndex
			{
				get
				{
					var currentProfileIndex = _currentProfileIndex;
					return (uint)(int)currentProfileIndex < (uint)_profileStates.Length ? (byte)currentProfileIndex : null;
				}
			}
			public ref readonly Profile CurrentProfile
			{
				get
				{
					if ((uint)(int)_currentProfileIndex < (uint)_profileStates.Length) return ref _profileStates[(int)(uint)_currentProfileIndex].RawProfile;
					throw new InvalidOperationException("There is no active profile.");
				}
			}

			public byte CurrentDpiIndex => _currentDpiIndex;

			private void EnsureSupport()
			{
				if (!IsSupported) throw new InvalidOperationException("The device is currently unsupported.");
			}

			private async Task RunAsync(ChannelReader<Message> messageReader, int retryCount, CancellationToken cancellationToken)
			{
				try
				{
					await foreach (var message in messageReader.ReadAllAsync().ConfigureAwait(false))
					{
						// When the instance is disposed, the writer will be completed, so we quickly consume all remaining messages and propagate exceptions if needed.
						if (cancellationToken.IsCancellationRequested)
						{
							if (message.TaskCompletionSource is not null)
							{
								switch (message.Kind)
								{
								case MessageKind.Initialize:
								case MessageKind.SwitchDeviceMode:
									(message.TaskCompletionSource as TaskCompletionSource)?.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(GetType().FullName)));
									break;
								}
							}
							continue;
						}

						// If the CancellationToken associated with the message is canceled, we should skip execution of the message.
						if (message.CancellationToken.IsCancellationRequested)
						{
							switch (message.Kind)
							{
							case MessageKind.Initialize:
							case MessageKind.SwitchDeviceMode:
								(message.TaskCompletionSource as TaskCompletionSource)?.TrySetCanceled(message.CancellationToken);
								break;
							}
							continue;
						}

						switch (message.Kind)
						{
						case MessageKind.Initialize:
							try
							{
								if (message.CancellationToken.CanBeCanceled)
								{
									using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, message.CancellationToken);
									await InitializeStateAsync(retryCount, cts.Token).ConfigureAwait(false);
								}
								else
								{
									await InitializeStateAsync(retryCount, cancellationToken).ConfigureAwait(false);
								}
								(message.TaskCompletionSource as TaskCompletionSource)?.TrySetResult();
							}
							catch (Exception ex)
							{
								(message.TaskCompletionSource as TaskCompletionSource)?.TrySetException(ex);
							}
							break;
						case MessageKind.Reset:
							ResetState();
							break;
						case MessageKind.ProfileChange:
							if (!IsInitialized) continue;
							await HandleProfileChangeAsync((sbyte)message.Payload, retryCount, cancellationToken).ConfigureAwait(false);
							break;
						case MessageKind.DpiChange:
							if (!IsInitialized) continue;
							HandleDpiChange(message.Payload);
							break;
						case MessageKind.SwitchDeviceMode:
							if (!IsInitialized) continue;
							try
							{
								// NB: This operation should not be interrupted by anything else than device dispose.
								await InitializeStateAsync(retryCount, cancellationToken).ConfigureAwait(false);
								(message.TaskCompletionSource as TaskCompletionSource)?.TrySetResult();
							}
							catch (Exception ex)
							{
								(message.TaskCompletionSource as TaskCompletionSource)?.TrySetException(ex);
							}
							break;
						}
					}
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
				}
				catch
				{
					// TODO: Log
				}
			}

			public override Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				_messageWriter.TryWrite(new(MessageKind.Initialize, 0, tcs, cancellationToken));
				return tcs.Task.WaitAsync(cancellationToken);
			}

			public override void Reset() => _messageWriter.TryWrite(new(MessageKind.Reset, 0, null, default));

			protected override void HandleNotification(byte eventId, ReadOnlySpan<byte> response)
			{
				if (response.Length < 3) return;

				// These events will be generated if a change is initiated by the device itself.
				// On many devices, those events will never be generated out of the box, but it is possible to bind special actions for changing profiles or dpi on mouse buttons.
				// In that case, one of the two events can be generated, indicating applications that something changed on the device.
				if (eventId == OnBoardProfiles.GetCurrentProfile.EventId)
				{
					HandleProfileChange(Unsafe.As<byte, OnBoardProfiles.GetCurrentProfile.Response>(ref Unsafe.AsRef(in response[0])));
				}
				else if (eventId == OnBoardProfiles.GetCurrentDpiIndex.EventId)
				{
					HandleDpiChange(Unsafe.As<byte, OnBoardProfiles.GetCurrentDpiIndex.Response>(ref Unsafe.AsRef(in response[0])));
				}
			}

			private void HandleProfileChange(in OnBoardProfiles.GetCurrentProfile.Response response)
				=> _messageWriter.TryWrite(new(MessageKind.ProfileChange, (byte)(response.ActiveProfileIndex - 1), null, default));

			private void HandleDpiChange(in OnBoardProfiles.GetCurrentDpiIndex.Response response)
				=> _messageWriter.TryWrite(new(MessageKind.DpiChange, response.ActivePresetIndex, null, default));

			private async Task InitializeStateAsync(int retryCount, CancellationToken cancellationToken)
			{
				State = FeatureState.None;

				_information = await Device.SendWithRetryAsync<OnBoardProfiles.GetInfo.Response>(FeatureIndex, OnBoardProfiles.GetInfo.FunctionId, retryCount, cancellationToken).ConfigureAwait(false);

				// Same as in libratbag, this will prevent messing up with unsupported devices.
				bool isSupported = _information.MemoryType is OnBoardProfiles.MemoryType.G402 &&
					_information.ProfileFormat is OnBoardProfiles.ProfileFormat.G402 or OnBoardProfiles.ProfileFormat.G303 or OnBoardProfiles.ProfileFormat.G900 or OnBoardProfiles.ProfileFormat.G915 &&
					_information.MacroFormat is OnBoardProfiles.MacroFormat.G402 &&
					_information.SectorSize >= 16;

				if (_profileStates.Length < _information.ProfileCount)
				{
					Array.Resize(ref _profileStates, _information.ProfileCount);
				}

				if (!isSupported)
				{
					State = FeatureState.Initialized;
					return;
				}

				State = FeatureState.Supported;

				_deviceMode = (
					await Device.SendWithRetryAsync<OnBoardProfiles.GetDeviceMode.Response>
					(
						FeatureIndex,
						OnBoardProfiles.GetDeviceMode.FunctionId,
						retryCount,
						cancellationToken
					).ConfigureAwait(false)
				).Mode;

				// Round-up the buffer size to the closest multiple of 16. This is done to ease up write operations.
				int bufferSize = (_information.SectorSize + 0xF) & ~0xF;

				if (_sectorBuffer.Length < bufferSize)
				{
					_sectorBuffer = GC.AllocateUninitializedArray<byte>(bufferSize, true);
				}

				try
				{
					await ReadAndValidateSectorAsync(0, retryCount, cancellationToken).ConfigureAwait(false);
					ReadProfilesDirectory();
				}
				catch
				{
					State = FeatureState.Initialized;
					return;
				}

				var mode = await GetDeviceModeAsync(cancellationToken).ConfigureAwait(false);

				_currentProfileIndex = (sbyte)(await GetActiveProfileIndexAsync(cancellationToken).ConfigureAwait(false) - 1);

				if ((uint)(int)_currentProfileIndex < (uint)_profileStates.Length)
				{
					if (!_profileStates[(int)(uint)_currentProfileIndex].IsLoaded)
					{
						await LoadProfileAsync(_currentProfileIndex, retryCount, cancellationToken).ConfigureAwait(false);
					}

					_currentDpiIndex = await GetActiveDpiIndex(cancellationToken).ConfigureAwait(false);
				}

				State = FeatureState.Initialized | FeatureState.Supported;
			}

			private void ResetState()
			{
				State = _state & ~FeatureState.Initialized;
			}

			private void ReadProfilesDirectory()
			{
				var entries = MemoryMarshal.Cast<byte, OnBoardProfiles.ProfileEntry>(_sectorBuffer.AsSpan(0, (_information.SectorSize - 2) & ~3));

				for (int i = 0; i < _information.ProfileCount; i++)
				{
					ref readonly var entry = ref entries[i];
					ref var state = ref _profileStates[i];
					if (entry.ProfileIndex != i + 1) throw new InvalidDataException("Invalid profile index.");
					state.Index = entry.ProfileIndex;
					state.IsEnabled = entry.IsEnabled;
				}
			}

			private async Task EnsureProfileLoadedAsync(sbyte profileIndex, int retryCount, CancellationToken cancellationToken)
			{
				if ((uint)(int)profileIndex < (uint)_profileStates.Length && !_profileStates[(int)(uint)profileIndex].IsLoaded)
				{
					await LoadProfileAsync(profileIndex, retryCount, cancellationToken).ConfigureAwait(false);
				}
			}

			private async Task LoadProfileAsync(sbyte profileIndex, int retryCount, CancellationToken cancellationToken)
			{
				await ReadSectorAsync((byte)(profileIndex + 1), retryCount, cancellationToken).ConfigureAwait(false);
				_profileStates[(int)(uint)profileIndex].RawProfile = ParseProfile(_sectorBuffer.AsSpan(0, _information.SectorSize - 2));
				_profileStates[(int)(uint)profileIndex].IsLoaded = true;
			}

			private async Task HandleProfileChangeAsync(sbyte profileIndex, int retryCount, CancellationToken cancellationToken)
			{
				if (profileIndex == _currentProfileIndex) return;

				if ((uint)(int)profileIndex >= _profileStates.Length) throw new InvalidOperationException("Invalid profile index.");

				await EnsureProfileLoadedAsync(profileIndex, retryCount, cancellationToken).ConfigureAwait(false);

				var dpiIndex = await GetActiveDpiIndex(cancellationToken).ConfigureAwait(false);

				_currentProfileIndex = profileIndex;

				Device.OnProfileChanged((byte)_currentProfileIndex);

				HandleDpiChange(dpiIndex);
			}

			private void HandleDpiChange(byte dpiIndex)
			{
				_currentDpiIndex = dpiIndex;
				Device.OnDpiChanged(new DpiStatus(_currentDpiIndex, new(CurrentProfile.DpiPresets[_currentDpiIndex])));
			}

			private static Profile ParseProfile(ReadOnlySpan<byte> buffer)
				=> buffer.Length >= Unsafe.SizeOf<Profile>() ?
					Unsafe.As<byte, Profile>(ref Unsafe.AsRef(in buffer[0])) :
					ParseTruncatedProfile(buffer);

			[SkipLocalsInit]
			private static Profile ParseTruncatedProfile(ReadOnlySpan<byte> buffer)
			{
				Profile profile;
				Unsafe.SkipInit(out profile);
				var span = MemoryMarshal.CreateSpan(ref Unsafe.As<Profile, byte>(ref profile), Unsafe.SizeOf<Profile>());
				buffer.CopyTo(span);
				span.Slice(buffer.Length).Fill(0xFF);
				return profile;
			}

			private Task WriteProfileAsync(byte profileIndex, in Profile profile, CancellationToken cancellationToken)
			{
				int dataLength = _information.SectorSize - 2;
				MemoryMarshal.CreateSpan(ref Unsafe.As<Profile, byte>(ref Unsafe.AsRef(in profile)), dataLength).CopyTo(_sectorBuffer);
				if (Unsafe.SizeOf<Profile>() < dataLength)
				{
					_sectorBuffer.AsSpan(Unsafe.SizeOf<Profile>(), dataLength - Unsafe.SizeOf<Profile>()).Clear();
				}

				return WriteSectorAsync(profileIndex, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);
			}

			public Task SwitchToHostModeAsync(CancellationToken cancellationToken)
				=> PrepareSwitchToModeAsync(DeviceMode.Host, cancellationToken);

			public Task SwitchToOnBoardModeAsync(CancellationToken cancellationToken)
				=> PrepareSwitchToModeAsync(DeviceMode.OnBoardMemory, cancellationToken);

			private Task PrepareSwitchToModeAsync(DeviceMode mode, CancellationToken cancellationToken)
			{
				var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				_messageWriter.TryWrite(new(MessageKind.SwitchDeviceMode, (byte)mode, tcs, cancellationToken));
				return tcs.Task.WaitAsync(cancellationToken);
			}

			private async Task SwitchToModeAsync(DeviceMode mode, int retryCount, CancellationToken cancellationToken)
			{
				EnsureSupport();
				await Device.SendWithRetryAsync
				(
					FeatureIndex,
					OnBoardProfiles.SetDeviceMode.FunctionId,
					new OnBoardProfiles.SetDeviceMode.Request { Mode = mode },
					retryCount,
					cancellationToken
				).ConfigureAwait(false);
				_deviceMode = mode;
				if (mode == DeviceMode.OnBoardMemory)
				{
					var profileIndex = (sbyte)(await GetActiveProfileIndexAsync(cancellationToken).ConfigureAwait(false) - 1);
					await EnsureProfileLoadedAsync(profileIndex, retryCount, cancellationToken).ConfigureAwait(false);
					var dpiIndex = await GetActiveDpiIndex(cancellationToken).ConfigureAwait(false);
					_currentProfileIndex = profileIndex;
					HandleDpiChange(dpiIndex);
				}
			}

			public async ValueTask<DeviceMode> GetDeviceModeAsync(CancellationToken cancellationToken)
			{
				EnsureSupport();
				var response = await Device.SendAsync<OnBoardProfiles.GetDeviceMode.Response>(FeatureIndex, OnBoardProfiles.GetDeviceMode.FunctionId, cancellationToken).ConfigureAwait(false);
				return response.Mode;
			}

			public async ValueTask<byte> GetActiveProfileIndexAsync(CancellationToken cancellationToken)
			{
				EnsureSupport();
				var response = await Device.SendAsync<OnBoardProfiles.GetCurrentProfile.Response>(FeatureIndex, OnBoardProfiles.GetCurrentProfile.FunctionId, cancellationToken).ConfigureAwait(false);
				return response.ActiveProfileIndex;
			}

			public async ValueTask<byte> GetActiveDpiIndex(CancellationToken cancellationToken)
			{
				EnsureSupport();
				var response = await Device.SendAsync<OnBoardProfiles.GetCurrentDpiIndex.Response>(FeatureIndex, OnBoardProfiles.GetCurrentDpiIndex.FunctionId, cancellationToken).ConfigureAwait(false);
				return response.ActivePresetIndex;
			}

			public async Task SetActiveDpiIndex(byte dpiIndex, CancellationToken cancellationToken)
			{
				EnsureSupport();
				if (dpiIndex > 4) throw new ArgumentOutOfRangeException(nameof(dpiIndex));
				await Device.SendAsync(FeatureIndex, OnBoardProfiles.SetCurrentDpiIndex.FunctionId, new OnBoardProfiles.SetCurrentDpiIndex.Request { ActivePresetIndex = dpiIndex }, cancellationToken).ConfigureAwait(false);
				_currentDpiIndex = dpiIndex;
			}

			private static ushort CcittCrc(ReadOnlySpan<byte> bytes)
			{
				ushort value = 0xFFFF;

				for (int i = 0; i < bytes.Length; i++)
				{
					ushort tmp = (ushort)((value >> 8) ^ bytes[i]);
					value = (ushort)((value << 8) ^ (tmp = (ushort)(tmp ^ (tmp >> 4))) ^ (tmp <<= 5) ^ (tmp <<= 7));
				}

				return value;
			}

			private async Task ReadSectorAsync(ushort sectorIndex, int retryCount, CancellationToken cancellationToken)
			{
				var buffer = _sectorBuffer;
				int length = _information.SectorSize;

				var readRequest = new OnBoardProfiles.ReadMemory.Request() { SectorIndex = sectorIndex };
				int alignedReadLength = length & ~0xF;
				for (int offset = 0; offset < alignedReadLength; offset += 16)
				{
					readRequest.Offset = (ushort)offset;
					var response = await Device.SendWithRetryAsync<OnBoardProfiles.ReadMemory.Request, OnBoardProfiles.ReadMemory.Response>
					(
						FeatureIndex,
						OnBoardProfiles.ReadMemory.FunctionId,
						in readRequest,
						retryCount,
						cancellationToken
					).ConfigureAwait(false);
					response.CopyTo(buffer.AsSpan(offset, 16));
				}

				// If the sector size is not a multiple of 16, we need to read the last bytes using a non aligned read (thus reading bytes that we already read previously)
				if (alignedReadLength < length)
				{
					readRequest.Offset = (ushort)(length - 16);
					int remainingLength = length & 0xF;
					var response = await Device.SendWithRetryAsync<OnBoardProfiles.ReadMemory.Request, OnBoardProfiles.ReadMemory.Response>
					(
						FeatureIndex,
						OnBoardProfiles.ReadMemory.FunctionId,
						in readRequest,
						retryCount,
						cancellationToken
					).ConfigureAwait(false);
					OnBoardProfiles.ReadMemory.Response.AsReadOnlySpan(in response).Slice(16 - remainingLength).CopyTo(buffer.AsSpan(alignedReadLength, remainingLength));
				}
			}

			private async Task WriteSectorAsync(byte sectorIndex, int retryCount, CancellationToken cancellationToken)
			{
				var buffer = _sectorBuffer;
				int length = _information.SectorSize;

				UpdateCrc(buffer.AsSpan(0, length));

				var response = await Device.SendWithRetryAsync<OnBoardProfiles.StartWrite.Request, OnBoardProfiles.ReadMemory.Response>
				(
					FeatureIndex,
					OnBoardProfiles.StartWrite.FunctionId,
					new() { SectorIndex = sectorIndex, Offset = 0, Count = (ushort)length },
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				for (int offset = 0; offset < length; offset += 16)
				{
					await Device.SendWithRetryAsync
					(
						FeatureIndex,
						OnBoardProfiles.WriteMemory.FunctionId,
						in Unsafe.As<byte, OnBoardProfiles.WriteMemory.Request>(ref _sectorBuffer[offset]),
						retryCount,
						cancellationToken
					).ConfigureAwait(false);
					response.CopyTo(buffer.AsSpan(offset, 16));
				}

				await Device.SendWithRetryAsync
				(
					FeatureIndex,
					OnBoardProfiles.EndWrite.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);
			}

			private async Task ReadAndValidateSectorAsync(ushort sectorIndex, int retryCount, CancellationToken cancellationToken)
			{
				await ReadSectorAsync(sectorIndex, retryCount, cancellationToken).ConfigureAwait(false);
				ValidateSector(_sectorBuffer.AsSpan(0, _information.SectorSize));
			}

			private static void ValidateSector(ReadOnlySpan<byte> data)
			{
				int length = data.Length - 2;
				if (CcittCrc(data.Slice(0, length)) != BigEndian.ReadUInt16(in data[length]))
				{
					throw new InvalidDataException("Invalid CRC.");
				}
			}

			private static void UpdateCrc(Span<byte> data)
			{
				int length = data.Length - 2;
				BigEndian.Write(ref data[length], CcittCrc(data.Slice(0, length)));
			}
		}
	}
}
