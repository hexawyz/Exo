using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;
using DeviceTools.Logitech.HidPlusPlus.Profiles;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class OnboardProfileFeatureHandler : FeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.OnboardProfiles;

			private OnBoardProfiles.GetInfo.Response _information;
			private DeviceMode _deviceMode;
			private bool _isDeviceSupported;
			private byte _currentDpiIndex;
			private byte[] _sectorBuffer;
			private Profile _currentProfile;

			public OnboardProfileFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
				_sectorBuffer = [];
			}

			public bool IsSupported => _isDeviceSupported;

			public DeviceMode DeviceMode => _deviceMode;

			public ref readonly Profile CurrentProfile => ref _currentProfile;

			public byte CurrentDpiIndex => _currentDpiIndex;

			private void EnsureSupport()
			{
				if (!IsSupported) throw new InvalidOperationException("The device is currently unsupported.");
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				_information = await Device.SendWithRetryAsync<OnBoardProfiles.GetInfo.Response>(FeatureIndex, OnBoardProfiles.GetInfo.FunctionId, retryCount, cancellationToken).ConfigureAwait(false);

				// Same as in libratbag, this will prevent messing up with unsupported devices.
				_isDeviceSupported = _information.MemoryType is OnBoardProfiles.MemoryType.G402 &&
					_information.ProfileFormat is OnBoardProfiles.ProfileFormat.G402 or OnBoardProfiles.ProfileFormat.G303 or OnBoardProfiles.ProfileFormat.G900 or OnBoardProfiles.ProfileFormat.G915 &&
					_information.MacroFormat is OnBoardProfiles.MacroFormat.G402 &&
					_information.SectorSize >= 16;

				if (!IsSupported) return;

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
				}
				catch
				{
					_isDeviceSupported = false;
				}

				if (!_isDeviceSupported) return;

				var mode = await GetDeviceModeAsync(cancellationToken).ConfigureAwait(false);

				byte activeProfileIndex = await GetActiveProfileIndexAsync(cancellationToken).ConfigureAwait(false);

				if (activeProfileIndex > 0)
				{
					await ReadAndValidateSectorAsync(activeProfileIndex, retryCount, cancellationToken).ConfigureAwait(false);

					_currentProfile = ParseProfile(_sectorBuffer.AsSpan(0, _information.SectorSize - 2));

					_currentDpiIndex = await GetActiveDpiIndex(cancellationToken).ConfigureAwait(false);
				}
			}

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
			{
				// TODO: We NEED to make sure to have the profile loaded at this point.
			}

			private void HandleDpiChange(in OnBoardProfiles.GetCurrentDpiIndex.Response response)
			{
				_currentDpiIndex = response.ActivePresetIndex;
				Device.OnDpiChanged(new DpiStatus(_currentDpiIndex, new(_currentProfile.DpiPresets[_currentDpiIndex])));
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
				=> SwitchToModeAsync(DeviceMode.Host, cancellationToken);

			public Task SwitchToOnBoardModeAsync(CancellationToken cancellationToken)
				=> SwitchToModeAsync(DeviceMode.OnBoardMemory, cancellationToken);

			private async Task SwitchToModeAsync(DeviceMode mode, CancellationToken cancellationToken)
			{
				EnsureSupport();
				await Device.SendAsync(FeatureIndex, OnBoardProfiles.SetDeviceMode.FunctionId, new OnBoardProfiles.SetDeviceMode.Request { Mode = DeviceMode.Host }, cancellationToken).ConfigureAwait(false);
				_deviceMode = mode;
				if (mode == DeviceMode.OnBoardMemory)
				{
					// TODO: Ensure that current profile is loaded.
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
