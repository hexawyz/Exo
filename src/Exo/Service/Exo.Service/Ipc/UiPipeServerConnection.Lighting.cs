using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;
using Exo.Primitives;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchLightingDevicesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<LightingDeviceInformation>.CreateAsync(_lightingService, cancellationToken))
		{
			try
			{
				await WriteInitialDataAsync(watcher, cancellationToken).ConfigureAwait(false);
				await WriteConsumedDataAsync(watcher, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
		}

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<LightingDeviceInformation> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var effectInformation in initialData)
					{
						int length = Write(buffer.Span, effectInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<LightingDeviceInformation> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var effectInformation))
					{
						int length = Write(buffer.Span, effectInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int Write(Span<byte> buffer, in LightingDeviceInformation device)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.LightingDevice);
			writer.Write(device.DeviceId);
			LightingDeviceFlags flags = LightingDeviceFlags.None;
			if (device.PersistenceMode != Lighting.LightingPersistenceMode.NeverPersisted)
			{
				flags |= LightingDeviceFlags.CanPersist;
				if (device.PersistenceMode == Lighting.LightingPersistenceMode.AlwaysPersisted) flags |= LightingDeviceFlags.AlwaysPersisted;
			}
			if (device.BrightnessCapabilities is not null) flags |= LightingDeviceFlags.HasBrightness;
			if (device.PaletteCapabilities is not null) flags |= LightingDeviceFlags.HasPalette;
			if (device.UnifiedLightingZone is not null) flags |= LightingDeviceFlags.HasUnifiedLighting;
			writer.Write((byte)flags);
			if (device.BrightnessCapabilities is not null)
			{
				writer.Write(device.BrightnessCapabilities.GetValueOrDefault().MinimumValue);
				writer.Write(device.BrightnessCapabilities.GetValueOrDefault().MaximumValue);
			}
			if (device.PaletteCapabilities is not null)
			{
				writer.Write(device.PaletteCapabilities.GetValueOrDefault().ColorCount);
			}
			if (device.UnifiedLightingZone is not null)
			{
				WriteLightingZone(ref writer, device.UnifiedLightingZone.GetValueOrDefault());
			}
			if (device.LightingZones.IsDefaultOrEmpty)
			{
				writer.Write((byte)0);
			}
			else
			{
				writer.WriteVariable((uint)device.LightingZones.Length);
				foreach (var lightingZone in device.LightingZones)
				{
					WriteLightingZone(ref writer, lightingZone);
				}
			}

			return (int)writer.Length;
		}

		static void WriteLightingZone(ref BufferWriter writer, LightingZoneInformation lightingZone)
		{
			writer.Write(lightingZone.ZoneId);
			if (lightingZone.SupportedEffectTypeIds.IsDefaultOrEmpty)
			{
				writer.Write((byte)0);
			}
			else
			{
				writer.WriteVariable((uint)lightingZone.SupportedEffectTypeIds.Length);
				foreach (var effectId in lightingZone.SupportedEffectTypeIds)
				{
					writer.Write(effectId);
				}
			}
		}
	}

	private async Task WatchLightingDeviceConfigurationAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<LightingDeviceConfiguration>.CreateAsync(_lightingService, cancellationToken).ConfigureAwait(false))
		{
			try
			{
				await WriteInitialDataAsync(watcher, cancellationToken).ConfigureAwait(false);
				await WriteConsumedDataAsync(watcher, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
		}

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<LightingDeviceConfiguration> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var effectInformation in initialData)
					{
						int length = Write(buffer.Span, effectInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<LightingDeviceConfiguration> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var effectInformation))
					{
						int length = Write(buffer.Span, effectInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int Write(Span<byte> buffer, in LightingDeviceConfiguration device)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.LightingDeviceConfiguration);
			writer.Write(device.DeviceId);
			LightingDeviceConfigurationFlags flags = LightingDeviceConfigurationFlags.None;
			if (device.IsUnifiedLightingEnabled) flags |= LightingDeviceConfigurationFlags.IsUnified;
			if (device.BrightnessLevel is not null) flags |= LightingDeviceConfigurationFlags.HasBrightness;
			if (!device.PaletteColors.IsDefaultOrEmpty) flags |= LightingDeviceConfigurationFlags.HasPalette;
			writer.Write((byte)flags);
			if (device.BrightnessLevel is not null) writer.Write(device.BrightnessLevel.GetValueOrDefault());
			if (!device.PaletteColors.IsDefaultOrEmpty)
			{
				writer.WriteVariable((uint)device.PaletteColors.Length);
				foreach (var color in device.PaletteColors)
				{
					writer.Write(color.R);
					writer.Write(color.G);
					writer.Write(color.B);
				}
			}
			if (device.ZoneEffects.IsDefaultOrEmpty)
			{
				writer.Write((byte)0);
			}
			else
			{
				writer.WriteVariable((uint)device.ZoneEffects.Length);
				foreach (var lightingZoneEffect in device.ZoneEffects)
				{
					WriteLightingZoneEffect(ref writer, in lightingZoneEffect);
				}
			}

			return (int)writer.Length;
		}

		static void WriteLightingZoneEffect(ref BufferWriter writer, in LightingZoneEffect lightingZoneEffect)
		{
			writer.Write(lightingZoneEffect.ZoneId);
			if (lightingZoneEffect.Effect is { } effect)
			{
				writer.Write(effect.EffectId);
				writer.WriteVariable((uint)effect.EffectData.Length);
				writer.Write(effect.EffectData);
			}
			else
			{
				writer.Write(Guid.Empty);
				writer.Write((byte)0);
			}
		}
	}

	private async Task WatchLightingEffectsAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<LightingEffectInformation>.CreateAsync(_lightingEffectMetadataService, cancellationToken).ConfigureAwait(false))
		{
			try
			{
				await WriteInitialDataAsync(watcher, cancellationToken).ConfigureAwait(false);
				await WriteConsumedDataAsync(watcher, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
		}

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<LightingEffectInformation> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var effectInformation in initialData)
					{
						int length = Write(buffer.Span, effectInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<LightingEffectInformation> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var effectInformation))
					{
						int length = Write(buffer.Span, effectInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int Write(Span<byte> buffer, in LightingEffectInformation effect)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.LightingEffect);
			writer.Write(effect.EffectId);
			if (effect.Properties.IsDefaultOrEmpty)
			{
				writer.Write((byte)0);
			}
			else
			{
				writer.WriteVariable((uint)effect.Properties.Length);
				foreach (var property in effect.Properties)
				{
					WriteProperty(ref writer, property);
				}
			}
			return (int)writer.Length;
		}

		static void WriteProperty(ref BufferWriter writer, in ConfigurablePropertyInformation property)
		{
			writer.WriteVariableString(property.Name);
			writer.WriteVariableString(property.DisplayName);
			writer.Write((byte)property.DataType);
			var flags = LightingEffectFlags.None;
			if (property.DefaultValue is not null) flags |= LightingEffectFlags.DefaultValue;
			if (property.MinimumValue is not null) flags |= LightingEffectFlags.MinimumValue;
			if (property.MaximumValue is not null) flags |= LightingEffectFlags.MaximumValue;
			if (!property.EnumerationValues.IsDefaultOrEmpty) flags |= LightingEffectFlags.Enum;
			if (property.ArrayLength is not null) flags |= LightingEffectFlags.Array;
			writer.Write((byte)flags);
			if (property.DefaultValue is not null) WriteValue(ref writer, property.DataType, property.DefaultValue);
			if (property.MinimumValue is not null) WriteValue(ref writer, property.DataType, property.MinimumValue);
			if (property.MaximumValue is not null) WriteValue(ref writer, property.DataType, property.MaximumValue);
			if (!property.EnumerationValues.IsDefaultOrEmpty)
			{
				writer.WriteVariable((uint)property.EnumerationValues.Length);
				foreach (var enumerationValue in property.EnumerationValues)
				{
					WriteConstantValue(ref writer, property.DataType, enumerationValue.Value);
					writer.WriteVariableString(enumerationValue.DisplayName);
					writer.WriteVariableString(enumerationValue.Description);
				}
			}
			if (property.ArrayLength is not null) writer.WriteVariable((uint)property.ArrayLength.GetValueOrDefault());
		}

		static void WriteValue(ref BufferWriter writer, LightingDataType dataType, object value)
		{
			switch (dataType)
			{
			case LightingDataType.UInt8:
			case LightingDataType.ColorGrayscale8:
				writer.Write((byte)value);
				break;
			case LightingDataType.SInt8:
				writer.Write((byte)value);
				break;
			case LightingDataType.UInt16:
				writer.Write((ushort)value);
				break;
			case LightingDataType.SInt16:
				writer.Write((short)value);
				break;
			case LightingDataType.UInt32:
			case LightingDataType.ColorRgbw32:
			case LightingDataType.ColorArgb32:
				writer.Write((uint)value);
				break;
			case LightingDataType.SInt32:
				writer.Write((int)value);
				break;
			case LightingDataType.UInt64:
				writer.Write((ulong)value);
				break;
			case LightingDataType.SInt64:
				writer.Write((long)value);
				break;
			case LightingDataType.Float16:
				writer.Write((Half)value);
				break;
			case LightingDataType.Float32:
				writer.Write((float)value);
				break;
			case LightingDataType.Float64:
				writer.Write((double)value);
				break;
			case LightingDataType.Boolean:
				writer.Write((bool)value);
				break;
			case LightingDataType.Guid:
				writer.Write((Guid)value);
				break;
			case LightingDataType.EffectDirection1D:
				writer.Write((byte)(EffectDirection1D)value);
				break;
			case LightingDataType.ColorRgb24:
				Serializer.Write(ref writer, (RgbColor)value);
				break;
			default:
				throw new InvalidOperationException($"Type not supported: {dataType}.");
			}
		}

		static void WriteConstantValue(ref BufferWriter writer, LightingDataType dataType, ulong value)
		{
			switch (dataType)
			{
			case LightingDataType.UInt8:
				writer.Write((byte)value);
				break;
			case LightingDataType.SInt8:
				writer.Write((byte)value);
				break;
			case LightingDataType.UInt16:
				writer.Write((ushort)value);
				break;
			case LightingDataType.SInt16:
				writer.Write((short)value);
				break;
			case LightingDataType.UInt32:
				writer.Write((uint)value);
				break;
			case LightingDataType.SInt32:
				writer.Write((int)value);
				break;
			case LightingDataType.UInt64:
				writer.Write(value);
				break;
			case LightingDataType.SInt64:
				writer.Write(value);
				break;
			default:
				throw new InvalidOperationException($"Type not supported: {dataType}.");
			}
		}
	}

	private void ProcessLightingDeviceConfiguration(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		uint requestId = reader.ReadVariableUInt32();
		var deviceId = reader.ReadGuid();
		var flags = (LightingDeviceConfigurationFlags)reader.ReadByte();
		byte brightness = 0;
		bool hasChanged = false;
		try
		{
			if ((flags & LightingDeviceConfigurationFlags.HasBrightness) != 0)
			{
				brightness = reader.ReadByte();
				try
				{
					_lightingService.SetBrightness(deviceId, brightness);
					hasChanged = true;
				}
				catch (DeviceNotFoundException)
				{
					WriteLightingDeviceDeviceConfigurationStatus(requestId, LightingDeviceOperationStatus.DeviceNotFound, cancellationToken);
					return;
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					return;
				}
				catch
				{
					WriteLightingDeviceDeviceConfigurationStatus(requestId, LightingDeviceOperationStatus.Error, cancellationToken);
					return;
				}
			}
			uint zoneCount = reader.ReadVariableUInt32();
			for (uint i = 0; i < zoneCount; i++)
			{
				var zoneId = reader.ReadGuid();
				var effectId = reader.ReadGuid();
				uint dataLength = reader.ReadVariableUInt32();
				try
				{
					_lightingService.SetEffect(deviceId, zoneId, effectId, reader.UnsafeReadSpan(dataLength));
					hasChanged = true;
				}
				catch (DeviceNotFoundException)
				{
					WriteLightingDeviceDeviceConfigurationStatus(requestId, LightingDeviceOperationStatus.DeviceNotFound, cancellationToken);
					return;
				}
				catch (LightingZoneNotFoundException)
				{
					WriteLightingDeviceDeviceConfigurationStatus(requestId, LightingDeviceOperationStatus.ZoneNotFound, cancellationToken);
					return;
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					return;
				}
				catch
				{
					WriteLightingDeviceDeviceConfigurationStatus(requestId, LightingDeviceOperationStatus.Error, cancellationToken);
					return;
				}
			}
		}
		finally
		{
			// NB: Maybe not the best place to put this, but at least the easiest.
			// This will allow configuration updates to be pushed before the changes are applied onto the device, however it might be acceptable still.
			// TODO: Refactor this by merging this whole method within the lighting service ?
			if (hasChanged) _lightingService.NotifyDeviceConfiguration(deviceId);
		}
		ApplyLightingChanges(requestId, deviceId, (flags & LightingDeviceConfigurationFlags.Persist) != 0, cancellationToken);
	}

	private async void ApplyLightingChanges(uint requestId, Guid deviceId, bool shouldPersist, CancellationToken cancellationToken)
	{
		try
		{
			await ApplyLightingChangesAsync(requestId, deviceId, shouldPersist, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
		}
	}

	private async ValueTask ApplyLightingChangesAsync(uint requestId, Guid deviceId, bool shouldPersist, CancellationToken cancellationToken)
	{
		try
		{
			await _lightingService.ApplyChangesAsync(deviceId, shouldPersist).ConfigureAwait(false);
		}
		catch (DeviceNotFoundException)
		{
			WriteLightingDeviceDeviceConfigurationStatus(requestId, LightingDeviceOperationStatus.DeviceNotFound, cancellationToken);
			return;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return;
		}
		catch
		{
			WriteLightingDeviceDeviceConfigurationStatus(requestId, LightingDeviceOperationStatus.Error, cancellationToken);
			return;
		}
		WriteLightingDeviceDeviceConfigurationStatus(requestId, LightingDeviceOperationStatus.Success, cancellationToken);
	}

	private void WriteLightingDeviceDeviceConfigurationStatus(uint requestId, LightingDeviceOperationStatus status, CancellationToken cancellationToken)
		=> WriteSimpleOperationStatus(ExoUiProtocolServerMessage.LightingDeviceOperationStatus, requestId, (byte)status, cancellationToken);
}
