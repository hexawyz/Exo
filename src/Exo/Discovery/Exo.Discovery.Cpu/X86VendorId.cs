using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exo.Discovery;

[JsonConverter(typeof(JsonConverter))]
public readonly struct X86VendorId : IEquatable<X86VendorId>
{
	internal sealed class JsonConverter : JsonConverter<X86VendorId>
	{
		public override X86VendorId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> new(reader.GetString()!);

		public override void Write(Utf8JsonWriter writer, X86VendorId value, JsonSerializerOptions options)
			=> writer.WriteStringValue(AsSpan(value));
	}

	public static X86VendorId ForCurrentCpu()
	{
		if (!X86Base.IsSupported) throw new PlatformNotSupportedException();

		var (_, ebx, ecx, edx) = X86Base.CpuId(0, 0);
		return new X86VendorId((uint)ebx, (uint)edx, (uint)ecx);
	}

	private readonly uint _ebx;
	private readonly uint _edx;
	private readonly uint _ecx;

	public X86VendorId(uint ebx, uint edx, uint ecx)
	{
		_ebx = ebx;
		_edx = edx;
		_ecx = ecx;
	}

	public X86VendorId(string manufacturerId)
	{
		ArgumentNullException.ThrowIfNull(manufacturerId);
		if (manufacturerId.Length != 12) throw new ArgumentException();

		Encoding.ASCII.GetBytes(manufacturerId, MemoryMarshal.CreateSpan(ref Unsafe.As<uint, byte>(ref _ebx), 12));
	}

	private static ReadOnlySpan<byte> AsSpan(in X86VendorId key)
		=> MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref Unsafe.AsRef(in key._ebx)), 12);

	public override bool Equals(object? obj) => obj is X86VendorId key && Equals(key);

	public bool Equals(X86VendorId other)
		=> _ebx == other._ebx && _edx == other._edx && _ecx == other._ecx;

	public override int GetHashCode()
		=> HashCode.Combine(_ebx, _edx, _ecx);

	public override string ToString() => Encoding.ASCII.GetString(AsSpan(in this));

	public static bool operator ==(X86VendorId left, X86VendorId right) => left.Equals(right);
	public static bool operator !=(X86VendorId left, X86VendorId right) => !(left == right);
}
