using System.Collections.ObjectModel;
using Exo.Lighting;

namespace Exo.Settings.Ui.ViewModels;

// ⚠️ This class can be instantiated on any thread. Further accesses should come from the UI thread.
internal sealed partial class LightingEffectViewModel : IOrderable
{
	private LightingEffectInformation _effectInformation;
	private readonly string _displayName;
	private readonly uint _displayOrder;

	public LightingEffectViewModel(LightingEffectInformation effectInformation, string displayName, uint displayOrder)
	{
		_effectInformation = effectInformation;
		_displayName = displayName;
		_displayOrder = displayOrder;
	}

	public Guid EffectId => _effectInformation.EffectId;

	public EffectCapabilities Capabilities => _effectInformation.Capabilities;

	public string DisplayName => _displayName;

	public uint DisplayOrder => _displayOrder;

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
			if (property.IsVariableLengthArray) isVariableLength = true;
			(uint align, uint length) = property.DataType switch
			{
				LightingDataType.UInt8 or LightingDataType.SInt8 or LightingDataType.EffectDirection1D => (1u, 1u),
				LightingDataType.UInt16 or LightingDataType.SInt16 or LightingDataType.Float16 => (2u, 2u),
				LightingDataType.UInt32 or LightingDataType.UInt32 or LightingDataType.Float32 => (4u, 4u),
				LightingDataType.UInt64 or LightingDataType.SInt64 or LightingDataType.Float64 or LightingDataType.TimeSpan or LightingDataType.DateTime => (8u, 8u),
				LightingDataType.Guid => (8u, 16u),
				LightingDataType.ColorGrayscale8 => (1u, property.MinimumElementCount),
				LightingDataType.ColorGrayscale16 => (2u, 2 * property.MinimumElementCount),
				LightingDataType.ColorRgb24 => (1u, 3 * property.MinimumElementCount),
				LightingDataType.ColorRgbw32 or LightingDataType.ColorArgb32 => (4u, 4 * property.MinimumElementCount),
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
			if (property.IsArray)
			{
				if (property.DataType == LightingDataType.ColorRgb24)
				{
					vm[i] = new RgbColorArrayPropertyViewModel(property, (int)padding);
				}
				else
				{
					throw new InvalidOperationException("Unsupported property type.");
				}
			}
			else
			{
				vm[i] = property.DataType switch
				{
					LightingDataType.UInt8 => property.EnumerationValues.IsDefaultOrEmpty ? new BytePropertyViewModel(property, (int)padding) : new ByteEnumPropertyViewModel(property, (int)padding),
					LightingDataType.SInt8 => property.EnumerationValues.IsDefaultOrEmpty ? new SBytePropertyViewModel(property, (int)padding) : new SByteEnumPropertyViewModel(property, (int)padding),
					LightingDataType.UInt16 => property.EnumerationValues.IsDefaultOrEmpty ? new UInt16PropertyViewModel(property, (int)padding) : new UInt16EnumPropertyViewModel(property, (int)padding),
					LightingDataType.SInt16 => property.EnumerationValues.IsDefaultOrEmpty ? new Int16PropertyViewModel(property, (int)padding) : new Int16EnumPropertyViewModel(property, (int)padding),
					LightingDataType.UInt32 => property.EnumerationValues.IsDefaultOrEmpty ? new UInt32PropertyViewModel(property, (int)padding) : new UInt32EnumPropertyViewModel(property, (int)padding),
					LightingDataType.SInt32 => property.EnumerationValues.IsDefaultOrEmpty ? new Int32PropertyViewModel(property, (int)padding) : new Int32EnumPropertyViewModel(property, (int)padding),
					LightingDataType.UInt64 => property.EnumerationValues.IsDefaultOrEmpty ? new UInt64PropertyViewModel(property, (int)padding) : new UInt64EnumPropertyViewModel(property, (int)padding),
					LightingDataType.SInt64 => property.EnumerationValues.IsDefaultOrEmpty ? new Int64PropertyViewModel(property, (int)padding) : new Int64EnumPropertyViewModel(property, (int)padding),
					LightingDataType.Float16 => new HalfPropertyViewModel(property, (int)padding),
					LightingDataType.Float32 => new SinglePropertyViewModel(property, (int)padding),
					LightingDataType.Float64 => new DoublePropertyViewModel(property, (int)padding),
					LightingDataType.Boolean => new BooleanPropertyViewModel(property, (int)padding),
					LightingDataType.String => new StringPropertyViewModel(property),
					LightingDataType.EffectDirection1D => new EffectDirection1DPropertyViewModel(property, (int)padding),
					LightingDataType.ColorRgb24 => new RgbColorPropertyViewModel(property, (int)padding),
					LightingDataType.ColorRgbw32 => new RgbwColorPropertyViewModel(property, (int)padding),
					LightingDataType.ColorArgb32 => new ArgbColorPropertyViewModel(property, (int)padding),
					_ => throw new InvalidOperationException("Unsupported property type."),
				};
			}
		}

		return Array.AsReadOnly(vm);
	}
}
