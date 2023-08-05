using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

// ⚠️ This class can be instantiated on any thread. Further accesses should come from the UI thread.
internal sealed class LightingEffectViewModel
{
	private readonly LightingEffectInformation _effectInformation;

	public LightingEffectViewModel(LightingEffectInformation effectInformation)
	{
		_effectInformation = effectInformation;
	}

	public Guid EffectId => _effectInformation.EffectId;

	public string DisplayName => EffectDatabase.GetEffectDisplayName(EffectId) ?? string.Create(CultureInfo.InvariantCulture, $"Effect {EffectId:B}.");

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
