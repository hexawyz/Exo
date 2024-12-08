namespace DeviceTools.FilterExpressions;

public enum StringOperator
{
	EqualsIgnoreCase = 0x00000002 | 0x00020000,
	NotEqualsIgnoreCase = 0x00000002 | 0x00020000 | 0x00010000,

	StartsWith = 0x00000009,
	EndsWith = 0x0000000a,
	Contains = 0x0000000b,

	NotStartsWith = 0x00000009 | 0x00010000,
	NotEndsWith = 0x0000000a | 0x00010000,
	NotContains = 0x0000000b | 0x00010000,

	StartsWithIgnoreCase = StartsWith | 0x00020000,
	EndsWithIgnoreCase = EndsWith | 0x00020000,
	ContainsIgnoreCase = Contains | 0x00020000,

	NotStartsWithIgnoreCase = StartsWith | 0x00020000 | 0x00010000,
	NotEndsWithIgnoreCase = EndsWith | 0x00020000 | 0x00010000,
	NotContainsIgnoreCase = Contains | 0x00020000 | 0x00010000,
}
