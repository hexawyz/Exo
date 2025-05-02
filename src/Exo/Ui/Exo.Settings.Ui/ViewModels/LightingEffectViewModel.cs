using System.Collections.ObjectModel;
using Exo.Lighting;

namespace Exo.Settings.Ui.ViewModels;

// ⚠️ This class can be instantiated on any thread. Further accesses should come from the UI thread.
internal sealed class LightingEffectViewModel
{
	private LightingEffectInformation _effectInformation;
	private readonly string _displayName;

	public LightingEffectViewModel(LightingEffectInformation effectInformation, string displayName)
	{
		_effectInformation = effectInformation;
		_displayName = displayName;
	}

	public Guid EffectId => _effectInformation.EffectId;

	public string DisplayName => _displayName;

	internal void OnMetadataUpdated(LightingEffectInformation effectInformation)
	{
		if (effectInformation.EffectId != _effectInformation.EffectId) throw new InvalidOperationException();

		_effectInformation = effectInformation;
	}

	public ReadOnlyCollection<PropertyViewModel> CreatePropertyViewModels(LightingDeviceBrightnessCapabilitiesViewModel? brightnessCapabilities)
	{
		var properties = _effectInformation.Properties;

		if (properties.IsDefaultOrEmpty) return ReadOnlyCollection<PropertyViewModel>.Empty;

		bool isVariableLength = false;
		var alignmentAndLengths = new (uint Align, uint Length)[properties.Length];
		for (int i = 0; i < properties.Length; i++)
		{
			var property = properties[i];
			(uint align, uint length) = property.DataType switch
			{
				LightingDataType.UInt8 or LightingDataType.SInt8 or LightingDataType.ColorGrayscale8 => (1u, 1u),
				LightingDataType.UInt16 or LightingDataType.SInt16 or LightingDataType.Float16 or LightingDataType.ColorGrayscale16 => (2u, 2u),
				LightingDataType.UInt32 or LightingDataType.UInt32 or LightingDataType.Float32 or LightingDataType.ColorRgbw32 or LightingDataType.ColorArgb32 => (4u, 4u),
				LightingDataType.UInt64 or LightingDataType.SInt64 or LightingDataType.Float64 or LightingDataType.TimeSpan or LightingDataType.DateTime => (8u, 8u),
				LightingDataType.Guid => (8u, 16u),
				LightingDataType.ColorRgb24 => (1u, 3u),
				LightingDataType.ArrayOfColorGrayscale8 => (1u, (uint)(property.ArrayLength ?? 1)),
				LightingDataType.ArrayOfColorGrayscale16 => (2u, 2 * (uint)(property.ArrayLength ?? 1)),
				LightingDataType.ArrayOfColorRgb24 => (1u, 3 * (uint)(property.ArrayLength ?? 1)),
				LightingDataType.ArrayOfColorRgbw32 or LightingDataType.ArrayOfColorArgb32 => (4u, 4 * (uint)(property.ArrayLength ?? 1)),
				_ => (0u, 0u)
			};
			alignmentAndLengths[i] = (align, length);
			if (length == 0) isVariableLength = true;
		}

		var vm = new PropertyViewModel[properties.Length];

		uint offset = 0;
		uint globalAlign = 0;
		for (int i = 0; i < properties.Length; i++)
		{
			var property = properties[i];
			uint length = alignmentAndLengths[i].Length;
			uint padding = 0;
			if (!isVariableLength)
			{
				offset += length;
				if ((uint)(i + 1) < (uint)alignmentAndLengths.Length)
				{
					uint align = alignmentAndLengths[i + 1].Align;
					padding = (uint)-(int)offset & (align - 1);
					offset += padding;
					if (align > globalAlign) globalAlign = align;
				}
				else if (globalAlign > 1)
				{
					padding = (uint)-(int)offset & (globalAlign - 1);
					offset += padding;
				}
			}
			if (property.DataType is LightingDataType.ArrayOfColorRgb24)
			{
				vm[i] = new RgbColorFixedLengthArrayPropertyViewModel(property, (int)padding);
			}
			else
			{
				vm[i] = new ScalarPropertyViewModel(property, (int)padding, brightnessCapabilities);
			}
		}

		return Array.AsReadOnly(vm);
	}
}
