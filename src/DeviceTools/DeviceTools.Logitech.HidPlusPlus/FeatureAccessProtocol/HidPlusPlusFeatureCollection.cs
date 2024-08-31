using System.Collections;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;

public sealed class HidPlusPlusFeatureCollection : IReadOnlyList<HidPlusPlusFeatureInformation>
{
	public struct Enumerator : IEnumerator<HidPlusPlusFeatureInformation>
	{
		private readonly HidPlusPlusFeatureInformation[] _featureInformations;
		private int _index;

		internal Enumerator(HidPlusPlusFeatureInformation[] featureInformations) : this() => _featureInformations = featureInformations;

		public void Dispose() { }

		public HidPlusPlusFeatureInformation Current => _featureInformations[_index];

		public bool MoveNext() => ++_index < _featureInformations.Length;
		public void Reset() => _index = -1;

		object IEnumerator.Current => Current;
	}

	private readonly HidPlusPlusFeatureInformation[] _featureInformations;
	private readonly Dictionary<HidPlusPlusFeature, byte> _featureIndexByFeature;

	public HidPlusPlusFeatureCollection(HidPlusPlusFeatureInformation[] featureInformations)
	{
		if (featureInformations is null) throw new ArgumentNullException(nameof(featureInformations));

		_featureInformations = featureInformations;
		_featureIndexByFeature = new(featureInformations.Length);
		for (int i = 0; i < featureInformations.Length; i++)
		{
			_featureIndexByFeature.Add(featureInformations[i].Feature, (byte)i);
		}
	}

	public HidPlusPlusFeatureInformation this[int index] => _featureInformations[index];

	public bool TryGetIndex(HidPlusPlusFeature feature, out byte index)
		=> _featureIndexByFeature.TryGetValue(feature, out index);

	public bool TryGetInformation(HidPlusPlusFeature feature, out HidPlusPlusFeatureInformation info)
	{
		if (_featureIndexByFeature.TryGetValue(feature, out byte index))
		{
			info = _featureInformations[index];
			return true;
		}
		else
		{
			info = default;
			return false;
		}
	}

	public int Count => _featureInformations.Length;

	public Enumerator GetEnumerator() => new(_featureInformations);
	IEnumerator<HidPlusPlusFeatureInformation> IEnumerable<HidPlusPlusFeatureInformation>.GetEnumerator() => GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
