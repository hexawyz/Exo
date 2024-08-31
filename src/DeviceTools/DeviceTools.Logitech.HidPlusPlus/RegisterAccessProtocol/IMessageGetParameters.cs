namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

public interface IMessageGetParameters : IMessageParameters { }

public interface IMessageGetParametersWithOneExtraParameter : IMessageGetParameters
{
	byte Parameter { get; }
}

public interface IMessageGetParametersWithTwoExtraParameters : IMessageGetParameters
{
	byte Parameter1 { get; }
	byte Parameter2 { get; }
}
