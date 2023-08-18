using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract class RegisterAccess : HidPlusPlusDevice
	{
		private protected RegisterAccess(object parentOrTransport, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(parentOrTransport, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
		}

		public sealed override HidPlusPlusProtocolFlavor ProtocolFlavor => HidPlusPlusProtocolFlavor.RegisterAccess;

		public Task<TResponseParameters> RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>
		(
			Address address,
			in TRequestParameters parameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
			where TResponseParameters : struct, IMessageParameters
			=> Transport.RegisterAccessGetRegisterWithRetryAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetShortRegisterAsync<TResponseParameters>(Address address, CancellationToken cancellationToken)
			where TResponseParameters : struct, IShortMessageParameters
			=> Transport.RegisterAccessGetShortRegisterWithRetryAsync<TResponseParameters>(DeviceIndex, address, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetShortRegisterAsync<TRequestParameters, TResponseParameters>
		(
			Address address,
			in TRequestParameters parameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
			where TResponseParameters : struct, IShortMessageParameters
			=> Transport.RegisterAccessGetShortRegisterWithRetryAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetLongRegisterAsync<TResponseParameters>(Address address, CancellationToken cancellationToken)
			where TResponseParameters : struct, ILongMessageParameters
			=> Transport.RegisterAccessGetLongRegisterWithRetryAsync<TResponseParameters>(DeviceIndex, address, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetLongRegisterAsync<TRequestParameters, TResponseParameters>
		(
			Address address,
			in TRequestParameters parameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
			where TResponseParameters : struct, ILongMessageParameters
			=> Transport.RegisterAccessGetLongRegisterWithRetryAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetVeryLongRegisterAsync<TResponseParameters>(Address address, CancellationToken cancellationToken)
			where TResponseParameters : struct, IVeryLongMessageParameters
			=> Transport.RegisterAccessGetVeryLongRegisterWithRetryAsync<TResponseParameters>(DeviceIndex, address, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetVeryLongRegisterAsync<TRequestParameters, TResponseParameters>
		(
			Address address,
			in TRequestParameters parameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
			where TResponseParameters : struct, IVeryLongMessageParameters
			=> Transport.RegisterAccessGetVeryLongRegisterWithRetryAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);
	}
}
