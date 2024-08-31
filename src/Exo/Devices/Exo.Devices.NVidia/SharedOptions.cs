using System.Threading.Channels;

namespace Exo.Devices.NVidia;

internal static class SharedOptions
{
	public static readonly BoundedChannelOptions ChannelOptions = new BoundedChannelOptions(10)
	{
		FullMode = BoundedChannelFullMode.DropOldest,
		AllowSynchronousContinuations = false,
		SingleReader = true,
		SingleWriter = true
	};
}
