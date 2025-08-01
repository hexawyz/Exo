using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Metadata;
using Exo.Service;
using Windows.UI;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

internal sealed partial class LightingZoneViewModel : ChangeableBindableObject, IOrderable
{
	private static readonly Guid NotAvailableEffectId = new(0xC771A454, 0xCAE5, 0x41CF, 0x91, 0x21, 0xBE, 0xF8, 0xAD, 0xC3, 0x80, 0xED);
	private static readonly Guid DisabledEffectId = new(0x6B972C66, 0x0987, 0x4A0F, 0xA2, 0x0F, 0xCB, 0xFC, 0x1B, 0x0F, 0x3D, 0x4B);

	private readonly LightingViewModel _lightingViewModel;
	private readonly LightingDeviceViewModel? _device;
	private readonly ObservableCollection<LightingEffectViewModel> _supportedEffects;
	private readonly ReadOnlyObservableCollection<LightingEffectViewModel> _readOnlySupportedEffects;

	public ReadOnlyObservableCollection<LightingEffectViewModel> SupportedEffects => _readOnlySupportedEffects;

	private LightingEffect? _initialEffect;
	private LightingEffectViewModel? _currentEffect;

	private ReadOnlyCollection<PropertyViewModel> _properties;

	public Guid Id { get; }

	private PropertyViewModel? _colorProperty;
	private PropertyViewModel? _speedProperty;

	public Color? Color => _colorProperty is RgbColorPropertyViewModel { Value: RgbColor color } ? Windows.UI.Color.FromArgb(255, color.R, color.G, color.B) : null;

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

	// Indicate if the currently selected effect is a new one.
	// This is especially important when the current effect and the initial effect are of the same kind, but could be different.
	private bool _isNewEffect;

	private readonly Commands.ResetCommand _resetCommand;
	public ICommand ResetCommand => _resetCommand;

	public string Name { get; }
	public uint DisplayOrder { get; }
	public LightingZoneComponentType ComponentType { get; }
	public LightingZoneShape Shape { get; }

	public LightingZoneViewModel(
		LightingViewModel lightingViewModel,
		LightingDeviceViewModel? device,
		LightingZoneInformation lightingZoneInformation,
		string displayName,
		uint displayOrder,
		LightingZoneComponentType componentType,
		LightingZoneShape shape)
	{
		_lightingViewModel = lightingViewModel;
		_device = device;
		_properties = ReadOnlyCollection<PropertyViewModel>.Empty;
		_resetCommand = new(this);
		Id = lightingZoneInformation.ZoneId;
		_supportedEffects = new();
		foreach (var effectId in ImmutableCollectionsMarshal.AsArray(lightingZoneInformation.SupportedEffectTypeIds)!)
		{
			var effect = _lightingViewModel.GetEffect(effectId);
			_supportedEffects.Insert(IOrderable.FindInsertPosition(_supportedEffects, effect.DisplayOrder), effect);
		}
		_readOnlySupportedEffects = new(_supportedEffects);
		Name = displayName;
		DisplayOrder = displayOrder;
		ComponentType = componentType;
		Shape = shape;
	}

	internal void UpdateInformation(LightingZoneInformation information)
	{
		bool shouldResetInitialEffect = false;
		bool shouldResetCurrentEffect = false;
		// Similar logic to the one used for lighting zones. We do the diff while making sure to remove and insert in the proper spots.
		var effectByIndex = new Dictionary<Guid, int>();
		var newEffectIds = new List<Guid>();
		for (int i = 0; i < _supportedEffects.Count; i++)
		{
			effectByIndex.Add(_supportedEffects[i].EffectId, i);
		}

		foreach (var effectId in information.SupportedEffectTypeIds)
		{
			if (!effectByIndex.Remove(effectId)) newEffectIds.Add(effectId);
		}

		if (effectByIndex.Count > 0)
		{
			var indicesToRemove = effectByIndex.Values.ToArray();
			Array.Sort(indicesToRemove);
			for (int i = indicesToRemove.Length; --i >= 0;)
			{
				int index = indicesToRemove[i];
				var effect = _supportedEffects[i];
				_supportedEffects.RemoveAt(index);
				shouldResetInitialEffect |= _initialEffect?.EffectId == effect.EffectId;
				shouldResetCurrentEffect |= _currentEffect?.EffectId == effect.EffectId;
			}
		}

		foreach (var effectId in newEffectIds)
		{
			var effect = _lightingViewModel.GetEffect(effectId);
			_supportedEffects.Insert(IOrderable.FindInsertPosition(_supportedEffects, effect.DisplayOrder), effect);
		}

		bool wasChanged = IsChanged;
		if (shouldResetInitialEffect)
		{
			OnEffectUpdated(null);
		}
		if (shouldResetCurrentEffect)
		{
			SetCurrentEffect(_initialEffect is null ? null : _lightingViewModel.GetEffect(_initialEffect.EffectId), false, wasChanged);
		}
	}

