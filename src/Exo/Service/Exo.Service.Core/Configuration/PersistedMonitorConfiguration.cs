using Exo.Images;

namespace Exo.Service.Configuration;

[TypeId(0x5A84D766, 0x721A, 0x478A, 0xA8, 0xF7, 0x51, 0x99, 0xED, 0x9A, 0xE0, 0x54)]
internal readonly struct PersistedMonitorConfiguration
{
	public Guid GraphicsId { get; init; }
	public UInt128 ImageId { get; init; }
	public Rectangle ImageRegion { get; init; }
}
