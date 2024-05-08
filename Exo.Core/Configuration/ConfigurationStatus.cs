namespace Exo.Configuration;

public enum ConfigurationStatus : sbyte
{
	Found = 0,
	MissingContainer = 1,
	MissingValue = 2,
	InvalidValue = 3,
	MalformedData = 4,
}
