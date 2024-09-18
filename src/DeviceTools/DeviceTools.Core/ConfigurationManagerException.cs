namespace DeviceTools;

public class ConfigurationManagerException : Exception
{
	internal ConfigurationManagerException(NativeMethods.ConfigurationManagerResult resultCode) : this((uint)resultCode)
	{
	}

	public ConfigurationManagerException(uint resultCode)
		: this(resultCode, $"Configuration manager returned the following error: {((NativeMethods.ConfigurationManagerResult)resultCode).ToString()}.")
	{
	}

	public ConfigurationManagerException(uint resultCode, string message) : base(message)
	{
		ResultCode = resultCode;
	}

	public ConfigurationManagerException(uint resultCode, string message, Exception innerException) : base(message, innerException)
	{
		ResultCode = resultCode;
	}

	public uint ResultCode { get; }
}
