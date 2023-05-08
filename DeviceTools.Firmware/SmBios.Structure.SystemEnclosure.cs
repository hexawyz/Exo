namespace DeviceTools.Firmware;

public sealed partial class SmBios
{
	public abstract partial class Structure
	{
		public sealed class SystemEnclosure : Structure
		{
			public override byte Type => 3;

			internal SystemEnclosure(ushort handle, ReadOnlySpan<byte> data, List<string> strings) : base(handle)
			{
			}
		}
	}
}
