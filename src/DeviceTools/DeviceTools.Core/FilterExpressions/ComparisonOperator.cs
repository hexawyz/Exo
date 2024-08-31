namespace DeviceTools.FilterExpressions
{
	public enum ComparisonOperator
	{
		Exists = 0x00000001,
		NotExists = Exists | 0x00010000,
		Equals = 0x00000002,
		NotEquals = Equals | 0x00010000,
		GreaterThan = 0x00000003,
		LessThan = 0x00000004,
		GreaterThanOrEquals = 0x00000005,
		LessThanOrEquals = 0x00000006,
	}
}
