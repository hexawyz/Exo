using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.WinUI.Helpers;
using Exo.Ui.Contracts;
using Windows.UI;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingZoneViewModel : BindableObject
{
	private readonly LightingDeviceViewModel _device;

	public ReadOnlyCollection<LightingEffectViewModel> SupportedEffects { get; }

	private LightingEffectViewModel? _currentEffect;

	private ReadOnlyCollection<PropertyViewModel> _properties;

	public Guid Id { get; }

	private bool _isModified;

	public LightingZoneViewModel(LightingDeviceViewModel device, LightingZoneInformation lightingZoneInformation)
	{
		_device = device;
		_properties = ReadOnlyCollection<PropertyViewModel>.Empty;
		Id = lightingZoneInformation.ZoneId;
		SupportedEffects = new ReadOnlyCollection<LightingEffectViewModel>(Array.ConvertAll(lightingZoneInformation.SupportedEffectTypeNames.AsMutable(), _device.LightingViewModel.GetEffect));
	}

	public string Name => _device.LightingViewModel.GetZoneName(Id);

	public LightingEffectViewModel? CurrentEffect
	{
		get => _currentEffect;
		private set
		{
			if (SetValue(ref _currentEffect, value))
			{
				Properties = value?.CreatePropertyViewModels() ?? ReadOnlyCollection<PropertyViewModel>.Empty;
				IsModified = true;
			}
		}
	}

	public ReadOnlyCollection<PropertyViewModel> Properties
	{
		get => _properties;
		private set => SetValue(ref _properties, value);
	}

	public bool IsModified
	{
		get => _isModified;
		set
		{
			if (SetValue(ref _isModified, value))
			{
				if (value)
				{
					_device.SetModified();
				}
				else
				{
					_device.UpdateModified();
				}
			}
		}
	}

	public LightingEffect? BuildEffect()
	{
		if (_currentEffect is null) return null;

		var properties = _properties;
		uint? color = null;
		uint? speed = null;
		var propertyValues = ImmutableArray.CreateBuilder<PropertyValue>(properties.Count);

		foreach (var property in properties)
		{
			if (property.Value is { } value)
			{
				if (property.Index is null)
				{
					if (property.Name == "Color")
					{
						if (property.DataType == DataType.ColorRgb24)
						{
							if (value is Color c)
							{
								color = (uint)c.ToInt() & 0xFFFFFFU;
							}
						}
						else if (property.DataType == DataType.ColorArgb32)
						{
							if (value is Color c)
							{
								color = (uint)c.ToInt();
							}
						}
					}
					else if (property.Name == "Speed" && IsUInt32Compatible(property.DataType))
					{
						speed = Convert.ToUInt32(property.Value);
					}
				}
				else
				{
					DataValue? dataValue = null;

					if (dataValue is not null)
					{
						propertyValues.Add(new() { Index = property.Index.GetValueOrDefault(), Value = dataValue });
					}
				}
			}
		}

		return new()
		{
			TypeName = _currentEffect.TypeName,
			Color = color.GetValueOrDefault(),
			Speed = speed.GetValueOrDefault(),
			ExtendedPropertyValues = propertyValues.DrainToImmutable()
		};
	}

	private static bool IsUInt32Compatible(DataType dataType)
	{
		switch (dataType)
		{
		case DataType.UInt8:
		case DataType.Int8:
		case DataType.UInt16:
		case DataType.Int16:
		case DataType.UInt32:
		case DataType.Int32:
			return true;
		default:
			return false;
		}
	}

	internal void OnChangesApplied()
	{
		IsModified = false;
	}

	public void Reset()
	{
	}
}
