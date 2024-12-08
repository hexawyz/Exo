namespace DeviceTools.FilterExpressions;

public enum StringListOperator
{
	Contains = 0x00001000,

	ContainsElementStartingWith = 0x00002000,
	ContainsElementEndingWith = 0x00003000,
	ContainsElementContaining = 0x00004000,

	ContainsIgnoreCase = Contains | 0x00020000,

	ContainsElementStartingWithIgnoreCase = ContainsElementStartingWith | 0x00020000,
	ContainsElementEndingWithIgnoreCase = ContainsElementEndingWith | 0x00020000,
	ContainsElementContainingIgnoreCase = ContainsElementContaining | 0x00020000,
}
