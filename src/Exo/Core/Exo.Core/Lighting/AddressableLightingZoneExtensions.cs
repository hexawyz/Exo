using System.Runtime.InteropServices;

namespace Exo.Lighting;

public static class AddressableLightingZoneExtensions
{
	public static ValueTask SetColorAsync<TColor>(this IDynamicAddressableLightingZone<TColor> zone, int index, TColor color)
		where TColor : unmanaged
		=> zone.SetColorsAsync(index, MemoryMarshal.CreateSpan(ref color, 1));

	public static ValueTask SetColorsAsync<TColor>(this IDynamicAddressableLightingZone<TColor> zone, ReadOnlySpan<TColor> colors)
		where TColor : unmanaged
		=> zone.SetColorsAsync(0, colors);
}
