namespace DeviceTools.FilterExpressions
{
	public readonly struct StringResource
	{
		public string Value { get; }

		public StringResource(string value) => Value = value;

		public override string ToString() => Value;
	}
}
