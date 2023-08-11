namespace Exo;

public static class Checksum
{
	public static byte Xor(ReadOnlySpan<byte> buffer, byte initialValue)
	{
		byte b = initialValue;
		for (int i = 0; i < buffer.Length; i++)
		{
			b ^= buffer[i];
		}
		return b;
	}
}