	public override bool IsChanged => _initialEffect?.EffectId != _currentEffect?.EffectId || _isNewEffect || _changedPropertyCount != 0;

	public LightingEffect? InitialEffect => _initialEffect;

	public LightingEffectViewModel? CurrentEffect
	{
		get => _currentEffect;
		set => SetCurrentEffect(value, false, IsChanged);
	}

	private bool SetCurrentEffect(LightingEffectViewModel? value, bool isInitialEffectUpdate, bool wasChanged)
	{
		if (SetValue(ref _currentEffect, value, ChangedProperty.CurrentEffect))
		{
			var oldProperties = Properties;
			var newProperties = value?.CreatePropertyViewModels(_device?.BrightnessCapabilities) ?? ReadOnlyCollection<PropertyViewModel>.Empty;
			bool colorChanged = _colorProperty is not null;

			if (SetValue(ref _properties, newProperties, ChangedProperty.Properties))
			{
				foreach (var property in oldProperties)
				{
					property.PropertyChanged -= OnPropertyChanged;
				}

				_colorProperty = null;
				_speedProperty = null;

				for (int index = 0; index < newProperties.Count; index++)
				{
					var property = newProperties[index];
					property.PropertyChanged += OnPropertyChanged;

					if (property.Name == "Color" && property.DataType is LightingDataType.ColorRgb24 or LightingDataType.ColorArgb32)
					{
						_colorProperty = property;
						colorChanged = true;
					}
					else if (property.Name == "Speed" && property.DataType is LightingDataType.SInt32)
					{
						_speedProperty = property;
					}
				}

			}
			_changedPropertyCount = 0;

			if (isInitialEffectUpdate)
			{
				_isNewEffect = false;
				OnChangeStateChange(wasChanged);
				AssignPropertyInitialValues(false);
			}
			else
			{
				_isNewEffect = true;
				OnChangeStateChange(wasChanged);
			}

			if (colorChanged) NotifyPropertyChanged(ChangedProperty.Color);

			return true;
		}
		else if (isInitialEffectUpdate)
		{
			_isNewEffect = false;
			OnChangeStateChange(wasChanged);
			AssignPropertyInitialValues(false);
		}

		return false;
	}

	public ReadOnlyCollection<PropertyViewModel> Properties => _properties;

