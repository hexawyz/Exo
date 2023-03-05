using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Exo.Devices.Logitech.HidPlusPlus.FeatureAccessProtocol;
using Exo.Devices.Logitech.HidPlusPlus.RegisterAccessProtocol;

namespace Exo.Devices.Logitech.HidPlusPlus;

/// <summary>Exposes the message-sending capabilities of the <see cref="HidPlusPlusTransport"/> class.</summary>
/// <remarks>This provides base methods to work with the HID++ 1.0 (Register Access Protocol) and HID++ 2.0 (Feature Access Protocol).</remarks>
public static class HidPlusPlusTransportExtensions
{
	private static Task<TResponseParameters> RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		SubId subId,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
		=> Unsafe.As<Task<TResponseParameters>>
		(
			transport.SendAsync
			(
				deviceIndex,
				(byte)subId,
				(byte)address,
				ParameterInformation<TRequestParameters>.GetShortReadOnlySpan(parameters),
				PendingOperationFactory.For<TResponseParameters>(),
				cancellationToken
			)
		);

	public static Task<TResponseParameters> RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
		=> transport.RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>
		(
			deviceIndex,
			ParameterInformation<TResponseParameters>.NativeSupportedReport switch
			{
				SupportedReports.Short => SubId.GetShortRegister,
				SupportedReports.Long => SubId.GetLongRegister,
				SupportedReports.VeryLong => SubId.GetVeryLongRegister,
				_ => throw new NotSupportedException()
			},
			address,
			parameters,
			cancellationToken
		);

	public static Task<TResponseParameters> RegisterAccessGetShortRegisterAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, IShortMessageParameters
		=> transport.RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>(deviceIndex, SubId.GetShortRegister, address, parameters, cancellationToken);

	public static Task<TResponseParameters> RegisterAccessGetLongRegisterAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, ILongMessageParameters
		=> transport.RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>(deviceIndex, SubId.GetLongRegister, address, parameters, cancellationToken);

	public static Task<TResponseParameters> RegisterAccessGetVeryLongRegisterAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, IVeryLongMessageParameters
		=> transport.RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>(deviceIndex, SubId.GetShortRegister, address, parameters, cancellationToken);

