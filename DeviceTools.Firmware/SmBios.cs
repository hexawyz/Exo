using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTools.Firmware;

public sealed partial class SmBios
{
	private readonly struct RawSmBiosHeader
	{
		public readonly byte Used20CallingMethod;
		public readonly byte SmBiosMajorVersion;
		public readonly byte SmBiosMinorVersion;
		public readonly byte DmiRevision;
		public readonly uint Length;
	}

	private readonly struct RawSmBiosStructureHeader
	{
		public readonly byte Type;
		public readonly byte Length;
		private readonly ushort _handle;
		public ushort Handle => Unaligned.ReadAt(_handle);
	}

	public static unsafe byte[] GetRawData()
	{
		uint signature = (((byte)'R' << 8 | (byte)'S') << 8 | (byte)'M') << 8 | (byte)'B';
		uint length = NativeMethods.GetSystemFirmwareTable(signature, 0, null, 0);
		if (length == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
		var data = new byte[length];
		fixed (byte* dataPointer = data)
		{
			length = NativeMethods.GetSystemFirmwareTable(signature, 0, dataPointer, length);
		}
		if (length == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
		else if (length != (uint)data.Length) throw new InvalidDataException();
		return data;
	}

	private static readonly SmBios CachedSmBios = Parse(GetRawData());

	public static SmBios GetForCurrentMachine() => CachedSmBios;

	public static SmBios Parse(ReadOnlySpan<byte> data) => new SmBios(data);

	public byte MajorVersion { get; }
	public byte MinorVersion { get; }
	public byte DmiRevision { get; }

	public Structure.BiosInformation BiosInformation { get; }
	public Structure.SystemInformation SystemInformation { get; }
	public ImmutableArray<Structure.ProcessorInformation> ProcessorInformations { get; }
	public ImmutableArray<Structure.MemoryDevice> MemoryDevices { get; }

	private SmBios(ReadOnlySpan<byte> data)
	{
		ref readonly var header = ref Unsafe.As<byte, RawSmBiosHeader>(ref MemoryMarshal.GetReference(data));
		MajorVersion = header.SmBiosMajorVersion;
		MinorVersion = header.SmBiosMinorVersion;
		DmiRevision = header.DmiRevision;

		var strings = new List<string>();
		var processorInformations = ImmutableArray.CreateBuilder<Structure.ProcessorInformation>(1);
		var memoryDevices = ImmutableArray.CreateBuilder<Structure.MemoryDevice>(4);

		var remaining = data[Unsafe.SizeOf<RawSmBiosHeader>()..];

		while (remaining.Length > 0)
		{
			ref readonly var structureHeader = ref Unsafe.As<byte, RawSmBiosStructureHeader>(ref MemoryMarshal.GetReference(remaining));

			var structureData = remaining[Unsafe.SizeOf<RawSmBiosStructureHeader>()..structureHeader.Length];

			remaining = remaining[structureHeader.Length..];

			// Strings are always parsed, even if they are not used.
			// Doing it at a single place makes the code simpler, as we always need to skip the strings block.
			remaining = remaining[ParseStrings(remaining, strings)..];

			switch (structureHeader.Type)
			{
			case 0:
				if (BiosInformation is not null) throw new InvalidDataException("BIOS Information structure must only appear once.");
				BiosInformation = new(structureHeader.Handle, structureData, strings);
				break;
			case 1:
				if (SystemInformation is not null) throw new InvalidDataException("System Information structure must only appear once.");
				SystemInformation = new(structureHeader.Handle, structureData, strings);
				break;
			case 4:
				processorInformations.Add(new(structureHeader.Handle, structureData, strings));
				break;
			case 17:
				memoryDevices.Add(new(structureHeader.Handle, structureData, strings));
				break;
			}
		}

		// Once parsing is finished, validate that all required structures are present.
		if (BiosInformation is null) throw new InvalidDataException("BIOS Information structure is missing.");
		if (SystemInformation is null) throw new InvalidDataException("System Information structure is missing.");
		if (processorInformations.Count == 0) throw new InvalidDataException("Processor Information structure is missing.");
		if (memoryDevices.Count == 0) throw new InvalidDataException("Memory Device structure is missing.");
		ProcessorInformations = processorInformations.Count == processorInformations.Capacity ? processorInformations.MoveToImmutable() : processorInformations.ToImmutable();
		MemoryDevices = memoryDevices.Count == memoryDevices.Capacity ? memoryDevices.MoveToImmutable() : memoryDevices.ToImmutable();
	}

	private static int ParseStrings(ReadOnlySpan<byte> data, List<string> strings)
	{
		strings.Clear();
		var remaining = data;
		while (true)
		{
			int endIndex = remaining.IndexOf((byte)0);
			if (endIndex < 0) throw new InvalidDataException("Could not find the end of a string.");
			var currentString = remaining[..endIndex];
			remaining = remaining[(endIndex + 1)..];
			if (currentString.Length == 0)
			{
				if (strings.Count == 0)
				{
					if (remaining is not [0, ..])
					{
						throw new InvalidDataException("Could not find the end of the string block.");
					}
					return 2;
				}

				return (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(data), ref MemoryMarshal.GetReference(remaining));
			}
			else
			{
				strings.Add(Encoding.UTF8.GetString(currentString));
			}
		}
	}

	private static string? GetString(List<string> strings, byte index)
		=> index == 0 ? null : strings[index - 1];

	public abstract partial class Structure
	{
		protected internal ushort Handle { get; }

		public abstract byte Type { get; }

		protected Structure(ushort handle) => Handle = handle;
	}
}
