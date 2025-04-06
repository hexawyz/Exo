using System.Collections.ObjectModel;
using Exo.Contracts;

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
		var lengths = new uint[properties.Length];
		for (int i = 0; i < properties.Length; i++)
		{
			uint length = properties[i].DataType switch
			{
				DataType.UInt8 or DataType.Int8 or DataType.ColorGrayscale8 => 1,
				DataType.UInt16 or DataType.Int16 or DataType.Float16 or DataType.ColorGrayscale16 => 2,
				DataType.UInt32 or DataType.UInt32 or DataType.Float32 or DataType.ColorRgbw32 or DataType.ColorArgb32 => 4,
				DataType.UInt64 or DataType.Int64 or DataType.Float64 or DataType.TimeSpan or DataType.DateTime => 8,
				DataType.Guid => 16,
				_ => 0
			};
			lengths[i] = length;
			if (length == 0) isVariableLength = true;
		}

		var vm = new PropertyViewModel[properties.Length];

		uint offset = 0;
		for (int i = 0; i < properties.Length; i++)
		{
			var property = properties[i];
			uint length = lengths[i];
			uint padding = 0;
			if (!isVariableLength)
			{
				offset += length;
				if ((uint)(i + 1) < (uint)lengths.Length)
				{
					uint align = lengths[i + 1];
					if (align == 16) align = 8;
					padding = (uint)-(int)offset & (align - 1);
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
