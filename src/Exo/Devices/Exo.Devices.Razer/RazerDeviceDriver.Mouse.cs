using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using DeviceTools;
using Exo.Features;
using Exo.Features.Mouses;

namespace Exo.Devices.Razer;

public abstract partial class RazerDeviceDriver
{
	private class Mouse :
		BaseDevice,
		IDeviceDriver<IMouseDeviceFeature>,
		IMouseDpiFeature,
		IMouseDynamicDpiFeature,
		IMouseConfigurableDpiPresetsFeature,
		IMouseDpiPresetsFeature,
		IMouseConfigurablePollingFrequencyFeature
	{
		public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

		private DotsPerInch[] _dpiPresets;
		private ulong _currentDpi;
		private readonly ushort _maximumDpi;
		private readonly ushort _maximumPollingFrequency;
		private byte _currentPollingFrequencyDivider;

		private readonly Dictionary<ushort, byte> _supportedPollingFrequencyToDividerMapping;
		private readonly ImmutableArray<ushort> _supportedPollingFrequencies;
		private readonly IDeviceFeatureSet<IMouseDeviceFeature> _mouseFeatures;

		public Mouse
		(
			IRazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator? periodicEventGenerator,
			in DeviceInformation deviceInformation,
			ImmutableArray<RazerLedId> ledIds,
			ushort maximumDpi,
			ushort maximumPollingFrequency,
			ImmutableArray<byte> supportedPollingFrequencyDividerPowers,
			DotsPerInch[] initialDpiPresets,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex
		) : base(transport, periodicEventGenerator, deviceInformation, ledIds, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
			_dpiPresets = initialDpiPresets;
			_maximumDpi = maximumDpi;
			_maximumPollingFrequency = maximumPollingFrequency;
			var supportedPollingFrequencyToDividerMapping = new Dictionary<ushort, byte>();
			var supportedPollingFrequencies = new ushort[supportedPollingFrequencyDividerPowers.Length];
			for (int i = 0; i < supportedPollingFrequencyDividerPowers.Length; i++)
			{
				byte power = supportedPollingFrequencyDividerPowers[i];
				ushort frequency = (ushort)(maximumPollingFrequency >> power);
				supportedPollingFrequencies[i] = frequency;
				supportedPollingFrequencyToDividerMapping.Add(frequency, (byte)(1 << power));
			}
			Array.Sort(supportedPollingFrequencies);
			_supportedPollingFrequencyToDividerMapping = supportedPollingFrequencyToDividerMapping;
			_supportedPollingFrequencies = ImmutableCollectionsMarshal.AsImmutableArray(supportedPollingFrequencies);
			_mouseFeatures = HasDpi ?
				HasDpiPresets ?
					FeatureSet.Create
					<
						IMouseDeviceFeature,
						Mouse,
						IMouseDpiFeature,
						IMouseDynamicDpiFeature,
						IMouseDpiPresetsFeature,
						IMouseConfigurableDpiPresetsFeature,
						IMouseConfigurablePollingFrequencyFeature
					>
					(this) :
					FeatureSet.Create
					<
						IMouseDeviceFeature,
						Mouse,
						IMouseDpiFeature,
						IMouseDynamicDpiFeature,
						IMouseConfigurablePollingFrequencyFeature
					>
					(this) :
				FeatureSet.Create
					<
						IMouseDeviceFeature,
						Mouse,
						IMouseConfigurableDpiPresetsFeature,
						IMouseConfigurablePollingFrequencyFeature
					>
					(this);
		}

		protected override async ValueTask InitializeAsync(CancellationToken cancellationToken)
		{
			await base.InitializeAsync(cancellationToken).ConfigureAwait(false);

			if (MustSetSensorState5)
			{
				await _transport.SetSensorStateAsync(0, 5, 0, cancellationToken).ConfigureAwait(false);
			}

			if (HasDpi)
			{
				if (HasDpiPresets)
				{
					//if (HasDpiPresetsRead)
					//{
						RazerMouseDpiProfileConfiguration dpiConfiguration;
						if (HasDpiPresetsV2)
						{
							dpiConfiguration = await _transport.GetDpiPresetsV2Async(cancellationToken).ConfigureAwait(false);
						}
						else
						{
							byte currentPresetIndex = await _transport.GetCurrentDpiPresetAsync(true, cancellationToken).ConfigureAwait(false);
							dpiConfiguration = await _transport.GetDpiPresetsV1Async(cancellationToken).ConfigureAwait(false);
							dpiConfiguration = new RazerMouseDpiProfileConfiguration(currentPresetIndex, dpiConfiguration.Presets);
						}
						_dpiPresets = Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(dpiConfiguration.Presets)!, p => new DotsPerInch(p.X, p.Y));
					//}
					//else if (_dpiPresets.Length > 0)
					//{
					//	var dpiConfiguration = new RazerMouseDpiProfileConfiguration
					//	(
					//		(byte)_dpiPresets.Length,
					//		ImmutableArray.CreateRange
					//		(
					//			ImmutableCollectionsMarshal.AsImmutableArray(_dpiPresets),
					//			dpi => new RazerMouseDpiPreset(dpi.Horizontal, dpi.Vertical, 0)
					//		)
					//	);

					//	if (HasDpiPresetsV2)
					//	{
					//		await _transport.SetDpiPresetsV2Async(false, dpiConfiguration, cancellationToken).ConfigureAwait(false);
					//	}
					//	else
					//	{
					//		await _transport.SetDpiPresetsV1Async(true, dpiConfiguration, cancellationToken).ConfigureAwait(false);
					//		await _transport.SetCurrentDpiPresetAsync(true, dpiConfiguration.ActivePresetIndex, cancellationToken).ConfigureAwait(false);
					//		await _transport.SetDpiAsync(true, _dpiPresets[dpiConfiguration.ActivePresetIndex - 1], cancellationToken).ConfigureAwait(false);
					//	}
					//}
				}
				var dpi = await _transport.GetDpiAsync(false, cancellationToken).ConfigureAwait(false);
				_currentDpi = GetRawDpiValue(_dpiPresets, dpi.Horizontal, dpi.Vertical);
			}
			_currentPollingFrequencyDivider = await _transport.GetPollingIntervalAsync(cancellationToken).ConfigureAwait(false);
		}

