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
	ValueTask<RazerMouseDpiProfileConfiguration> GetDpiPresetsAsync(CancellationToken cancellationToken);
	Task SetDpiProfilesAsync(bool persist, RazerMouseDpiProfileConfiguration configuration, CancellationToken cancellationToken);

	ValueTask<byte> GetPollingFrequencyDivider(CancellationToken cancellationToken);
	Task SetPollingFrequencyDivider(byte divider, CancellationToken cancellationToken);

	ValueTask<byte> GetDeviceInformationXxxxxAsync(CancellationToken cancellationToken);
	Task SetBrightnessAsync(bool persist, byte value, CancellationToken cancellationToken);
	ValueTask<ILightingEffect?> GetSavedEffectAsync(byte flag, CancellationToken cancellationToken);
	Task SetEffectAsync(bool persist, RazerLightingEffect effect, byte colorCount, RgbColor color1, RgbColor color2, CancellationToken cancellationToken);
	ValueTask SetDynamicColorAsync(RgbColor color, CancellationToken cancellationToken);

	ValueTask<ushort> GetIdleTimerAsync(CancellationToken cancellationToken);
	Task SetIdleTimerAsync(ushort value, CancellationToken cancellationToken);

	ValueTask<byte> GetLowPowerThresholdAsync(CancellationToken cancellationToken);
	Task SetLowPowerThresholdAsync(byte value, CancellationToken cancellationToken);

	ValueTask<PairedDeviceInformation[]> GetDevicePairingInformationAsync(CancellationToken cancellationToken);
	ValueTask<PairedDeviceInformation> GetDeviceInformationAsync(CancellationToken cancellationToken);

	ValueTask<ILightingEffect?> GetSavedLegacyEffectAsync(CancellationToken cancellationToken);
	Task SetLegacyEffectAsync(RazerLegacyLightingEffect effect, byte parameter, RgbColor color1, RgbColor color2, CancellationToken cancellationToken);
	Task SetLegacyBrightnessAsync(byte value, CancellationToken cancellationToken);
	ValueTask<byte> GetLegacyBrightnessAsync(CancellationToken cancellationToken);
}
