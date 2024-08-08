using Exo.ColorFormats;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer;

internal interface IRazerProtocolTransport : IDisposable
{
	ValueTask<byte> GetBatteryLevelAsync(CancellationToken cancellationToken);
	ValueTask<PairedDeviceInformation> GetDeviceInformationAsync(CancellationToken cancellationToken);
	ValueTask<byte> GetDeviceInformationXxxxxAsync(CancellationToken cancellationToken);
	ValueTask<PairedDeviceInformation[]> GetDevicePairingInformationAsync(CancellationToken cancellationToken);
	ValueTask<DotsPerInch> GetDpiAsync(CancellationToken cancellationToken);
	ValueTask<RazerMouseDpiProfileStatus> GetDpiProfilesAsync(bool persisted, CancellationToken cancellationToken);
	ValueTask<ILightingEffect?> GetSavedEffectAsync(byte flag, CancellationToken cancellationToken);
	ValueTask<string> GetSerialNumberAsync(CancellationToken cancellationToken);
	ValueTask<bool> HandshakeAsync(CancellationToken cancellationToken);
	ValueTask<bool> IsConnectedToExternalPowerAsync(CancellationToken cancellationToken);
	ValueTask SetBrightnessAsync(bool persist, byte value, CancellationToken cancellationToken);
	ValueTask SetDpiAsync(DotsPerInch dpi, CancellationToken cancellationToken);
	ValueTask SetDynamicColorAsync(RgbColor color, CancellationToken cancellationToken);
	ValueTask SetEffectAsync(bool persist, RazerLightingEffect effect, byte colorCount, RgbColor color1, RgbColor color2, CancellationToken cancellationToken);
}
