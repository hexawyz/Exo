using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

namespace DeviceTools.Logitech.HidPlusPlus;

/// <summary>Exposes the message-sending capabilities of the <see cref="HidPlusPlusTransport"/> class.</summary>
/// <remarks>This provides base methods to work with the HID++ 1.0 (Register Access Protocol) and HID++ 2.0 (Feature Access Protocol).</remarks>
public static class HidPlusPlusTransportExtensions
{
	/// <summary>The default retry count that will be used.</summary>
	/// <remarks>The retry count should be kept quite low. It is set at <c>1</c> for now, but it will be increased if necessary.</remarks>
	public const int DefaultRetryCount = 2;

	/// <summary>The default delay that will be applied before retrying a request when the device is busy.</summary>
	/// <remarks>
	/// This delay is not customizable in the WithRetry methods below, but it can be referenced when implementing the retry mechanism separately.
	/// </remarks>
	public const int DefaultDeviceBusyRetryDelayInMilliseconds = 100;

	private static Task<TResponseParameters> RegisterAccessGetRegisterAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		SubId subId,
		Address address,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, IMessageParameters
		=> Unsafe.As<Task<TResponseParameters>>
		(
			transport.SendAsync
			(
				deviceIndex,
				(byte)subId,
				(byte)address,
				default,
				PendingOperationFactory.For<TResponseParameters>(),
				cancellationToken
			)
		);

