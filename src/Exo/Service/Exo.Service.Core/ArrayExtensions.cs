using System.Threading.Channels;

namespace Exo.Service;

internal static class ChannelWriterArrayExtensions
{
	public static void TryWrite<T>(this ChannelWriter<T>[]? writers, T item)
	{
		if (writers is null) return;
		foreach (var writer in writers)
		{
			writer.TryWrite(item);
		}
	}
}
