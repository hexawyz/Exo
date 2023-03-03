namespace Exo.Devices.Logitech.HidPlusPlus;

internal sealed class PendingOperationFactory
{
	// Singleton that will be passed to all factory methods in order to have non-static delegates. (Which are faster)
	internal static readonly PendingOperationFactory Instance = new();

	// This was previously differentiated for all message lengths.
	private static class AnyLength<T>
		where T : struct, IMessageParameters
	{
		public static readonly Func<RawMessageHeader, MessagePendingOperation<T>> Factory = CreateFactory();

		private static Func<RawMessageHeader, MessagePendingOperation<T>> CreateFactory()
		{
			ParameterInformation<T>.ThrowIfInvalid();
			return new(Instance.CreateParameterOperation<T>);
		}
	}

	public static readonly Func<RawMessageHeader, EmptyPendingOperation> Empty = new(Instance.CreateEmptyParameterOperation);

	public static Func<RawMessageHeader, MessagePendingOperation<T>> For<T>()
		where T : struct, IMessageParameters
		=> AnyLength<T>.Factory;

	private PendingOperationFactory() { }
}

// We have to deal with all of this to create non static delegates, and the class needs to be top-level because extension methods in nested classes are not allowed.
internal static class PendingOperationFactoryExtensions
{
	internal static EmptyPendingOperation CreateEmptyParameterOperation(this PendingOperationFactory _, RawMessageHeader header)
		=> new EmptyPendingOperation(header);

	internal static MessagePendingOperation<T> CreateParameterOperation<T>(this PendingOperationFactory _, RawMessageHeader header)
		where T : struct, IMessageParameters
		=> new MessagePendingOperation<T>(header);
}