	public static Task RegisterAccessSetRegisterAsync<TRequestParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageSetParameters, IMessageParameters
	{
		// Disallow message truncation.
		// While automatically dispatching the correct operation will be helpful and appropriate in most cases, automatic truncation of messages could be harmful.
		// The explicit SetXXX methods below still allow message truncation if it was ever required. (May revisit this later to explicitly disable it, and thus possibly remove those methods)
		if (ParameterInformation<TRequestParameters>.NativeSupportedReport <= transport.SupportedReports)
		{
			return transport.SendAsync
			(
				deviceIndex,
				ParameterInformation<TRequestParameters>.NativeSupportedReport switch
				{
					SupportedReports.Short => (byte)SubId.SetShortRegister,
					SupportedReports.Long => (byte)SubId.SetLongRegister,
					SupportedReports.VeryLong => (byte)SubId.SetVeryLongRegister,
					_ => throw new NotSupportedException()
				},
				(byte)address,
				ParameterInformation<TRequestParameters>.GetShortReadOnlySpan(parameters),
				PendingOperationFactory.For<TRequestParameters>(),
				cancellationToken
			);
		}
		else
		{
			// This exception is legitimately expected to occur in case of naïve/bogus use of the API, so it is better to wrap it in a task, as if it had occurred within an AsyncMethodBuilder.
			return Task.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException($"Cannot set registers with parameters of type {typeof(TRequestParameters)} on the current transport.")));
		}
	}

	public static Task RegisterAccessSetShortRegisterAsync<TRequestParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageSetParameters, IShortMessageParameters
		=> transport.SendAsync
		(
			deviceIndex,
			(byte)SubId.SetShortRegister,
			(byte)address,
			ParameterInformation<TRequestParameters>.GetShortReadOnlySpan(parameters),
			PendingOperationFactory.For<TRequestParameters>(),
			cancellationToken
		);

	public static Task RegisterAccessSetLongRegisterAsync<TRequestParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageSetParameters, ILongMessageParameters
		=> transport.SendAsync
		(
			deviceIndex,
			(byte)SubId.SetLongRegister,
			(byte)address,
			ParameterInformation<TRequestParameters>.GetLongReadOnlySpan(parameters),
			PendingOperationFactory.For<TRequestParameters>(),
			cancellationToken
		);

	public static Task RegisterAccessSetVeryLongRegisterAsync<TRequestParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageSetParameters, IVeryLongMessageParameters
		=> transport.SendAsync
		(
			deviceIndex,
			(byte)SubId.SetVeryLongRegister,
			(byte)address,
			ParameterInformation<TRequestParameters>.GetVeryLongReadOnlySpan(parameters),
			PendingOperationFactory.For<TRequestParameters>(),
			cancellationToken
		);

	private static byte ForFunctionId(this HidPlusPlusTransport transport, byte functionId) => (byte)(functionId << 4 | transport.FeatureAccessSoftwareId);

	public static Task<TResponseParameters> FeatureAccessSendAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		byte featureIndex,
		byte functionId,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, IMessageResponseParameters
		=> Unsafe.As<Task<TResponseParameters>>
		(
			transport.SendAsync
			(
				deviceIndex,
				featureIndex,
				transport.ForFunctionId(functionId),
				default,
				PendingOperationFactory.For<TResponseParameters>(),
				cancellationToken
			)
		);

	public static Task FeatureAccessSendAsync<TRequestParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		byte featureIndex,
		byte functionId,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageRequestParameters
	{
		// Disallow message truncation.
		// While automatically dispatching the correct operation will be helpful and appropriate in most cases, automatic truncation of messages could be harmful.
		// The explicit SetXXX methods below still allow message truncation if it was ever required. (May revisit this later to explicitly disable it, and thus possibly remove those methods)
		if (ParameterInformation<TRequestParameters>.NativeSupportedReport <= transport.SupportedReports)
		{
			return transport.SendAsync
			(
				deviceIndex,
				featureIndex,
				transport.ForFunctionId(functionId),
				ParameterInformation<TRequestParameters>.GetNativeReadOnlySpan(parameters),
				PendingOperationFactory.Empty,
				cancellationToken
			);
		}
		else
		{
			return Task.FromException
			(
				ExceptionDispatchInfo.SetCurrentStackTrace
				(
					new InvalidOperationException($"Cannot call features with parameters of type {typeof(TRequestParameters)} on the current transport.")
				)
			);
		}
	}

	public static Task<TResponseParameters> FeatureAccessSendAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		byte featureIndex,
		byte functionId,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageRequestParameters
		where TResponseParameters : struct, IMessageResponseParameters
	{
		// Disallow message truncation.
		// While automatically dispatching the correct operation will be helpful and appropriate in most cases, automatic truncation of messages could be harmful.
		// The explicit SetXXX methods below still allow message truncation if it was ever required. (May revisit this later to explicitly disable it, and thus possibly remove those methods)
		if (ParameterInformation<TRequestParameters>.NativeSupportedReport <= transport.SupportedReports)
		{
			return Unsafe.As<Task<TResponseParameters>>
			(
				transport.SendAsync
				(
					deviceIndex,
					featureIndex,
					transport.ForFunctionId(functionId),
					ParameterInformation<TRequestParameters>.GetNativeReadOnlySpan(parameters),
					PendingOperationFactory.For<TResponseParameters>(),
					cancellationToken
				)
			);
		}
		else
		{
			return Task.FromException<TResponseParameters>
			(
				ExceptionDispatchInfo.SetCurrentStackTrace
				(
					new InvalidOperationException($"Cannot call features with parameters of type {typeof(TRequestParameters)} on the current transport.")
				)
			);
		}
	}
}
