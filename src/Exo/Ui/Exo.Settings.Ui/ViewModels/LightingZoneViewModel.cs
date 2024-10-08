using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using CommunityToolkit.WinUI.Helpers;
using Exo.Contracts;
using Exo.Contracts.Ui.Settings;
using Windows.UI;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingZoneViewModel : ChangeableBindableObject
{
	private static readonly Guid NotAvailableEffectId = new(0xC771A454, 0xCAE5, 0x41CF, 0x91, 0x21, 0xBE, 0xF8, 0xAD, 0xC3, 0x80, 0xED);
	private static readonly Guid DisabledEffectId = new(0x6B972C66, 0x0987, 0x4A0F, 0xA2, 0x0F, 0xCB, 0xFC, 0x1B, 0x0F, 0x3D, 0x4B);

	private readonly LightingDeviceViewModel _device;

	public ReadOnlyCollection<LightingEffectViewModel> SupportedEffects { get; }

	private LightingEffect? _initialEffect;
	private LightingEffectViewModel? _currentEffect;

	private ReadOnlyCollection<PropertyViewModel> _properties;

	public Guid Id { get; }

	private PropertyViewModel? _colorProperty;
	private PropertyViewModel? _speedProperty;
	private readonly Dictionary<uint, PropertyViewModel> _propertiesByIndex;

	public Color? Color => (_colorProperty as ScalarPropertyViewModel)?.Value as Color?;

	private int _changedPropertyCount = 0;

	private bool _isBusy;
	public bool IsNotBusy
	{
		get => !_isBusy;
		private set => SetValue(ref _isBusy, !value, ChangedProperty.IsNotBusy);
	}

	private bool _isExpanded;
	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	private readonly Commands.ResetCommand _resetCommand;
	public ICommand ResetCommand => _resetCommand;

	public string Name { get; }
	public int DisplayOrder { get; }

	public LightingZoneViewModel(LightingDeviceViewModel device, LightingZoneInformation lightingZoneInformation, string displayName, int displayOrder)
	{
		_device = device;
		_properties = ReadOnlyCollection<PropertyViewModel>.Empty;
		_propertiesByIndex = new();
		_resetCommand = new(this);
		Id = lightingZoneInformation.ZoneId;
		SupportedEffects = new ReadOnlyCollection<LightingEffectViewModel>
		(
			Array.ConvertAll
			(
				ImmutableCollectionsMarshal.AsArray(lightingZoneInformation.SupportedEffectIds)!,
				_device.LightingViewModel.GetEffect
			)
		);
		Name = displayName;
		DisplayOrder = displayOrder;
		OnEffectUpdated();
	}

	public LightingEffectViewModel? CurrentEffect
	{
		get => _currentEffect;
		private set => SetCurrentEffect(value, false);
	}

	private void SetCurrentEffect(LightingEffectViewModel? value, bool isInitialEffectUpdate)
	{
		bool wasChanged = IsChanged;
		if (SetValue(ref _currentEffect, value, ChangedProperty.CurrentEffect))
		{
			var oldProperties = Properties;
			var newProperties = value?.CreatePropertyViewModels(_device.BrightnessCapabilities) ?? ReadOnlyCollection<PropertyViewModel>.Empty;
			bool colorChanged = _colorProperty is not null;

			if (SetValue(ref _properties, newProperties, ChangedProperty.Properties))
			{
				foreach (var property in oldProperties)
				{
					property.PropertyChanged -= OnPropertyChanged;
				}

				_propertiesByIndex.Clear();

				_colorProperty = null;
				_speedProperty = null;

				foreach (var property in newProperties)
				{
					property.PropertyChanged += OnPropertyChanged;

					if (property.Index is not null)
					{
						_propertiesByIndex[property.Index.GetValueOrDefault()] = property;
					}
					else
					{
						if (property.Name == nameof(LightingEffect.Color) && property.DataType is DataType.ColorRgb24 or DataType.ColorArgb32)
						{
							_colorProperty = property;
							colorChanged = true;
						}
						else if (property.Name == nameof(LightingEffect.Speed) && property.DataType is DataType.Int32)
						{
							_speedProperty = property;
						}
					}
				}

			}
			_changedPropertyCount = 0;

			if (isInitialEffectUpdate)
			{
				AssignPropertyInitialValues();
			}

			OnChangeStateChange(wasChanged);
			if (colorChanged) NotifyPropertyChanged(ChangedProperty.Color);
		}
	}

	public ReadOnlyCollection<PropertyViewModel> Properties => _properties;

	private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender == _colorProperty && e.PropertyName == nameof(ScalarPropertyViewModel.Value))
		{
			NotifyPropertyChanged(ChangedProperty.Color);
		}

		if (e.PropertyName == nameof(PropertyViewModel.IsChanged))
		{
			bool wasChanged = IsChanged;
			bool isChanged = wasChanged;

			if (((PropertyViewModel)sender!).IsChanged)
			{
				if (_changedPropertyCount++ == 0)
				{
					isChanged = true;
				}
			}
			else
			{
				if (--_changedPropertyCount == 0)
				{
					isChanged = IsChanged;
				}
			}

			OnChangeStateChange(wasChanged, isChanged);
		}
	}

	public override bool IsChanged => _initialEffect?.EffectId != _currentEffect?.EffectId || _changedPropertyCount != 0;

	public LightingEffect? BuildEffect()
	{
		if (_currentEffect is null) return null;

		var properties = _properties;
		uint? color = null;
		uint? speed = null;
		var propertyValues = ImmutableArray.CreateBuilder<PropertyValue>(properties.Count);

		foreach (var property in properties)
		{
			if (property is ScalarPropertyViewModel scalarProperty && scalarProperty.Value is { } value && scalarProperty.Index is null)
			{
				if (scalarProperty.Name == "Color")
				{
					if (scalarProperty.DataType == DataType.ColorRgb24)
					{
						if (value is Color c)
						{
							color = (uint)c.ToInt() & 0xFFFFFFU;
						}
					}
					else if (scalarProperty.DataType == DataType.ColorArgb32)
					{
						if (value is Color c)
						{
							color = (uint)c.ToInt();
						}
					}
				}
				else if (property.Name == "Speed" && IsUInt32Compatible(property.DataType))
				{
					speed = Convert.ToUInt32(scalarProperty.Value);
				}
			}
			else
			{
				DataValue dataValue = property.GetDataValue();

				if (!dataValue.IsDefault)
				{
					propertyValues.Add(new() { Index = property.Index.GetValueOrDefault(), Value = dataValue });
				}
			}
		}

		// Quick hack to force an effect update in case the effect is "Not Applicable" (which would initially be the case for either the unified lighting zone or the other ones)
		// This can be made better, but by doing this, it should ensure that toggling from unified to non-unified lighting actually does something.
		var effectId = _currentEffect.EffectId;
		if (effectId == NotAvailableEffectId)
		{
			effectId = DisabledEffectId;
		}

		return new()
		{
			EffectId = effectId,
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

	internal void OnEffectUpdated() => OnEffectUpdated(_device.GetActiveLightingEffect(Id));

	private void OnEffectUpdated(LightingEffect? effect)
	{
		bool wasChanged = IsChanged;

		_initialEffect = effect;

		if (!wasChanged)
		{
			SetCurrentEffect(effect is null ? null : _device.LightingViewModel.GetEffect(effect.EffectId), true);
		}
		else if (_initialEffect?.EffectId == CurrentEffect?.EffectId)
		{
			if (Properties.Count > 0)
			{
				AssignPropertyInitialValues();
			}
			else
			{
				OnChangeStateChange(wasChanged);
			}
		}

		IsNotBusy = true;
	}

	private void AssignPropertyInitialValues()
	{
		var effect = _initialEffect;
		if (effect is not null)
		{
			_colorProperty?.SetInitialValue(new() { UnsignedValue = effect.Color });
			_speedProperty?.SetInitialValue(new() { UnsignedValue = effect.Speed });

			if (!effect.ExtendedPropertyValues.IsDefaultOrEmpty)
			{
				foreach (var p in effect.ExtendedPropertyValues)
				{
					// TODO: Might be good to take note of the unaffected properties and reset them too.
					if (_propertiesByIndex.TryGetValue(p.Index, out var property))
					{
						property.SetInitialValue(p.Value);
					}
				}
			}
		}
	}

	public void Reset()
	{
		if (!IsChanged)
		{
			return;
		}
		else if (_initialEffect?.EffectId != CurrentEffect?.EffectId)
		{
			SetCurrentEffect(_initialEffect is null ? null : _device.LightingViewModel.GetEffect(_initialEffect.EffectId), true);
		}
		else
		{
			AssignPropertyInitialValues();
		}
	}

	internal void OnBeforeApplyingChanges() => IsNotBusy = false;
	internal void OnAfterApplyingChangesCancellation() => IsNotBusy = true;

	protected sealed override void OnChanged(bool isChanged)
	{
		_resetCommand.OnChanged();
		base.OnChanged(isChanged);
	}

	private static class Commands
	{
		public sealed class ResetCommand : ICommand
		{
			private readonly LightingZoneViewModel _lightingZone;

			public ResetCommand(LightingZoneViewModel lightingZone) => _lightingZone = lightingZone;

			public bool CanExecute(object? parameter) => _lightingZone.IsChanged;
			public void Execute(object? parameter) => _lightingZone.Reset();

			public event EventHandler? CanExecuteChanged;

			internal void OnChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
