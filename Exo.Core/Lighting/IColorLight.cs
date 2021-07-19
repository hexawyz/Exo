using System.Threading.Tasks;

namespace Exo.Core.Lighting
{
	public interface IColorLight : ILight
	{
		ValueTask EnableAsync(bool enable, RgbColor color);
		ValueTask SetColorAsync(RgbColor color);
	}
}
