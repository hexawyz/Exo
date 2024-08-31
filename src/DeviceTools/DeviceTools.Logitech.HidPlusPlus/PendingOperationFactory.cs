namespace DeviceTools.Logitech.HidPlusPlus;

internal sealed class PendingOperationFactory
{
	// Singleton that will be passed to all factory methods in order to have non-static delegates. (Which are faster)
	internal static readonly PendingOperationFactory Instance = new();

	// This was previously differentiated for all message lengths.
	private static class AnyLength<T>
		where T : struct, IMessageParameters
	{
		public static readonly OperationFactory Factory = CreateFactory();
		public static readonly OperationFactory OneExtraParameterFactory = CreateFactory();
		public static readonly OperationFactory TwoExtraParametersFactory = CreateFactory();

		private static OperationFactory CreateFactory()
		{
			ParameterInformation<T>.ThrowIfInvalid();
			return new(Instance.CreateParameterOperation<T>);
		}

		private static OperationFactory CreateParameterOperationWithOneExtraParameter()
		{
			ParameterInformation<T>.ThrowIfInvalid();
			return new(Instance.CreateParameterOperation<T>);
		}

		private static OperationFactory CreateParameterOperationWithTwoExtraParameters()
		{
			ParameterInformation<T>.ThrowIfInvalid();
			return new(Instance.CreateParameterOperation<T>);
		}
	}

	public static readonly OperationFactory Empty = new(Instance.CreateEmptyParameterOperation);

	public static OperationFactory For<T>()
		where T : struct, IMessageParameters
		=> AnyLength<T>.Factory;

	public static OperationFactory ForOneExtraParameter<T>()
		where T : struct, IMessageParameters
		=> AnyLength<T>.OneExtraParameterFactory;

	public static OperationFactory ForTwoExtraParameters<T>()
		where T : struct, IMessageParameters
		=> AnyLength<T>.TwoExtraParametersFactory;

	private PendingOperationFactory() { }
}

// We have to deal with all of this to create non static delegates, and the class needs to be top-level because extension methods in nested classes are not allowed.
internal static class PendingOperationFactoryExtensions
{
	internal static EmptyPendingOperation CreateEmptyParameterOperation(this PendingOperationFactory _, in RawMessageHeader header, ReadOnlySpan<byte> message)
		=> new EmptyPendingOperation(header);

	internal static MessagePendingOperation<T> CreateParameterOperation<T>(this PendingOperationFactory _, in RawMessageHeader header, ReadOnlySpan<byte> message)
		where T : struct, IMessageParameters
		=> new MessagePendingOperation<T>(header);

	internal static MessagePendingOperation<T> CreateParameterOperationWithOneExtraParameter<T>(this PendingOperationFactory _, in RawMessageHeader header, ReadOnlySpan<byte> message)
		where T : struct, IMessageParameters
		=> new MessagePendingOperationWithOneExtraParameter<T>(header, message[0]);

	internal static MessagePendingOperation<T> CreateParameterOperationWithTwoExtraParameters<T>(this PendingOperationFactory _, in RawMessageHeader header, ReadOnlySpan<byte> message)
		where T : struct, IMessageParameters
		=> new MessagePendingOperationWithTwoExtraParameters<T>(header, message[0], message[1]);
}

internal delegate PendingOperation OperationFactory(in RawMessageHeader header, ReadOnlySpan<byte> message);
