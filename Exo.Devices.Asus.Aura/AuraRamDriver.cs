using Exo.Features.LightingFeatures;
using Exo.Lighting;
using Exo.Features;
using Exo.Discovery;
using System.Collections.Immutable;
using Exo.SystemManagementBus;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using System.Buffers;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Asus.Aura;

public partial class AuraRamDriver :
	Driver,
	IDeviceDriver<ILightingDeviceFeature>,
	ILightingControllerFeature,
	ILightingDeferredChangesFeature
{
	// A list of common addresses where the SMBus devices RAM sticks can be assigned.
	// I've put the 0x39+ addresses first here because that seems to be what is used on my computer, but this can always be changed later on.
	// AFAIK nothing should prevent us from keeping a stick at address 0x77, but we can only use that address as last fallback, if we have a single stick without address assigned.
	private static ReadOnlySpan<byte> CandidateAddresses => [0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F, 0x4F, 0x66, 0x67];

	// Define zone IDs for each memory modules.
	// These would be for the 5-LED memory modules. We'd use other IDs for other kinds of memory modules.
	private static readonly Guid[] MemoryModuleZoneIds =
	[
		new(0xB2BED1D4, 0x81AE, 0x48AE, 0x9A, 0x05, 0x09, 0xE5, 0x5C, 0x77, 0xAE, 0xBA),
		new(0xCB8A8ACC, 0x94DA, 0x4D7F, 0x89, 0xE4, 0x7A, 0x3C, 0xA4, 0x2C, 0x30, 0x50),
		new(0x0D4237BB, 0x02CC, 0x4BA3, 0xB8, 0x48, 0x67, 0x19, 0x80, 0xB1, 0xB9, 0xF8),
		new(0x3A782612, 0xF492, 0x4817, 0xA9, 0xD1, 0x85, 0x2A, 0xD9, 0x62, 0x8B, 0x09),
		new(0x477EACEE, 0xBDE8, 0x45D3, 0x9E, 0x39, 0x81, 0xDA, 0x03, 0x52, 0x36, 0x72),
		new(0xD2CC4954, 0x868F, 0x4F9F, 0xAD, 0x11, 0x94, 0xEE, 0x25, 0xD2, 0x15, 0xA9),
		new(0xBDE005C1, 0x8BC6, 0x4126, 0xBE, 0xF0, 0xB8, 0x84, 0x02, 0x71, 0xD6, 0xD5),
		new(0xA3BBB8F5, 0x16E6, 0x4AF2, 0x9B, 0x46, 0x9C, 0xEC, 0x68, 0xC2, 0x4E, 0x95),
	];

	private static ReadOnlySpan<sbyte> DefaultFrameDelays => [3, 2, 1, 0, -1, -2];

	private const byte DefaultDeviceAddress = 0x77;

	// Address used to indicate the slot ID to control for reassigning device addresses.
	private const ushort AddressMovableSlotId = 0x80F8;
	private const ushort AddressDeviceAddress = 0x80F9;
	// Address where the slot ID is supposedly readable. (This seems to correlate but it could be wrong)
	private const ushort AddressDeviceSlotId = 0x8586;

	// NB: Aura sticks are generally configured all at once, since they start mapped at the same 0x77 register.
	// We should be able to detect and map each of the discovered sticks as separate lighting zones under the same device, but we could also expose each ram stick as its own device.
	// Let's try mapping the sticks as lighting zones for now, but maybe we'll want to revisit this later on and return one driver for each stick.
	// This would, however, necessitate to rework the discovery system again, because we want to handle initialization for all sticks at once, and we probably don't want to expose a dummy parent device.
	[DiscoverySubsystem<SystemManagementBiosRamDiscoverySubsystem>]
	[RamModuleId(0x04, 0x4D, "F4-3600C18-32GTZN")]
	public static async Task<DriverCreationResult<SystemMemoryDeviceKey>?> CreateAsync
	(
		ILogger<AuraRamDriver> logger,
		ImmutableArray<SystemMemoryDeviceKey> discoveredKeys,
		ImmutableArray<MemoryModuleInformation> memoryModules,
		int totalMemoryModuleCount,
		ISystemManagementBus systemManagementBus
	)
	{
		// I have no idea how everything is laid out if there are more than 8 memory modules, as they need some addresses for the SPD stuff.
		// For up to 8 memory sticks, the RAM will use addresses 0x50-0x57 and optionally 0x30-0x37.
		// If there are more, I'd assume they would extend from there, but it would definitely cause a problem with the current code.
		// At minimum, we'd have to at least blacklist the 0x39+ addresses, but it is better to not handle this for now, as it can be a source of problems.
		if (totalMemoryModuleCount > 8) throw new InvalidOperationException("Unsupported configuration.");

		// We use 255 as a value to indicate an invalid entry in the two tables below.
		var slotIndexToIndex = new sbyte[8];
		var discoveredModules = new DiscoveredModuleDescription[memoryModules.Length];

		for (int i = 0; i < discoveredModules.Length; i++)
		{
			discoveredModules[i].Address = 255;
		}

		slotIndexToIndex.AsSpan().Fill(-1);
		for (int i = 0; i < memoryModules.Length; i++)
		{
			slotIndexToIndex[memoryModules[i].Index] = (sbyte)i;
		}

		int unmappedDeviceCount = discoveredModules.Length;

		var buffer = ArrayPool<byte>.Shared.Rent(32);
		try
		{
			await using (await systemManagementBus.AcquireMutexAsync())
			{
				int candidateIndex = 0;
				bool canHaveDeviceAtDefaultAddress = true;

				while (unmappedDeviceCount != 0 && candidateIndex < CandidateAddresses.Length)
				{
					byte candidateAddress = CandidateAddresses[candidateIndex];

					if (await DetectDevicePresenceAsync(systemManagementBus, candidateAddress))
					{
						logger.SmBusDeviceDetected(candidateAddress);
						if (await DetectAuraRamAsync(systemManagementBus, candidateAddress))
						{
							logger.SmBusAuraDeviceDetected(candidateAddress);

							byte slotIndex = await ReadByteAsync(systemManagementBus, candidateAddress, AddressDeviceSlotId);
							int moduleIndex = slotIndex < 8 ? slotIndexToIndex[slotIndex] : -1;

							if (moduleIndex < 0) throw new InvalidOperationException("Expected to read a slot index, but got something else instead.");

							logger.SmBusAuraDeviceSlotDetected(candidateAddress, slotIndex);

							await ReadModuleSettingsAsync(systemManagementBus, candidateAddress, slotIndex, discoveredModules, moduleIndex, buffer);
							unmappedDeviceCount--;
						}
					}
					else if (canHaveDeviceAtDefaultAddress)
					{
						// If we fail to find a mapped device at a candidate address, we need to check if there is a device that can be remapped.
						// The general idea is that if there is a device, it would always be remappable, so we only need to remember the status of "no device".
						if (await DetectDevicePresenceAsync(systemManagementBus, DefaultDeviceAddress) &&
							await DetectAuraRamAsync(systemManagementBus, DefaultDeviceAddress))
						{
							logger.SmBusAuraDeviceDetectedAtDefaultAddress();

							// We don't really know which modules are unmapped, so we have to try the slot IDs one by one.
							// This is only really a problem in the case where some devices were already mapped but not all of them. (e.g. if the code is interrupted in the middle of its work)
							for (int moduleIndex = 0; moduleIndex < memoryModules.Length; moduleIndex++)
							{
								if ((sbyte)discoveredModules[moduleIndex].Address > 0) continue;
								// Try to move the module. This should only do something if the module for the specified slot ID is still
								await WriteBytesAsync(systemManagementBus, DefaultDeviceAddress, AddressDeviceSlotId, memoryModules[moduleIndex].Index, candidateAddress);

								if (await DetectDevicePresenceAsync(systemManagementBus, candidateAddress))
								{
									// Generally, there should be no reason why we would detect a device after the move but fail the Aura RAM detection, but who knows.
									if (await DetectAuraRamAsync(systemManagementBus, candidateAddress))
									{
										await ReadModuleSettingsAsync(systemManagementBus, candidateAddress, memoryModules[moduleIndex].Index, discoveredModules, moduleIndex, buffer);
										unmappedDeviceCount--;
										logger.SmBusAuraDeviceRemapped(candidateAddress);
									}
									else
									{
										logger.SmBusAuraDeviceRemappingFailure(candidateAddress);
									}
									break;
								}
							}
						}
						else
						{
							logger.SmBusAuraDeviceNotDetectedAtDefaultAddress();

							// Remember that there wasn't a device at the default address so that we don't check it anymore.
							// It will be faster in case the addresses we check last are the good ones, or if the devices are mapped to an unusual address.
							canHaveDeviceAtDefaultAddress = false;
						}
					}
					candidateIndex++;
				}

				// We allow one device to be mapped on address 0x77, because that should never really be a problem (right?)
				if (unmappedDeviceCount == 1 && await DetectAuraRamAsync(systemManagementBus, DefaultDeviceAddress))
				{
					int slotIndex = await ReadByteAsync(systemManagementBus, DefaultDeviceAddress, AddressDeviceSlotId);
					int moduleIndex = slotIndex < 8 ? slotIndexToIndex[slotIndex] : -1;

					if (moduleIndex < 0) throw new InvalidOperationException("Expected to read a slot index, but got something else instead.");

					await ReadModuleSettingsAsync(systemManagementBus, DefaultDeviceAddress, memoryModules[moduleIndex].Index, discoveredModules, moduleIndex, buffer);
					unmappedDeviceCount--;
				}
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer, false);
		}

		if (unmappedDeviceCount == memoryModules.Length) return null;

		var keys = discoveredKeys;

		// If for some reason, we got some modules but not all of them, remove the unmapped modules from the list.
		// That scenario should be quite unlikely.
		if (unmappedDeviceCount > 0)
		{
			var newModules = new DiscoveredModuleDescription[discoveredModules.Length - unmappedDeviceCount];
			var newKeys = new SystemMemoryDeviceKey[newModules.Length];
			int moduleIndex = 0;
			for (int i = 0; i < discoveredModules.Length; i++)
			{
				var module = discoveredModules[i];
				if ((sbyte)module.Address > 0)
				{
					newModules[moduleIndex] = module;
					newKeys[moduleIndex] = memoryModules[i].Index;
				}
			}
			discoveredModules = newModules;
			keys = ImmutableCollectionsMarshal.AsImmutableArray(newKeys);
		}

		return new(keys, new AuraRamDriver(systemManagementBus, ImmutableCollectionsMarshal.AsImmutableArray(discoveredModules)));
	}

	// Helper to test the presence of a device.
	// NB: According to I2C detect, this method can cause problems with some devices, however we should not be scanning problematic addresses in this case.
	private static async ValueTask<bool> DetectDevicePresenceAsync(ISystemManagementBus smBus, byte address)
	{
		try
		{
			await smBus.QuickWriteAsync(address);
			return true;
		}
		catch (SystemManagementBusDeviceNotFoundException)
		{
			return false;
		}
	}

	private static async ValueTask<bool> DetectAuraRamAsync(ISystemManagementBus smBus, byte address)
	{
		for (uint i = 0; i < 32; i++)
		{
			try
			{
				if (await smBus.ReadByteAsync(address, (byte)(RepeatedSequenceStart + i)) != i)
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

	private static async ValueTask ReadModuleSettingsAsync(ISystemManagementBus smBus, byte address, byte slotIndex, DiscoveredModuleDescription[] modules, int index, byte[] buffer)
	{
		modules[index].Address = address;
		modules[index].ZoneId = MemoryModuleZoneIds[slotIndex];
		await ReadBytesAsync(smBus, address, 0x8020, buffer.AsMemory(0, 4));
		bool isDynamic = buffer[0] != 0;
		modules[index].HasExtendedColors = true;
		modules[index].ColorCount = await ReadByteAsync(smBus, address, 0x1C02);
		modules[index].Effect = isDynamic ? AuraEffect.Dynamic : (AuraEffect)buffer[1];
		modules[index].FrameDelay = (sbyte)buffer[2];
		modules[index].IsReversed = buffer[3] == 1;
		await ReadBytesAsync(smBus, address, isDynamic ? (ushort)0x8100 : (ushort)0x8160, buffer.AsMemory(0, 32));
		MemoryMarshal.Cast<byte, RgbColor>(buffer.AsSpan(0, 30)).CopyTo(modules[index].Colors);
	}

	// Helper to use for debugging and dumping an Aura device memory.
	private static async ValueTask<byte[]> DumpRegistersAsync(ISystemManagementBus smBus, byte address)
	{
		await smBus.WriteWordAsync(address, 0x00, 0x0000);
		var bytes = new byte[65536];
		for (uint i = 0; i < 65536; i++)
		{
			bytes[i] = await smBus.ReadByteAsync(address, ReadByteCommand);
		}
		return bytes;
	}

	private const byte WriteAddressCommand = 0x00;
	private const byte WriteByteCommand = 0x01;
	private const byte WriteWordCommand = 0x02;
	private const byte WriteBlockCommand = 0x03;
	private const byte ReadByteCommand = 0x81;
	private const byte ReadWordCommand = 0x82;
	private const byte RepeatedSequenceStart = 0xA0;

	// NB: No idea if endianness should be swapped on big-endian systems, but that is very unlikely to ever be a concern.
	private static ValueTask WriteRegisterAddress(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress)
		=> smBusDriver.WriteWordAsync(deviceAddress, WriteAddressCommand, BinaryPrimitives.ReverseEndianness(registerAddress));

	private static async ValueTask<byte> ReadByteAsync(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		return await smBusDriver.ReadByteAsync(deviceAddress, ReadByteCommand);
	}

	private static async ValueTask ReadBytesAsync(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress, Memory<byte> destination)
	{
		if (destination.Length is 0 or > 65536) throw new ArgumentException();

		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		for (int i = 0; i < destination.Length; i++)
		{
			byte value = await smBusDriver.ReadByteAsync(deviceAddress, ReadByteCommand);
			destination.Span[i] = value;
		}
	}

	private static async ValueTask WriteByteAsync(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress, byte value)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		await smBusDriver.WriteByteAsync(deviceAddress, WriteByteCommand, value);
	}

	private static async ValueTask WriteBytesAsync(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress, byte value1, byte value2)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		await smBusDriver.WriteByteAsync(deviceAddress, WriteByteCommand, value1);
		await smBusDriver.WriteByteAsync(deviceAddress, WriteByteCommand, value2);
	}

	private static async ValueTask WriteBytesAsync(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress, byte value1, byte value2, byte value3)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		await smBusDriver.WriteByteAsync(deviceAddress, WriteByteCommand, value1);
		await smBusDriver.WriteByteAsync(deviceAddress, WriteByteCommand, value2);
		await smBusDriver.WriteByteAsync(deviceAddress, WriteByteCommand, value3);
	}

	private static async ValueTask WriteBytesAsync(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress, byte value1, byte value2, byte value3, byte value4)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		await smBusDriver.WriteByteAsync(deviceAddress, WriteByteCommand, value1);
		await smBusDriver.WriteByteAsync(deviceAddress, WriteByteCommand, value2);
		await smBusDriver.WriteByteAsync(deviceAddress, WriteByteCommand, value3);
		await smBusDriver.WriteByteAsync(deviceAddress, WriteByteCommand, value4);
	}

	private static async ValueTask WriteBytesAsync(ISystemManagementBus smBusDriver, byte deviceAddress, ushort registerAddress, ReadOnlyMemory<byte> values)
	{
		await WriteRegisterAddress(smBusDriver, deviceAddress, registerAddress);
		await smBusDriver.WriteBlockAsync(deviceAddress, WriteBlockCommand, values);
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
	private readonly object _lock;
	private readonly ImmutableArray<AuraRamLightingZone> _lightingZones;
	private readonly FinalPendingChanges[] _deferredChangesBuffer;
	private readonly ReadOnlyCollection<ILightingZone> _lightingZoneCollection;
	private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;

	IReadOnlyCollection<ILightingZone> ILightingControllerFeature.LightingZones => _lightingZoneCollection;

	public override DeviceCategory DeviceCategory => DeviceCategory.Lighting;

	IDeviceFeatureCollection<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;

	private AuraRamDriver(ISystemManagementBus smBus, ImmutableArray<DiscoveredModuleDescription> modules)
		: base("Aura RAM", new("Aura", "RAM", "RAM", null))
	{
		_smBus = smBus;
		_lock = new();
		var lightingZones = new AuraRamLightingZone[modules.Length];
		for (int i = 0; i < modules.Length; i++)
		{
			ref readonly var description = ref ImmutableCollectionsMarshal.AsArray(modules)![i];
			lightingZones[i] = description.ColorCount switch
			{
				5 => new AuraRam5LightingZone(this, description),
				8 => new AuraRam8LightingZone(this, description),
				_ => throw new InvalidOperationException(),
			};
		}
		_lightingZones = ImmutableCollectionsMarshal.AsImmutableArray(lightingZones);
		_deferredChangesBuffer = new FinalPendingChanges[lightingZones.Length];
		_lightingZoneCollection = new ReadOnlyCollection<ILightingZone>(lightingZones);
		_lightingFeatures = FeatureCollection.Create<ILightingDeviceFeature, AuraRamDriver, ILightingControllerFeature, ILightingDeferredChangesFeature>(this);
	}

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

	async ValueTask ILightingDeferredChangesFeature.ApplyChangesAsync()
	{
		await using (await _smBus.AcquireMutexAsync())
		{
			var pendingChanges = _deferredChangesBuffer;
			// First pass: Transmit deferred updates to each RAM stick.
			for (int i = 0; i < _lightingZones.Length; i++)
			{
				var zone = _lightingZones[i];
				pendingChanges[i] = await zone.UploadDeferredChangesAsync();
			}
			// Second pass: Apply pending changes on each RAM stick.
			// This is done to reduce the visual delay to a minimum.
			// Updates at this point will be done with a single SMBus operation per stick. (Color updates might be a bit slower than just sending a commit flag, but that's the best we can do)
			for (int i = 0; i < _lightingZones.Length; i++)
			{
				var zone = _lightingZones[i];
				await zone.ApplyChangesAsync(pendingChanges[i]);
			}
		}
	}
}


