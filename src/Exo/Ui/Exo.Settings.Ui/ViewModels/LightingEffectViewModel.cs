using System.Collections.ObjectModel;
using Exo.Contracts;
using Exo.Lighting;

namespace Exo.Settings.Ui.ViewModels;

// ⚠️ This class can be instantiated on any thread. Further accesses should come from the UI thread.
internal sealed class LightingEffectViewModel
{
	private readonly LightingEffectInformation _effectInformation;
	private readonly string _displayName;

	public LightingEffectViewModel(LightingEffectInformation effectInformation, string displayName)
	{
		_effectInformation = effectInformation;
		_displayName = displayName;
	}

	public Guid EffectId => _effectInformation.EffectId;

	public string DisplayName => _displayName;

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
				DataType.UInt8 or DataType.Int8 or DataType.ColorGrayscale8 => (1u, 1u),
				DataType.UInt16 or DataType.Int16 or DataType.Float16 or DataType.ColorGrayscale16 => (2u, 2u),
				DataType.UInt32 or DataType.UInt32 or DataType.Float32 or DataType.ColorRgbw32 or DataType.ColorArgb32 => (4u, 4u),
				DataType.UInt64 or DataType.Int64 or DataType.Float64 or DataType.TimeSpan or DataType.DateTime => (8u, 8u),
				DataType.Guid => (8u, 16u),
				DataType.ColorRgb24 => (1u, 3u),
				DataType.ArrayOfColorGrayscale8 => (1u, (uint)(property.ArrayLength ?? 1)),
				DataType.ArrayOfColorGrayscale16 => (2u, 2 * (uint)(property.ArrayLength ?? 1)),
				DataType.ArrayOfColorRgb24 => (1u, 3 * (uint)(property.ArrayLength ?? 1)),
				DataType.ArrayOfColorRgbw32 or DataType.ArrayOfColorArgb32 => (4u, 4 * (uint)(property.ArrayLength ?? 1)),
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
			if (property.DataType is DataType.ArrayOfColorRgb24)
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
