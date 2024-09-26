using System.Collections.Immutable;
using Exo.ColorFormats;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer;

internal interface IRazerProtocolTransport : IDisposable
{
	ValueTask<bool> HandshakeAsync(CancellationToken cancellationToken);
	ValueTask<string> GetSerialNumberAsync(CancellationToken cancellationToken);
	ValueTask<byte> GetBatteryLevelAsync(CancellationToken cancellationToken);
	ValueTask<bool> IsConnectedToExternalPowerAsync(CancellationToken cancellationToken);

	ValueTask<DotsPerInch> GetDpiAsync(bool persisted, CancellationToken cancellationToken);
	Task SetDpiAsync(bool persist, DotsPerInch dpi, CancellationToken cancellationToken);
	ValueTask<byte> GetCurrentDpiPresetAsync(bool persisted, CancellationToken cancellationToken);
	Task SetCurrentDpiPresetAsync(bool persist, byte index, CancellationToken cancellationToken);
	ValueTask<RazerMouseDpiProfileConfiguration> GetDpiPresetsV1Async(CancellationToken cancellationToken);
	Task SetDpiPresetsV1Async(bool persist, RazerMouseDpiProfileConfiguration configuration, CancellationToken cancellationToken);
	ValueTask<RazerMouseDpiProfileConfiguration> GetDpiPresetsV2Async(CancellationToken cancellationToken);
	Task SetDpiPresetsV2Async(bool persist, RazerMouseDpiProfileConfiguration configuration, CancellationToken cancellationToken);

	ValueTask<byte> GetPollingIntervalAsync(CancellationToken cancellationToken);
	Task SetPollingIntervalAsync(byte divider, CancellationToken cancellationToken);

	ValueTask<ILightingEffect?> GetSavedEffectV1Async(CancellationToken cancellationToken);
	Task SetEffectV1Async(RazerLegacyLightingEffect effect, byte parameter, RgbColor color1, RgbColor color2, CancellationToken cancellationToken);
	Task SetBrightnessV1Async(byte value, CancellationToken cancellationToken);
	ValueTask<byte> GetBrightnessV1Async(CancellationToken cancellationToken);

	ValueTask<ImmutableArray<byte>> GetLightingZoneIdsAsync(CancellationToken cancellationToken);
	ValueTask<byte> GetBrightnessV2Async(bool persisted, byte ledId, CancellationToken cancellationToken);
	Task SetBrightnessV2Async(bool persist, byte value, CancellationToken cancellationToken);
	ValueTask<ILightingEffect?> GetSavedEffectV2Async(byte ledId, CancellationToken cancellationToken);
	Task SetEffectV2Async(bool persist, RazerLightingEffect effect, byte colorCount, RgbColor color1, RgbColor color2, CancellationToken cancellationToken);
	ValueTask SetDynamicColorAsync(RgbColor color, CancellationToken cancellationToken);

	ValueTask<ushort> GetIdleTimerAsync(CancellationToken cancellationToken);
	Task SetIdleTimerAsync(ushort value, CancellationToken cancellationToken);

	ValueTask<byte> GetLowPowerThresholdAsync(CancellationToken cancellationToken);
	Task SetLowPowerThresholdAsync(byte value, CancellationToken cancellationToken);

	ValueTask<PairedDeviceInformation[]> GetDevicePairingInformationAsync(CancellationToken cancellationToken);
	ValueTask<PairedDeviceInformation> GetDeviceInformationAsync(CancellationToken cancellationToken);
	ValueTask<Version> GetFirmwareVersionAsync(CancellationToken cancellationToken);
	ValueTask<string> GetDockSerialNumberAsync(CancellationToken cancellationToken);
	ValueTask<byte> GetSensorStateAsync(byte parameter1, byte parameter2, CancellationToken cancellationToken);
	ValueTask<byte> GetDeviceModeAsync(CancellationToken cancellationToken);
	ValueTask SetDeviceModeAsync(byte mode, CancellationToken cancellationToken);
	ValueTask SetSensorStateAsync(byte parameter1, byte parameter2, byte value, CancellationToken cancellationToken);
}
