using System.Runtime.CompilerServices;

namespace Exo;

[InlineArray(5)]
public struct FixedArray5<T>
where T : unmanaged
{
#pragma warning disable IDE0044
	private T _element0;
#pragma warning restore IDE0044 // Add readonly modifier
}

[InlineArray(8)]
public struct FixedArray8<T>
	where T : unmanaged
{
#pragma warning disable IDE0044
	private T _element0;
#pragma warning restore IDE0044 // Add readonly modifier
}

[InlineArray(10)]
public struct FixedArray10<T>
	where T : unmanaged
{
#pragma warning disable IDE0044
	private T _element0;
#pragma warning restore IDE0044 // Add readonly modifier
}

[InlineArray(16)]
public struct FixedArray16<T>
	where T : unmanaged
{
#pragma warning disable IDE0044
	private T _element0;
#pragma warning restore IDE0044 // Add readonly modifier
}

[InlineArray(32)]
public struct FixedArray32<T>
	where T : unmanaged
{
#pragma warning disable IDE0044
	private T _element0;
#pragma warning restore IDE0044 // Add readonly modifier
}
