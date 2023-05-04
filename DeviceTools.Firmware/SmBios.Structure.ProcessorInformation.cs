namespace DeviceTools.Firmware;

public sealed partial class SmBios
{

	public abstract partial class Structure
	{
		public sealed class ProcessorInformation : Structure
		{
			public override byte Type => 4;

			internal ProcessorInformation(ushort handle, ReadOnlySpan<byte> data, List<string> strings) : base(handle)
			{
			}
		}
	}
}