		IDeviceFeatureSet<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => _mouseFeatures;

		protected override void OnDeviceDpiChange(ushort dpiX, ushort dpiY)
		{
			var profiles = Volatile.Read(ref _dpiPresets);
			ulong newDpi = GetRawDpiValue(profiles, dpiX, dpiY);
			OnDeviceDpiChange(newDpi);
		}

		private void OnDeviceDpiChange(byte rawProfileIndex, DotsPerInch dpi)
			=> OnDeviceDpiChange(GetRawDpiValue(rawProfileIndex, dpi.Horizontal, dpi.Vertical));

		private void OnDeviceDpiChange(ulong rawDpi)
		{
			ulong oldDpi = Interlocked.Exchange(ref _currentDpi, rawDpi);

			if (rawDpi != oldDpi)
			{
				if (DpiChanged is { } dpiChanged)
				{
					_ = Task.Run
					(
						() =>
						{
							try
							{
								dpiChanged(this, GetDpi(rawDpi));
							}
							catch (Exception ex)
							{
								// TODO: Log
							}
						}
					);
				}
			}
		}

		private static ulong GetRawDpiValue(DotsPerInch[] profiles, ushort dpiX, ushort dpiY)
			=> GetRawDpiValue(GetDpiProfileIndex(profiles, dpiX, dpiY), dpiX, dpiY);

		private static ulong GetRawDpiValue(byte rawProfileIndex, ushort dpiX, ushort dpiY)
			=> (ulong)rawProfileIndex << 32 | (uint)dpiY << 16 | dpiX;

		private static byte GetDpiProfileIndex(DotsPerInch[] profiles, ushort dpiX, ushort dpiY)
		{
			for (int i = 0; i < profiles.Length; i++)
			{
				if (profiles[i].Horizontal == dpiX && profiles[i].Vertical == dpiY)
				{
					return (byte)(i + 1);
				}
			}

			return 0;
		}

		private static MouseDpiStatus GetDpi(ulong rawValue)
			=> new()
			{
				PresetIndex = (byte)(rawValue >> 32) is not 0 and byte i ? (byte)(i - 1) : null,
				Dpi = new((ushort)rawValue, (ushort)(rawValue >> 16))
			};

		private event Action<Driver, MouseDpiStatus>? DpiChanged;

		MouseDpiStatus IMouseDpiFeature.CurrentDpi => GetDpi(Volatile.Read(ref _currentDpi));

		event Action<Driver, MouseDpiStatus> IMouseDynamicDpiFeature.DpiChanged
		{
			add => DpiChanged += value;
			remove => DpiChanged -= value;
		}

		ImmutableArray<DotsPerInch> IMouseDpiPresetsFeature.DpiPresets => ImmutableCollectionsMarshal.AsImmutableArray(Volatile.Read(ref _dpiPresets));

		bool IMouseDynamicDpiFeature.AllowsSeparateXYDpi => true;
		byte IMouseConfigurableDpiPresetsFeature.MaxPresetCount => 5;

