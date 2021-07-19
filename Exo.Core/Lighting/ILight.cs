using System.Threading.Tasks;

namespace Exo.Core.Lighting
{
	public interface ILight
	{
		RgbColor Color { get; }
		bool IsEnabled { get; }
		ValueTask EnableAsync(bool enable);
	}
}
