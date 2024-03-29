using System.Collections.ObjectModel;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;

/// <summary>Provides helpers to work with the feature access protocol over <see cref="HidPlusPlusTransport"/>.</summary>
public static class HidPlusPlusTransportFeatureAccessProtocolExtensions
{
	public static async Task<HidPlusPlusVersion> GetProtocolVersionAsync(this HidPlusPlusTransport transport, byte deviceId, CancellationToken cancellationToken)
	{
		// This an arbitrarily chosen value to validate the (ping) response.
		// Logitech Options and G-Hub use 0x90 here.
		const byte Sentinel = 0xA5;

		var getVersionResponse = await transport.FeatureAccessSendAsync<Root.GetVersion.Request, Root.GetVersion.Response>
		(
			deviceId,
			0,
			Root.GetVersion.FunctionId,
			new Root.GetVersion.Request { Sentinel = Sentinel },
			cancellationToken
		).ConfigureAwait(false);

		if (getVersionResponse.Beacon != Sentinel)
			throw new Exception("Received an invalid response.");

		return new HidPlusPlusVersion(getVersionResponse.Major, getVersionResponse.Minor);
	}

	public static async Task<HidPlusPlusVersion> GetProtocolVersionWithRetryAsync(this HidPlusPlusTransport transport, byte deviceId, int retryCount, CancellationToken cancellationToken)
	{
		// This an arbitrarily chosen value to validate the (ping) response.
		// Logitech Options and G-Hub use 0x90 here.
		const byte Sentinel = 0xA5;

		var getVersionResponse = await transport.FeatureAccessSendWithRetryAsync<Root.GetVersion.Request, Root.GetVersion.Response>
		(
			deviceId,
			0,
			Root.GetVersion.FunctionId,
			new Root.GetVersion.Request { Sentinel = Sentinel },
			retryCount,
			cancellationToken
		).ConfigureAwait(false);

		if (getVersionResponse.Beacon != Sentinel)
			throw new Exception("Received an invalid response.");

		return new HidPlusPlusVersion(getVersionResponse.Major, getVersionResponse.Minor);
	}

	public static async Task<ReadOnlyDictionary<HidPlusPlusFeature, byte>> GetFeaturesAsync(this HidPlusPlusTransport transport, byte deviceId, CancellationToken cancellationToken)
	{
		var getCountResponse = await transport.FeatureAccessSendAsync<FeatureSet.GetCount.Response>(deviceId, 1, FeatureSet.GetCount.FunctionId, cancellationToken);

		if (getCountResponse.Count == 0)
			throw new InvalidOperationException();

		int count = getCountResponse.Count + 1;

		var features = new Dictionary<HidPlusPlusFeature, byte>(count)
		{
			{ HidPlusPlusFeature.Root, 0 }
		};

		for (int i = 1; i < count; i++)
		{
			var getFeatureIdResponse = await transport.FeatureAccessSendAsync<FeatureSet.GetFeatureId.Request, FeatureSet.GetFeatureId.Response>
			(
				deviceId,
				1,
				1,
				new FeatureSet.GetFeatureId.Request { Index = (byte)i },
				cancellationToken
			);

			features.Add(getFeatureIdResponse.FeatureId, (byte)i);
		}

		var readOnlyFeatures = new ReadOnlyDictionary<HidPlusPlusFeature, byte>(features);

		return readOnlyFeatures;
	}

	public static async Task<HidPlusPlusFeatureCollection> GetFeaturesWithRetryAsync
	(
		this HidPlusPlusTransport transport,
		byte deviceId,
		int retryCount,
		CancellationToken cancellationToken
	)
	{
		var getCountResponse = await transport.FeatureAccessSendWithRetryAsync<FeatureSet.GetCount.Response>(deviceId, 1, FeatureSet.GetCount.FunctionId, retryCount, cancellationToken);

		if (getCountResponse.Count == 0)
			throw new InvalidOperationException();

		int count = getCountResponse.Count + 1;

		var features = new HidPlusPlusFeatureInformation[count];
		features[0] = new(0, HidPlusPlusFeature.Root, HidPlusPlusFeatureTypes.None, 0);

		for (int i = 1; i < count; i++)
		{
			var getFeatureIdResponse = await transport.FeatureAccessSendWithRetryAsync<FeatureSet.GetFeatureId.Request, FeatureSet.GetFeatureId.Response>
			(
				deviceId,
				1,
				1,
				new FeatureSet.GetFeatureId.Request { Index = (byte)i },
				retryCount,
				cancellationToken
			);

			features[i] = new((byte)i, getFeatureIdResponse.FeatureId, getFeatureIdResponse.FeatureType, getFeatureIdResponse.FeatureVersion);
		}

		return new HidPlusPlusFeatureCollection(features);
	}

	public static async Task<byte> GetFeatureIndexAsync(this HidPlusPlusTransport transport, byte deviceId, HidPlusPlusFeature feature, CancellationToken cancellationToken)
	{
		var getFeatureResponse = await transport.FeatureAccessSendAsync<Root.GetFeature.Request, Root.GetFeature.Response>
		(
			deviceId,
			0,
			Root.GetFeature.FunctionId,
			new Root.GetFeature.Request { FeatureId = feature },
			cancellationToken
		);

		return getFeatureResponse.Index;
	}

	public static async Task<byte> GetFeatureIndexWithRetryAsync(this HidPlusPlusTransport transport, byte deviceId, HidPlusPlusFeature feature, int retryCount, CancellationToken cancellationToken)
	{
		var getFeatureResponse = await transport.FeatureAccessSendWithRetryAsync<Root.GetFeature.Request, Root.GetFeature.Response>
		(
			deviceId,
			0,
			Root.GetFeature.FunctionId,
			new Root.GetFeature.Request { FeatureId = feature },
			retryCount,
			cancellationToken
		);

		return getFeatureResponse.Index;
	}
}
