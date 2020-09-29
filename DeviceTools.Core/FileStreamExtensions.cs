using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTools
{
	public static class FileStreamExtensions
	{
		public static Span<byte> IoControl(this FileStream deviceFile, uint ioControlCode, ReadOnlySpan<byte> input, Span<byte> outputBuffer)
			=> deviceFile.SafeFileHandle.IoControl(ioControlCode, input, outputBuffer);
	}
}