		public DotsPerInch MaximumDpi => new(_maximumDpi);

		async ValueTask IMouseDpiPresetsFeature.ChangeCurrentPresetAsync(byte activePresetIndex, CancellationToken cancellationToken)
		{
			if (activePresetIndex >= (uint)_dpiPresets.Length) throw new ArgumentOutOfRangeException(nameof(activePresetIndex));
			await _transport.SetDpiAsync(false, _dpiPresets[activePresetIndex], cancellationToken).ConfigureAwait(false);

			// Remember: The index we receive as input of this method is zero-based, but the devices use 1-based indices.
			byte newPresetIndex = (byte)(activePresetIndex + 1);
			if (HasDpiPresetsV2)
			{
				// NB: I don't know if we want to persist stuff here.
				// It seems that reading volatile profile data might not always be possible, but writing is, somehow?
				// Downside of this is that the selected profile will not be persisted, but that might help preserve the storage. (Is it flash or NVRAM ?)
				await _transport.SetDpiPresetsV2Async
				(
					false,
					new RazerMouseDpiProfileConfiguration
					(
						newPresetIndex,
						ImmutableArray.CreateRange
						(
							ImmutableCollectionsMarshal.AsImmutableArray(_dpiPresets),
							dpi => new RazerMouseDpiPreset(dpi.Horizontal, dpi.Vertical, 0)
						)
					),
					cancellationToken
				).ConfigureAwait(false);
			}
			else
			{
				// NB: I don't know if we really want to always commit the preset index, but this is what synapse does anyway.
				await _transport.SetCurrentDpiPresetAsync(true, newPresetIndex, cancellationToken);
			}

			OnDeviceDpiChange(newPresetIndex, _dpiPresets[activePresetIndex]);
		}

		async ValueTask IMouseConfigurableDpiPresetsFeature.SetDpiPresetsAsync(byte activePresetIndex, ImmutableArray<DotsPerInch> dpiPresets, CancellationToken cancellationToken)
		{
			if (dpiPresets.IsDefault) throw new ArgumentNullException(nameof(dpiPresets));
			if (dpiPresets.Length is < 1 or > 5) throw new ArgumentException();
			if (activePresetIndex >= (uint)dpiPresets.Length) throw new ArgumentOutOfRangeException(nameof(activePresetIndex));

			foreach (var preset in dpiPresets)
			{
				if (preset.Horizontal == 0 || preset.Horizontal > _maximumDpi || preset.Vertical == 0 || preset.Vertical > _maximumDpi)
				{
					throw new ArgumentException("Invalid DPI specified.");
				}
			}

			// Remember: The index we receive as input of this method is zero-based, but the devices use 1-based indices.
			byte newPresetIndex = (byte)(activePresetIndex + 1);
			if (HasDpiPresetsV2)
			{
				await _transport.SetDpiPresetsV2Async
				(
					true,
					new RazerMouseDpiProfileConfiguration(newPresetIndex, ImmutableArray.CreateRange(dpiPresets, dpi => new RazerMouseDpiPreset(dpi.Horizontal, dpi.Vertical, 0))),
					cancellationToken
				).ConfigureAwait(false);
			}
			else
			{
				await _transport.SetDpiPresetsV1Async
				(
					true,
					new RazerMouseDpiProfileConfiguration(newPresetIndex, ImmutableArray.CreateRange(dpiPresets, dpi => new RazerMouseDpiPreset(dpi.Horizontal, dpi.Vertical, 0))),
					cancellationToken
				).ConfigureAwait(false);
				await _transport.SetCurrentDpiPresetAsync(true, newPresetIndex, cancellationToken).ConfigureAwait(false);
			}

			OnDeviceDpiChange(newPresetIndex, dpiPresets[activePresetIndex]);
		}

		ushort IMouseConfigurablePollingFrequencyFeature.PollingFrequency => (ushort)(_maximumPollingFrequency >>> BitOperations.Log2(_currentPollingFrequencyDivider));

		ImmutableArray<ushort> IMouseConfigurablePollingFrequencyFeature.SupportedPollingFrequencies => _supportedPollingFrequencies;

		async ValueTask IMouseConfigurablePollingFrequencyFeature.SetPollingFrequencyAsync(ushort pollingFrequency, CancellationToken cancellationToken)
		{
			if (!_supportedPollingFrequencyToDividerMapping.TryGetValue(pollingFrequency, out byte divider)) throw new ArgumentOutOfRangeException(nameof(pollingFrequency), pollingFrequency, $"Unsupported polling frequency: {pollingFrequency}.");

			await _transport.SetPollingIntervalAsync(divider, cancellationToken).ConfigureAwait(false);
		}
	}
}
