namespace Exo;

public static class PawnIoExtensions
{
	public static void LoadKnownModule(this PawnIo pawnIo, PawnIoKnownModule module)
		=> pawnIo.LoadModuleFromResource(typeof(PawnIoExtensions).Assembly, $"{module}.bin");
}
