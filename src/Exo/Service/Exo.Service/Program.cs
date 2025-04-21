using System.Collections.Immutable;
using Exo.Utils;

namespace Exo.Service;

public static class Program
{
	public static ImmutableArray<byte> GitCommitId => GitCommitHelper.GetCommitId(typeof(Program).Assembly);

	public static void Main(string[] args)
	{
		SixLabors.ImageSharp.Configuration.Default.MemoryAllocator = new ImageSharpNativeMemoryAllocator();

		new ExoService().Run();
	}
}
