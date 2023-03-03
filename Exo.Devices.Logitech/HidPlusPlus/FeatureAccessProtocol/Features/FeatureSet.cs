using System.Runtime.InteropServices;

namespace Exo.Devices.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class FeatureSet
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.FeatureSet;

	public static class GetCount
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public byte Count;
		}
	}

	public static class GetFeatureId
	{
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte Index;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters, ILongMessageParameters
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
