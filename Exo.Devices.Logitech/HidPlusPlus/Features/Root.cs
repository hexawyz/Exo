namespace Exo.Devices.Logitech.HidPlusPlus.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class Root
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.Root;

	public static class GetFeature
	{
		public const byte FunctionId = 0;

		public struct Request : IMessageRequestParameters
		{
			private byte _featureId0;
			private byte _featureId1;
			public HidPlusPlusFeature FeatureId
			{
				get => (HidPlusPlusFeature)BigEndian.ReadUInt16(_featureId0);
				set => BigEndian.Write(ref _featureId0, (ushort)value);
			}
		}

		public struct Response : IMessageResponseParameters
		{
			public byte Index;
		}
	}

	public static class GetVersion
	{
		public const byte FunctionId = 1;

		public struct Request : IMessageRequestParameters
		{
			public byte Zero0;
			public byte Zero1;
			public byte Beacon;
		}

		public struct Response : IMessageResponseParameters
		{
			public byte Major;
			public byte Minor;
			public byte Beacon;
		}
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
