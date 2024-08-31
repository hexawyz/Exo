namespace DeviceTools;

public static class FileStreamExtensions
{
	public static int IoControl(this FileStream deviceFile, int ioControlCode, ReadOnlySpan<byte> input, Span<byte> outputBuffer)
		=> unchecked((int)deviceFile.SafeFileHandle.IoControl(unchecked((uint)ioControlCode), input, outputBuffer));
}
