using System.Collections.Immutable;
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
		IMouseDpiPresetFeature
	{
		public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

		private DotsPerInch[] _dpiProfiles;
		private ulong _currentDpi;
		private readonly ushort _maximumDpi;

		private readonly IDeviceFeatureSet<IMouseDeviceFeature> _mouseFeatures;

		public Mouse(
			IRazerProtocolTransport transport,
			Guid lightingZoneId,
			ushort maximumDpi,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex
		) : base(transport, lightingZoneId, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
			_dpiProfiles = [];
			_maximumDpi = maximumDpi;
			_mouseFeatures = FeatureSet.Create<IMouseDeviceFeature, Mouse, IMouseDpiFeature, IMouseDynamicDpiFeature, IMouseDpiPresetFeature>(this);
		}

		protected override async ValueTask InitializeAsync(CancellationToken cancellationToken)
		{
			await base.InitializeAsync(cancellationToken).ConfigureAwait(false);

			var dpiLevels = await _transport.GetDpiProfilesAsync(false, cancellationToken).ConfigureAwait(false);
			_dpiProfiles = Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(dpiLevels.Profiles)!, p => new DotsPerInch(p.X, p.Y));
			var dpi = await _transport.GetDpiAsync(false, cancellationToken).ConfigureAwait(false);
			_currentDpi = (uint)dpi.Vertical << 16 | dpi.Horizontal;
		}

		IDeviceFeatureSet<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => _mouseFeatures;

		protected override void OnDeviceDpiChange(ushort dpiX, ushort dpiY)
		{
			var profiles = Volatile.Read(ref _dpiProfiles);

			uint profileIndex = 0;
			for (int i = 0; i < profiles.Length; i++)
			{
				if (profiles[i].Horizontal == dpiX && profiles[i].Vertical == dpiY)
				{
					profileIndex = (uint)i + 1;
					break;
				}
			}

			ulong newDpi = (ulong)profileIndex << 32 | (uint)dpiY << 16 | dpiX;
			ulong oldDpi = Interlocked.Exchange(ref _currentDpi, newDpi);

			if (newDpi != oldDpi)
			{
				if (DpiChanged is { } dpiChanged)
				{
					_ = Task.Run
					(
						() =>
						{
							try
							{
								dpiChanged(this, GetDpi(newDpi));
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

		private static MouseDpiStatus GetDpi(ulong rawValue)
			=> new()
			{
				PresetIndex = (byte)(rawValue >> 32) is > 0 and byte i ? i : null,
				Dpi = new((ushort)rawValue, (ushort)(rawValue >> 16))
			};

		private event Action<Driver, MouseDpiStatus>? DpiChanged;

		MouseDpiStatus IMouseDpiFeature.CurrentDpi => GetDpi(Volatile.Read(ref _currentDpi));

		event Action<Driver, MouseDpiStatus> IMouseDynamicDpiFeature.DpiChanged
		{
			add => DpiChanged += value;
			remove => DpiChanged -= value;
		}

		ImmutableArray<DotsPerInch> IMouseDpiPresetFeature.DpiPresets => ImmutableCollectionsMarshal.AsImmutableArray(Volatile.Read(ref _dpiProfiles));

		bool IMouseConfigurableDpiPresetsFeature.AllowsSeparateXYDpi => true;
		byte IMouseConfigurableDpiPresetsFeature.MaxPresetCount => 5;

		public DotsPerInch MaximumDpi => new(_maximumDpi);

		ValueTask IMouseDpiPresetFeature.ChangeCurrentPresetAsync(byte activePresetIndex, CancellationToken cancellationToken)
			=> _transport.SetDpiAsync(false, _dpiProfiles[activePresetIndex], cancellationToken);

		async ValueTask IMouseConfigurableDpiPresetsFeature.SetDpiPresetsAsync(byte activePresetIndex, ImmutableArray<DotsPerInch> dpiPresets, CancellationToken cancellationToken)
		{
			if (dpiPresets.IsDefault) throw new ArgumentNullException(nameof(dpiPresets));
			if (dpiPresets.Length is < 1 or > 5) throw new ArgumentException();

			await _transport.SetDpiProfilesAsync
			(
				true,
				new RazerMouseDpiProfileConfiguration(activePresetIndex, ImmutableArray.CreateRange(dpiPresets, dpi => new RazerMouseDpiProfile(dpi.Horizontal, dpi.Vertical, 0))),
				cancellationToken
			).ConfigureAwait(false);
		}
	}
}
