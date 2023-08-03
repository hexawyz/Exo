using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingEffectViewModel
{
	private readonly LightingEffectInformation _effectInformation;

	public LightingEffectViewModel(LightingEffectInformation effectInformation)
	{
		_effectInformation = effectInformation;
	}

	public Guid EffectId => _effectInformation.EffectId;

	public string DisplayName => EffectDatabase.GetEffectName(EffectId);

	public ReadOnlyCollection<PropertyViewModel> CreatePropertyViewModels()
	{
		var properties = _effectInformation.Properties;

		if (properties.IsDefaultOrEmpty) return ReadOnlyCollection<PropertyViewModel>.Empty;

		var vm = new PropertyViewModel[properties.Length];

		for (int i = 0; i < properties.Length; i++)
		{
			vm[i] = new(properties[i]);
		}

		return Array.AsReadOnly(vm);
	}
}
