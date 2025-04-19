using System.Text.Json.Serialization;

namespace Exo.EmbeddedMonitors;

public readonly struct EmbeddedMonitorGraphicsDescription : IEquatable<EmbeddedMonitorGraphicsDescription>
{
	/// <summary>Gets a default definition of the custom graphics.</summary>
	/// <remarks>
	/// Unless it is not supported by the monitor, custom graphics is always assumed to be the default mode.
	/// To that effect, the <see cref="Guid"/> associated with it is <see cref="Guid.Empty"/>.
	/// </remarks>
	public static EmbeddedMonitorGraphicsDescription CustomGraphics => new(default, new Guid(0x2F538961, 0xDAF9, 0x4664, 0x87, 0xFA, 0x22, 0xB8, 0x48, 0xDF, 0x0E, 0xEC));

	public static Guid OffId => new(0xA93C0E79, 0xB47E, 0x4542, 0xBF, 0x77, 0xC0, 0x06, 0xE4, 0xFF, 0xFB, 0x6D);

	/// <summary>Gets a definition to represent a graphics off mode.</summary>
	/// <remarks>Embedded monitors supporting a built-in "graphics off" mode can add this to the list of modes.</remarks>
	// NB: References the shared "off" string, to avoid useless duplication. may be changed later.
	public static EmbeddedMonitorGraphicsDescription Off => new(OffId, new Guid(0xA9F9A2E6, 0x2091, 0x4BD9, 0xB1, 0x35, 0xA4, 0xA5, 0xD6, 0xD4, 0x00, 0x9E));

	/// <summary>Gets the ID used to describe these graphics.</summary>
	/// <remarks>
	/// This property can be <see cref="Guid.Empty"/> to represent custom graphics, which is assumed to be default in all cases where it is supported.
	/// </remarks>
	public Guid GraphicsId { get; }
	/// <summary>Gets the name string ID for these graphics.</summary>
	public Guid NameStringId { get; }

	/// <summary>Initializes the structure using the same <see cref="Guid"/> for both graphics ID and name string ID.</summary>
	/// <remarks>
	/// In many cases, graphics ID would be unique GUIDs, so it is not reasonable to use the same <see cref="Guid"/> value for both the name and the graphics themselves.
	/// From a logic POV it does not change much, but it is more convenient for implementation to just require a single GUID.
	/// Of course, the constructor allowing for the use of two different GUIDs is still available for implementation that want more complex binding.
	/// </remarks>
	/// <param name="graphicsAndNameStringId">The unique ID that will be used to reference both the graphics and the name.</param>
	public EmbeddedMonitorGraphicsDescription(Guid graphicsAndNameStringId)
		: this(graphicsAndNameStringId, graphicsAndNameStringId) { }

	[JsonConstructor]
	public EmbeddedMonitorGraphicsDescription(Guid graphicsId, Guid nameStringId)
	{
		GraphicsId = graphicsId;
		NameStringId = nameStringId;
	}

	public override bool Equals(object? obj) => obj is EmbeddedMonitorGraphicsDescription description && Equals(description);
	public bool Equals(EmbeddedMonitorGraphicsDescription other) => GraphicsId == other.GraphicsId && NameStringId == other.NameStringId;
	public override int GetHashCode() => HashCode.Combine(GraphicsId, NameStringId);

	public static bool operator ==(EmbeddedMonitorGraphicsDescription left, EmbeddedMonitorGraphicsDescription right) => left.Equals(right);
	public static bool operator !=(EmbeddedMonitorGraphicsDescription left, EmbeddedMonitorGraphicsDescription right) => !(left == right);
}
