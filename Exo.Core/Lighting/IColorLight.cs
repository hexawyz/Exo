using System.Threading.Tasks;

namespace Exo.Lighting
{
	public interface IColorLight : ILight
	{
		ValueTask EnableAsync(bool enable, RgbColor color);
		ValueTask SetColorAsync(RgbColor color);
	}
}
