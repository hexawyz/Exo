namespace Exo.Configuration;

public readonly struct ConfigurationResult<T>
{
	public T? Value { get; }
	public ConfigurationStatus Status { get; }

	internal ConfigurationResult(ConfigurationStatus status)
	{
		Value = default!;
		Status = status;
	}

	internal ConfigurationResult(T value)
	{
		Value = value;
		Status = ConfigurationStatus.Found;
	}

	public bool Found => Status == ConfigurationStatus.Found;

	public void ThrowIfNotFound()
	{
		var status = Status;
		switch (status)
		{
		case ConfigurationStatus.Found: return;
		case ConfigurationStatus.MissingContainer: throw new InvalidOperationException("Missing configuration container.");
		case ConfigurationStatus.MissingValue: throw new InvalidOperationException("Missing configuration value.");
		case ConfigurationStatus.InvalidValue: throw new InvalidOperationException("Invalid configuration value.");
		case ConfigurationStatus.MalformedData: throw new InvalidOperationException("Malformed configuration data.");
		default: throw new InvalidOperationException();
		}
	}
}