	private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender == _colorProperty && e.PropertyName == nameof(ScalarPropertyViewModel<byte>.Value))
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

	public LightingEffect? BuildEffect()
	{
		if (_currentEffect is null) return null;

		// Quick hack to force an effect update in case the effect is "Not Applicable" (which would initially be the case for either the unified lighting zone or the other ones)
		// This can be made better, but by doing this, it should ensure that toggling from unified to non-unified lighting actually does something.
		var effectId = _currentEffect.EffectId;
		if (effectId == NotAvailableEffectId)
		{
			effectId = DisabledEffectId;
		}

		var properties = _properties;
		if (properties.Count == 0)
		{
			return new(effectId, []);
		}
		else
		{
			using (var stream = new MemoryStream())
			{
				using (var writer = new BinaryWriter(stream))
				{
					foreach (var property in properties)
					{
						property.WriteValue(writer);
						switch (property.PaddingLength)
						{
						case 0: break;
						case 1: writer.Write((byte)0); break;
						case 2: writer.Write((ushort)0); break;
						case 3: writer.Write((byte)0); writer.Write((ushort)0); break;
						case 4: writer.Write((uint)0); break;
						case 5: writer.Write((byte)0); writer.Write((uint)0); break;
						case 6: writer.Write((ushort)0); writer.Write((uint)0); break;
						case 7: writer.Write((byte)0); writer.Write((ushort)0); writer.Write((uint)0); break;
						case 8: writer.Write((ulong)0); break;
						default: throw new InvalidOperationException("Invalid padding.");
						}
					}
				}

				return new(effectId, stream.ToArray());
			}
		}
	}

	internal void OnEffectUpdated(LightingEffect? effect)
	{
		bool wasChanged = IsChanged;

		_initialEffect = effect;

		if (!wasChanged)
		{
			SetCurrentEffect(effect is null ? null : _lightingViewModel.GetEffect(effect.EffectId), true, wasChanged);
		}
		else if (effect?.EffectId == CurrentEffect?.EffectId)
		{
			_isNewEffect = false;
			OnChangeStateChange(wasChanged);
			AssignPropertyInitialValues(false);
		}

		IsNotBusy = true;
	}

	private void AssignPropertyInitialValues(bool shouldReset)
		=> AssignPropertyInitialValues(_initialEffect, shouldReset);

	// Outside of the main use-case of loading the values from the backend, this is also used for configuration imports.
	private void AssignPropertyInitialValues(LightingEffect? effect, bool shouldReset)
	{
		if (effect is not null)
		{
			var data = effect.EffectData.AsSpan();

			foreach (var property in Properties)
			{
				int count = property.ReadInitialValue(data);
				if (count <= 0) throw new InvalidOperationException("Properties must write at least one byte.");
				count += property.PaddingLength;
				data = data[count..];
				if (shouldReset) property.Reset();
			}
		}
	}

	internal bool TrySetCurrentEffect(LightingEffect effect)
	{
		// Quick shortcut if the current effect we assign is already the initial effect.
		// In that case, we don't want the "new effect" to show up as changed, so the easiest thing to do is just to reset the lighting zone.
		if (_initialEffect is not null && _initialEffect.EffectId == effect.EffectId && _initialEffect.EffectData.SequenceEqual(effect.EffectData))
		{
			ResetChanges();
			return true;
		}

		LightingEffectViewModel? newEffect = null;
		foreach (var effectViewModel in _supportedEffects)
		{
			if (effectViewModel.EffectId == effect.EffectId)
			{
				newEffect = effectViewModel;
			}
		}

		if (newEffect is null) return false;

		// We will set the initial value of the effect to reflect the provided configuration, so the current effect must absolutely be marked as new.
		bool wasChanged = IsChanged;
		if (!SetCurrentEffect(newEffect, false, wasChanged))
		{
			_isNewEffect = true;
			OnChangeStateChange(wasChanged);
		}
		AssignPropertyInitialValues(effect, true);

		return true;
	}

	public void ResetChanges()
	{
		if (!IsChanged)
		{
			return;
		}
		else if (_initialEffect?.EffectId != CurrentEffect?.EffectId)
		{
			SetCurrentEffect(_initialEffect is null ? null : _lightingViewModel.GetEffect(_initialEffect.EffectId), true, true);
		}
		else
		{
			// Here and in other places, we do the change state check before the effect is fully reset, because properties could also trigger such changes.
			bool wasChanged = IsChanged;
			_isNewEffect = false;
			OnChangeStateChange(wasChanged);
			AssignPropertyInitialValues(true);
		}
	}

	internal void OnBeforeApplyingChanges() => IsNotBusy = false;
	internal void OnAfterApplyingChangesCancellation() => IsNotBusy = true;

	protected sealed override void OnChanged(bool isChanged)
	{
		_resetCommand.OnChanged();
		base.OnChanged(isChanged);
	}

	private static partial class Commands
	{
		[GeneratedBindableCustomProperty]
		public sealed partial class ResetCommand : ICommand
		{
			private readonly LightingZoneViewModel _lightingZone;

			public ResetCommand(LightingZoneViewModel lightingZone) => _lightingZone = lightingZone;

			public bool CanExecute(object? parameter) => _lightingZone.IsChanged;
			public void Execute(object? parameter) => _lightingZone.ResetChanges();

			public event EventHandler? CanExecuteChanged;

			internal void OnChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
