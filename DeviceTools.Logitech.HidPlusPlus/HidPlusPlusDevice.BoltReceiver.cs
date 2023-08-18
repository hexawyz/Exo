using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Notifications;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Registers;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public sealed class BoltReceiver : RegisterAccessReceiver
	{
		internal BoltReceiver(HidPlusPlusTransport transport, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(transport, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
		}

		// NB: Do HID++ 1.0 Bolt devices exist ?
		// If so, we'll need to change the algorithm here. Bolt devices seem to use their Bluetooth (USB) PID as WPID directly.
		// This kinda makes sense, as Bolt is based upon Bluetooth, but it does not provide any information on the underlying protocol used by the device.
		private protected override HidPlusPlusProtocolFlavor InferProtocolFlavor(in DeviceConnectionParameters deviceConnectionParameters)
			=> HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess;

		protected override async Task<(RegisterAccessProtocol.DeviceType DeviceType, string? DeviceName, string? SerialNumber)> GetPairedDeviceInformationAsync
		(
			byte deviceIndex,
			int retryCount,
			CancellationToken cancellationToken
		)
		{
			// The serial number should probably be somewhere in there, but apart from the WPID (which seems to be the BT/USB PID in this case), nothing appears obvious.
			var boltPairingInformation = await Transport.RegisterAccessGetLongRegisterWithRetryAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.BoltPairingInformationResponse>
			(
				DeviceIndex,
				Address.NonVolatileAndPairingInformation,
				new NonVolatileAndPairingInformation.Request { Parameter = NonVolatileAndPairingInformation.Parameter.BoltPairingInformation1 - 1 + deviceIndex },
				retryCount,
				cancellationToken
			).ConfigureAwait(false);

			// Seems like this could be a multipart query, but I don't have any device with a name longer than 13 characters to verify this.
			var deviceNameResponse = await Transport.RegisterAccessGetLongRegisterWithRetryAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.BoltDeviceNameResponse>
			(
				DeviceIndex,
				Address.NonVolatileAndPairingInformation,
				new NonVolatileAndPairingInformation.Request { Parameter = NonVolatileAndPairingInformation.Parameter.BoltDeviceName1 - 1 + deviceIndex, Index = 1 },
				retryCount,
				cancellationToken
			).ConfigureAwait(false);

			var deviceName = deviceNameResponse.GetDeviceName();

			return (0, deviceName, null);
		}
	}
}