	private static async Task<TResponseParameters> RegisterAccessGetRegisterWithRetryAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		SubId subId,
		Address address,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, IMessageParameters
	{
		while (true)
		{
			try
			{
				return await transport.RegisterAccessGetRegisterAsync<TResponseParameters>
				(
					deviceIndex,
					subId,
					address,
					cancellationToken
				).ConfigureAwait(false);
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

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

	private static Task<TResponseParameters> RegisterAccessGetRegisterWithOneExtraParameterAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		SubId subId,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParametersWithOneExtraParameter, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
		=> Unsafe.As<Task<TResponseParameters>>
		(
			transport.SendAsync
			(
				deviceIndex,
				(byte)subId,
				(byte)address,
				ParameterInformation<TRequestParameters>.GetShortReadOnlySpan(parameters),
				PendingOperationFactory.ForOneExtraParameter<TResponseParameters>(),
				cancellationToken
			)
		);

	private static Task<TResponseParameters> RegisterAccessGetRegisterWithTwoExtraParametersAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		SubId subId,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParametersWithTwoExtraParameters, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
		=> Unsafe.As<Task<TResponseParameters>>
		(
			transport.SendAsync
			(
				deviceIndex,
				(byte)subId,
				(byte)address,
				ParameterInformation<TRequestParameters>.GetShortReadOnlySpan(parameters),
				PendingOperationFactory.ForTwoExtraParameters<TResponseParameters>(),
				cancellationToken
			)
		);

	private static async Task<TResponseParameters> RegisterAccessGetRegisterWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		SubId subId,
		Address address,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
	{
		while (true)
		{
			try
			{
				return await transport.RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>
				(
					deviceIndex,
					subId,
					address,
					parameters,
					cancellationToken
				).ConfigureAwait(false);
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

	private static async Task<TResponseParameters> RegisterAccessGetRegisterWithOneExtraParameterWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		SubId subId,
		Address address,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParametersWithOneExtraParameter, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
	{
		while (true)
		{
			try
			{
				return await transport.RegisterAccessGetRegisterWithOneExtraParameterAsync<TRequestParameters, TResponseParameters>
				(
					deviceIndex,
					subId,
					address,
					parameters,
					cancellationToken
				).ConfigureAwait(false);
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

	private static async Task<TResponseParameters> RegisterAccessGetRegisterWithTwoExtraParametersWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		SubId subId,
		Address address,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParametersWithTwoExtraParameters, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
	{
		while (true)
		{
			try
			{
				return await transport.RegisterAccessGetRegisterWithTwoExtraParametersAsync<TRequestParameters, TResponseParameters>
				(
					deviceIndex,
					subId,
					address,
					parameters,
					cancellationToken
				).ConfigureAwait(false);
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

	public static Task<TResponseParameters> RegisterAccessGetRegisterAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, IMessageParameters
		=> transport.RegisterAccessGetRegisterAsync<TResponseParameters>
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
			cancellationToken
		);

	public static async Task<TResponseParameters> RegisterAccessGetRegisterWithRetryAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, IMessageParameters
	{
		while (true)
		{
			try
			{
				return await transport.RegisterAccessGetRegisterAsync<TResponseParameters>
				(
					deviceIndex,
					address,
					cancellationToken
				).ConfigureAwait(false);
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

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

	public static Task<TResponseParameters> RegisterAccessGetRegisterWithOneExtraParameterAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParametersWithOneExtraParameter, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
		=> transport.RegisterAccessGetRegisterWithOneExtraParameterAsync<TRequestParameters, TResponseParameters>
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

	public static Task<TResponseParameters> RegisterAccessGetRegisterWithTwoExtraParametersAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParametersWithTwoExtraParameters, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
		=> transport.RegisterAccessGetRegisterWithTwoExtraParametersAsync<TRequestParameters, TResponseParameters>
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

	public static async Task<TResponseParameters> RegisterAccessGetRegisterWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
	{
		while (true)
		{
			try
			{
				return await transport.RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>
				(
					deviceIndex,
					address,
					parameters,
					cancellationToken
				).ConfigureAwait(false);
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

	public static async Task<TResponseParameters> RegisterAccessGetRegisterWithOneExtraParameterWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParametersWithOneExtraParameter, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
	{
		while (true)
		{
			try
			{
				return await transport.RegisterAccessGetRegisterWithOneExtraParameterAsync<TRequestParameters, TResponseParameters>
				(
					deviceIndex,
					address,
					parameters,
					cancellationToken
				).ConfigureAwait(false);
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

	public static async Task<TResponseParameters> RegisterAccessGetRegisterWithTwoExtraParametersWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParametersWithTwoExtraParameters, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
	{
		while (true)
		{
			try
			{
				return await transport.RegisterAccessGetRegisterWithTwoExtraParametersAsync<TRequestParameters, TResponseParameters>
				(
					deviceIndex,
					address,
					parameters,
					cancellationToken
				).ConfigureAwait(false);
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

	public static Task<TResponseParameters> RegisterAccessGetShortRegisterAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, IShortMessageParameters
		=> transport.RegisterAccessGetRegisterAsync<TResponseParameters>(deviceIndex, SubId.GetShortRegister, address, cancellationToken);

	public static Task<TResponseParameters> RegisterAccessGetShortRegisterWithRetryAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, IShortMessageParameters
		=> transport.RegisterAccessGetRegisterWithRetryAsync<TResponseParameters>(deviceIndex, SubId.GetShortRegister, address, retryCount, cancellationToken);

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

	public static Task<TResponseParameters> RegisterAccessGetShortRegisterWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, IShortMessageParameters
		=> transport.RegisterAccessGetRegisterWithRetryAsync<TRequestParameters, TResponseParameters>(deviceIndex, SubId.GetShortRegister, address, parameters, retryCount, cancellationToken);

	public static Task<TResponseParameters> RegisterAccessGetLongRegisterAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, ILongMessageParameters
		=> transport.RegisterAccessGetRegisterAsync<TResponseParameters>(deviceIndex, SubId.GetLongRegister, address, cancellationToken);

	public static Task<TResponseParameters> RegisterAccessGetLongRegisterWithRetryAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, ILongMessageParameters
		=> transport.RegisterAccessGetRegisterWithRetryAsync<TResponseParameters>(deviceIndex, SubId.GetLongRegister, address, retryCount, cancellationToken);

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

	public static Task<TResponseParameters> RegisterAccessGetLongRegisterWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, ILongMessageParameters
		=> transport.RegisterAccessGetRegisterWithRetryAsync<TRequestParameters, TResponseParameters>(deviceIndex, SubId.GetLongRegister, address, parameters, retryCount, cancellationToken);

	public static Task<TResponseParameters> RegisterAccessGetLongRegisterWithOneExtraParameterWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParametersWithOneExtraParameter, IShortMessageParameters
		where TResponseParameters : struct, ILongMessageParameters
		=> transport.RegisterAccessGetRegisterWithOneExtraParameterWithRetryAsync<TRequestParameters, TResponseParameters>(deviceIndex, SubId.GetLongRegister, address, parameters, retryCount, cancellationToken);

	public static Task<TResponseParameters> RegisterAccessGetLongRegisterWithTwoExtraParametersWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		in TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParametersWithTwoExtraParameters, IShortMessageParameters
		where TResponseParameters : struct, ILongMessageParameters
		=> transport.RegisterAccessGetRegisterWithTwoExtraParametersWithRetryAsync<TRequestParameters, TResponseParameters>(deviceIndex, SubId.GetLongRegister, address, parameters, retryCount, cancellationToken);

	public static Task<TResponseParameters> RegisterAccessGetVeryLongRegisterAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, IVeryLongMessageParameters
		=> transport.RegisterAccessGetRegisterAsync<TResponseParameters>(deviceIndex, SubId.GetShortRegister, address, cancellationToken);

	public static Task<TResponseParameters> RegisterAccessGetVeryLongRegisterWithRetryAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, IVeryLongMessageParameters
		=> transport.RegisterAccessGetRegisterWithRetryAsync<TResponseParameters>(deviceIndex, SubId.GetShortRegister, address, retryCount, cancellationToken);

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

	public static Task<TResponseParameters> RegisterAccessGetVeryLongRegisterWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, IVeryLongMessageParameters
		=> transport.RegisterAccessGetRegisterWithRetryAsync<TRequestParameters, TResponseParameters>(deviceIndex, SubId.GetShortRegister, address, parameters, retryCount, cancellationToken);

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
	public static async Task RegisterAccessSetRegisterWithRetryAsync<TRequestParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageSetParameters, IMessageParameters
	{
		while (true)
		{
			try
			{
				await transport.RegisterAccessSetRegisterAsync
				(
					deviceIndex,
					address,
					parameters,
					cancellationToken
				).ConfigureAwait(false);
				return;
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
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

	public static async Task RegisterAccessSetShortRegisterWithRetryAsync<TRequestParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageSetParameters, IShortMessageParameters
	{
		while (true)
		{
			try
			{
				await transport.RegisterAccessSetShortRegisterAsync
				(
					deviceIndex,
					address,
					parameters,
					cancellationToken
				).ConfigureAwait(false);
				return;
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

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

	public static async Task RegisterAccessSetLongRegisterWithRetryAsync<TRequestParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageSetParameters, ILongMessageParameters
	{
		while (true)
		{
			try
			{
				await transport.RegisterAccessSetLongRegisterAsync
				(
					deviceIndex,
					address,
					parameters,
					cancellationToken
				).ConfigureAwait(false);
				return;
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

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

	public static async Task RegisterAccessSetVeryLongRegisterWithRetryAsync<TRequestParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		Address address,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageSetParameters, IVeryLongMessageParameters
	{
		while (true)
		{
			try
			{
				await transport.RegisterAccessSetVeryLongRegisterAsync
				(
					deviceIndex,
					address,
					parameters,
					cancellationToken
				).ConfigureAwait(false);
				return;
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

	private static byte ForFunctionId(this HidPlusPlusTransport transport, byte functionId) => (byte)(functionId << 4 | transport.FeatureAccessSoftwareId);

	public static Task FeatureAccessSendAsync
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		byte featureIndex,
		byte functionId,
		CancellationToken cancellationToken
	)
		=>
			transport.SendAsync
			(
				deviceIndex,
				featureIndex,
				transport.ForFunctionId(functionId),
				default,
				PendingOperationFactory.Empty,
				cancellationToken
			);

	public static async Task FeatureAccessSendWithRetryAsync
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		byte featureIndex,
		byte functionId,
		int retryCount,
		CancellationToken cancellationToken
	)
	{
		while (true)
		{
			try
			{
				await transport.FeatureAccessSendAsync(deviceIndex, featureIndex, functionId, cancellationToken).ConfigureAwait(false);
				return;
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus2Exception ex) when (ex.ErrorCode == FeatureAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

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

	public static async Task<TResponseParameters> FeatureAccessSendWithRetryAsync<TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		byte featureIndex,
		byte functionId,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, IMessageResponseParameters
	{
		while (true)
		{
			try
			{
				return await transport.FeatureAccessSendAsync<TResponseParameters>(deviceIndex, featureIndex, functionId, cancellationToken).ConfigureAwait(false);
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus2Exception ex) when (ex.ErrorCode == FeatureAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}

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

	public static async Task FeatureAccessSendWithRetryAsync<TRequestParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		byte featureIndex,
		byte functionId,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageRequestParameters
	{
		while (true)
		{
			try
			{
				await transport.FeatureAccessSendAsync(deviceIndex, featureIndex, functionId, parameters, cancellationToken).ConfigureAwait(false);
				return;
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus2Exception ex) when (ex.ErrorCode == FeatureAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
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

	public static async Task<TResponseParameters> FeatureAccessSendWithRetryAsync<TRequestParameters, TResponseParameters>
	(
		this HidPlusPlusTransport transport,
		byte deviceIndex,
		byte featureIndex,
		byte functionId,
		TRequestParameters parameters,
		int retryCount,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageRequestParameters
		where TResponseParameters : struct, IMessageResponseParameters
	{
		while (true)
		{
			try
			{
				return await transport.FeatureAccessSendAsync<TRequestParameters, TResponseParameters>(deviceIndex, featureIndex, functionId, parameters, cancellationToken).ConfigureAwait(false);
			}
			catch (TimeoutException) when (retryCount > 0)
			{
				retryCount--;
			}
			catch (HidPlusPlus2Exception ex) when (ex.ErrorCode == FeatureAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.Busy && retryCount > 0)
			{
				await Task.Delay(DefaultDeviceBusyRetryDelayInMilliseconds, cancellationToken).ConfigureAwait(false);
				retryCount--;
			}
		}
	}
}
