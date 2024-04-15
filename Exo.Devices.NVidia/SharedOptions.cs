using System.Threading.Channels;

namespace Exo.Devices.NVidia;

internal static class SharedOptions
{
	public static readonly UnboundedChannelOptions ChannelOptions = new UnboundedChannelOptions { AllowSynchronousContinuations = true, SingleReader = true, SingleWriter = true };
}
