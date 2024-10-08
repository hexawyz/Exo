using System.Runtime.CompilerServices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using Microsoft.Extensions.Logging;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public sealed class FeatureAccessDirect : FeatureAccess
	{
		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<HidPlusPlusTransport>(ParentOrTransport);

		internal FeatureAccessDirect
		(
			HidPlusPlusTransport transport,
			ILogger<FeatureAccessDirect> logger,
			HidPlusPlusDeviceId[] deviceIds,
			byte mainDeviceIdIndex,
			byte deviceIndex,
			DeviceConnectionInfo deviceConnectionInfo,
			FeatureAccessProtocol.DeviceType deviceType,
			HidPlusPlusFeatureCollection cachedFeatures,
			string? friendlyName,
			string? serialNumber
		)
			: base(transport, logger, deviceIds, mainDeviceIdIndex, deviceIndex, deviceConnectionInfo, deviceType, cachedFeatures, friendlyName, serialNumber)
		{
		}

		public override async ValueTask DisposeAsync(bool parentDisposed)
		{
			await Transport.DisposeAsync().ConfigureAwait(false);
			await base.DisposeAsync(parentDisposed).ConfigureAwait(false);
		}
	}
}
