namespace DeviceTools.Firmware;

public sealed partial class SmBios
{

	public abstract partial class Structure
	{
		public sealed class SystemInformation : Structure
		{
			public override byte Type => 1;

			internal SystemInformation(ushort handle, ReadOnlySpan<byte> data, List<string> strings) : base(handle)
			{
			}
		}
	}
}
