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
	ValueTask SetDpiAsync(bool persist, DotsPerInch dpi, CancellationToken cancellationToken);
	ValueTask<RazerMouseDpiProfileConfiguration> GetDpiProfilesAsync(CancellationToken cancellationToken);
	ValueTask SetDpiProfilesAsync(RazerMouseDpiProfileConfiguration configuration, CancellationToken cancellationToken);

	ValueTask<byte> GetPollingFrequencyDivider(CancellationToken cancellationToken);
	ValueTask SetPollingFrequencyDivider(byte divider, CancellationToken cancellationToken);

	ValueTask<byte> GetDeviceInformationXxxxxAsync(CancellationToken cancellationToken);
	ValueTask SetBrightnessAsync(bool persist, byte value, CancellationToken cancellationToken);
	ValueTask<ILightingEffect?> GetSavedEffectAsync(byte flag, CancellationToken cancellationToken);
	ValueTask SetEffectAsync(bool persist, RazerLightingEffect effect, byte colorCount, RgbColor color1, RgbColor color2, CancellationToken cancellationToken);
	ValueTask SetDynamicColorAsync(RgbColor color, CancellationToken cancellationToken);

	ValueTask<PairedDeviceInformation[]> GetDevicePairingInformationAsync(CancellationToken cancellationToken);
	ValueTask<PairedDeviceInformation> GetDeviceInformationAsync(CancellationToken cancellationToken);
}
