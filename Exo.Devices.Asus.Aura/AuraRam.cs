using Exo.Features.LightingFeatures;
using Exo.Lighting.Effects;
using Exo.Lighting;

namespace Exo.Devices.Asus.Aura;

[RamStick(0x04, 0x8D, "F4-3600C18-32GTZN")]
public class AuraRamDriver :
	Driver,
	IDeviceDriver<ILightingDeviceFeature>,
	ILightingControllerFeature,
	IUnifiedLightingFeature,
	ILightingZoneEffect<DisabledEffect>,
	ILightingZoneEffect<StaticColorEffect>,
	ILightingZoneEffect<ColorPulseEffect>,
	ILightingZoneEffect<ColorFlashEffect>,
	ILightingZoneEffect<ColorDoubleFlashEffect>,
	ILightingZoneEffect<RainbowCycleEffect>,
	ILightingZoneEffect<RainbowWaveEffect>
{
	// TODO: How should we handle the RAM ?
	// Aura sticks are generally configured all at once, since they start mapped at the same 0x77 register.
	// Either we expose all the sticks as a single device, or we map each stick individually. But if we want to map sticks individually, the simple way CreateAsync() will not do.
	// => It might be better to expose the driver as a generic "hub" device, mapping each RAM stick as a child driver of the main Aura driver ?
	// In the case of SMBus, we also need some detection protocol. SMBus device discovery is theoretically possible, but it does not seem realistic to count on it.
	// For the RAM sticks, I'm thinking we should be able to read meaningful-enough information from the SMBIOS tables. If not, we'd have to read SPD data from the SMBus…
	public async Task<AuraRamDriver> CreateAsync(ISmBusDriver smBusDriver)
	{
		return null;
	}

	private const byte WriteAddressCommand = 0x00;
	private const byte ReadWriteByteCommand = 0x01;
	private const byte WriteWordCommand = 0x02;
	private const byte ReadWordCommand = 0x82;
	private const byte RepeatedSequenceStart = 0xA0;

	private static ValueTask WriteRegisterAddress(ISmBusDriver smBusDriver, byte deviceAddress, ushort registerAddress)
		=> smBusDriver.WriteWordAsync(deviceAddress, WriteAddressCommand, registerAddress);

	private static async ValueTask<byte> ReadByteAsync(ISmBusDriver smBusDriver, byte deviceAddress, ushort registerAddress)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		return await smBusDriver.ReadByteAsync(deviceAddress, ReadWriteByteCommand);
	}

	private static async ValueTask WriteByteAsync(ISmBusDriver smBusDriver, byte deviceAddress, ushort registerAddress, byte value)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		await smBusDriver.WriteByteAsync(deviceAddress, ReadWriteByteCommand, value);
	}

	private static async ValueTask<ushort> ReadWordAsync(ISmBusDriver smBusDriver, byte deviceAddress, ushort registerAddress)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		return await smBusDriver.ReadByteAsync(deviceAddress, ReadWordCommand);
	}

	private static async ValueTask WriteWordAsync(ISmBusDriver smBusDriver, byte deviceAddress, ushort registerAddress, ushort value)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		await smBusDriver.WriteWordAsync(deviceAddress, WriteWordCommand, value);
	}

	private readonly ISmBusDriver _smBusDriver;
	private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

	public AuraRamDriver(string friendlyName, DeviceConfigurationKey configurationKey, ISmBusDriver smBusDriver)
		: base("Aura RAM", default)
	{
		_smBusDriver = smBusDriver;
		_lightingFeatures = FeatureCollection.Create<ILightingDeviceFeature, AuraRamDriver, ILightingControllerFeature, IUnifiedLightingFeature>(this);
		_allFeatures = FeatureCollection.Create<IDeviceFeature, AuraRamDriver, ILightingControllerFeature, IUnifiedLightingFeature>(this);
	}

	public override ValueTask DisposeAsync() => throw new NotImplementedException();

	IDeviceFeatureCollection<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;
	public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

	public IReadOnlyCollection<ILightingZone> LightingZones { get; }

	public void ApplyChanges() => throw new NotImplementedException();

	public bool IsUnifiedLightingEnabled { get; }
	public Guid ZoneId { get; }

	public ILightingEffect GetCurrentEffect() => throw new NotImplementedException();
	public void ApplyEffect(DisabledEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out DisabledEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(StaticColorEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out StaticColorEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(ColorPulseEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out ColorPulseEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(ColorFlashEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out ColorFlashEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(ColorDoubleFlashEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out ColorDoubleFlashEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(RainbowCycleEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out RainbowCycleEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(RainbowWaveEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out RainbowWaveEffect effect) => throw new NotImplementedException();
}
