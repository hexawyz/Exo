namespace Exo.Devices.Logitech.HidPlusPlus.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class FeatureSet
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.FeatureSet;

	public static class GetCount
	{
		public const byte FunctionId = 0;

		public struct Response : IMessageResponseParameters
		{
			public byte Count;
		}
	}

	public static class GetFeatureId
	{
		public const byte FunctionId = 1;

		public struct Request : IMessageRequestParameters
		{
			public byte Index;
		}

		public struct Response : IMessageResponseParameters
		{
			private byte _featureId0;
			private byte _featureId1;
			public HidPlusPlusFeature FeatureId
			{
				get => (HidPlusPlusFeature)BigEndian.ReadUInt16(_featureId0);
				set => BigEndian.Write(ref _featureId0, (ushort)value);
			}

			private byte _hidPlusPlusFeatureTypes;

			public HidPlusPlusFeatureTypes FeatureType
			{
				get => (HidPlusPlusFeatureTypes)_hidPlusPlusFeatureTypes;
				set => _hidPlusPlusFeatureTypes = (byte)value;
			}

			public byte FeatureVersion;
		}
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
