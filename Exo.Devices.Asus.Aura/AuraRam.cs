using Exo.Features.LightingFeatures;
using Exo.Lighting.Effects;
using Exo.Lighting;
using Exo.Features;
using Exo.Discovery;
using System.Collections.Immutable;
using Exo.SystemManagementBus;

namespace Exo.Devices.Asus.Aura;

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
	ILightingZoneEffect<ColorCycleEffect>,
	ILightingZoneEffect<ColorWaveEffect>
{
	// NB: Aura sticks are generally configured all at once, since they start mapped at the same 0x77 register.
	// We should be able to detect and map each of the discovered sticks as separate lighting zones under the same device, but we could also expose each ram stick as its own device.
	// Let's try mapping the sticks as lighting zones for now, but maybe we'll want to revisit this later on and return one driver for each stick.
	// This would, however, necessitate to rework the discovery system again, because we want to handle initialization for all sticks at once, and we probably don't want to expose a dummy parent device.
	[DiscoverySubsystem<SystemManagementBiosRamDiscoverySubsystem>]
	[RamModuleId(0x04, 0x4D, "F4-3600C18-32GTZN")]
	public static async Task<DriverCreationResult<SystemMemoryDeviceKey>?> CreateAsync
	(
		ImmutableArray<SystemMemoryDeviceKey> discoveredKeys,
		ImmutableArray<MemoryModuleInformation> memoryModules,
		ISystemManagementBus systemManagementBus
	)
	{
		await using (await systemManagementBus.AcquireMutexAsync())
		{
			// First try to detect unmapped Aura RAM stick.
			if (await DetectAuraRamAsync(systemManagementBus, 0x77))
			{
				// If the RAM sticks are unmapped, we will have to map them somewhere available.
			}

			// Detect and validate mapped aura RAM sticks.
			// Previously, RAM sticks would generally be mapped at 0x70‥0x77, but from what is said around, this can change depending on availability of the addresses.
			// I know this mapping is at least supported on the Z490 VISION D, as that's what I got in the past, but it might be hard to tell on other motherboards.
			// In fact, after checking, RAM sticks now seem to be mapped from 0x72 onwards.
			// However, something seems to have happened, as there are 8 devices mapped from 0x72 to 0x79 included for only 4 sticks.
			// I'm not sure the Aura addresses that were documented there are still entirely right, though:
			// https://web.archive.org/web/20211010175028/https://gitlab.com/CalcProgrammer1/OpenRGB/-/wikis/Aura-Controller-Registers
			// * If I request the last accessed address after using RGB Fusion, I get 0x8030, so this must definitely map to something, but what?
			// * Trying to request 0x80F9 does not seem to return anything coherent for the I2C/SMBus address?

			//await DetectAuraRamAsync(systemManagementBus, 0x70);
		}
		return null;
	}

	private static async ValueTask<bool> DetectAuraRamAsync(ISystemManagementBus smBus, byte address)
	{
		for (uint i = 0; i < 32; i++)
		{
			try
			{
				if (await smBus.ReadByteAsync(address, (byte)(RepeatedSequenceStart + i)).ConfigureAwait(false) != i)
				{
					return false;
				}
			}
			catch (SystemManagementBusDeviceNotFoundException)
			{
				return false;
			}
		}
		return true;
	}

	private const byte WriteAddressCommand = 0x00;
	private const byte ReadWriteByteCommand = 0x01;
	private const byte WriteWordCommand = 0x02;
	private const byte ReadWordCommand = 0x82;
	private const byte RepeatedSequenceStart = 0xA0;

	private static ValueTask WriteRegisterAddress(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress)
		=> smBusDriver.WriteWordAsync(deviceAddress, WriteAddressCommand, registerAddress);

	private static async ValueTask<byte> ReadByteAsync(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		return await smBusDriver.ReadByteAsync(deviceAddress, ReadWriteByteCommand);
	}

	private static async ValueTask WriteByteAsync(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress, byte value)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		await smBusDriver.WriteByteAsync(deviceAddress, ReadWriteByteCommand, value);
	}

	private static async ValueTask<ushort> ReadWordAsync(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		return await smBusDriver.ReadByteAsync(deviceAddress, ReadWordCommand);
	}

	private static async ValueTask WriteWordAsync(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress, ushort value)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		await smBusDriver.WriteWordAsync(deviceAddress, WriteWordCommand, value);
	}

	private readonly ISystemManagementBus _smBus;
	private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

	public override DeviceCategory DeviceCategory => DeviceCategory.Lighting;

	public AuraRamDriver(string friendlyName, DeviceConfigurationKey configurationKey, ISystemManagementBus smBus)
		: base("Aura RAM", default)
	{
		_smBus = smBus;
		_lightingFeatures = FeatureCollection.Create<ILightingDeviceFeature, AuraRamDriver, ILightingControllerFeature, IUnifiedLightingFeature>(this);
		_allFeatures = FeatureCollection.Create<IDeviceFeature, AuraRamDriver, ILightingControllerFeature, IUnifiedLightingFeature>(this);
	}

	public override ValueTask DisposeAsync() => throw new NotImplementedException();

	IDeviceFeatureCollection<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;

	public IReadOnlyCollection<ILightingZone> LightingZones { get; }

	public bool IsUnifiedLightingEnabled { get; }
	public Guid ZoneId { get; }

	public ValueTask ApplyChangesAsync() => throw new NotImplementedException();

	public ILightingEffect GetCurrentEffect() => throw new NotImplementedException();
	public void ApplyEffect(in DisabledEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out DisabledEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(in StaticColorEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out StaticColorEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(in ColorPulseEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out ColorPulseEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(in ColorFlashEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out ColorFlashEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(in ColorDoubleFlashEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out ColorDoubleFlashEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(in ColorCycleEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out ColorCycleEffect effect) => throw new NotImplementedException();
	public void ApplyEffect(in ColorWaveEffect effect) => throw new NotImplementedException();
	public bool TryGetCurrentEffect(out ColorWaveEffect effect) => throw new NotImplementedException();
}
