using System.Collections.Immutable;
using Exo.Images;
using Exo.Monitors;

namespace Exo.Features.EmbeddedMonitors;

/// <summary>To be used for a device exposing multiple embedded monitors.</summary>
/// <remarks>
/// <para>This feature is necessary to support devices such as the various Elgato StreamDecks.</para>
/// <para>This feature is exclusive with <see cref="IEmbeddedMonitorFeature"/>.</para>
/// </remarks>
public interface IEmbeddedMonitorControllerFeature : IEmbeddedMonitorDeviceFeature
{
	/// <summary>Gets a list of the embedded monitors exposed by this device.</summary>
	ImmutableArray<IEmbeddedMonitor> EmbeddedMonitors { get; }
}

/// <summary>A feature to implement for a device supporting an automatic screensaver.</summary>
/// <remarks></remarks>
public interface IEmbeddedMonitorScreenSaverFeature : IEmbeddedMonitor, IEmbeddedMonitorDeviceFeature
{
}

public interface IEmbeddedMonitor
{
	/// <summary>Gets the monitor ID.</summary>
	/// <remarks>This property is especially important for devices exposing multiple monitors.</remarks>
	Guid MonitorId { get; }
	/// <summary>Gets information about the embedded monitor.</summary>
	EmbeddedMonitorInformation MonitorInformation { get; }
	/// <summary>Sets the image of a monitor.</summary>
	/// <remarks>
	/// <para>
	/// Images provided must strictly be in a format supported by the monitor.
	/// Implementations are <em>not</em> requested to do any kind of conversion to a supported image format.
	/// Implementations are <em>not</em> requested to do any kind of resizing.
	/// </para>
	/// <para>
	/// JPEG and similar formats are a bit special in that they would generally always represent R8G8B8 data but will be decoded by the device in their format of choice.
	/// No monitor is ever expected to support transparency, even if they do make use of an extra color channel. (e.g. for alignment purposes)
	/// </para>
	/// <para>
	/// The image ID that is provided to this method can be used by the implementation to avoid unnecessary I/O if data for the same image ID has already been uploaded.
	/// It is up to the caller to determine how to manage their image IDs, but the same ID should never be used for two different images.
	/// An auto-incremented ID is a perfectly fine minimal implementation, in this case considering all images as different even while they might actually be identical.
	/// To be noted though, collisions are likely to not be a real problem as long as caller can guarantee that they happen rarely enough.
	/// For example, if a collision is guaranteed to not occur in a lifetime of 100 new ID generations, the devices are unlikely to remember the previous data associated with that ID.
	/// A 128 bit value is used in order to easily allow for callers to implement a more complex hashing system, for example using XXH128, in order to optimize operations on their side.
	/// This is intended to be done in the image service of Exo.
	/// </para>
	/// <para>
	/// Calling this method will switch the monitor to custom graphics and set <see cref="IEmbeddedMonitorBuiltInGraphicModes.CurrentModeId"/> to <see cref="string.Empty"/>, if the
	/// monitor supports built-in modes.
	/// </para>
	/// </remarks>
	/// <param name="imageId">An opaque image ID used to identify an image.</param>
	/// <param name="imageFormat">The image format of the specified data.</param>
	/// <param name="data">Valid image data in the format specified by <paramref name="imageFormat"/>.</param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	/// <exception cref="NotSupportedException">If the monitor only supports built-in graphics modes, offering no ability to show a custom image.</exception>
	ValueTask SetImageAsync(UInt128 imageId, ImageFormat imageFormat, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}

public interface IDrawableEmbeddedMonitor : IEmbeddedMonitor
{
	/// <summary>Draws the specified image in the specified region of a monitor.</summary>
	/// <remarks>
	/// <para>
	/// This call is useful to update part of the framebuffer of a device supporting such partial updates.
	/// It is especially desirable as images might be generated on-the-fly, as it will reduce CPU usage of said images.
	/// </para>
	/// <para>
	/// Constraints on the drawn image are the same as for <see cref="IEmbeddedMonitor.SetImageAsync(UInt128, ImageFormat, ReadOnlyMemory{byte}, CancellationToken)"/>.
	/// It is expected that embedded monitors that support partial drawing do not support any kind of animated image format, as the two purposes would conflict.
	/// </para>
	/// <para>
	/// The provided image ID is likely to be less useful in this call, however it is provided for consistency with the
	/// <see cref="IEmbeddedMonitor.SetImageAsync(UInt128, ImageFormat, ReadOnlyMemory{byte}, CancellationToken)"/> call.
	/// Implementations can decide to make use of it as they wish, with exactly the same rules and expectations.
	/// </para>
	/// </remarks>
	/// <param name="position">The position at which to draw the image.</param>
	/// <param name="size">The size of the region where the image will be drawn.</param>
	/// <param name="imageId">An opaque image ID used to identify an image.</param>
	/// <param name="imageFormat">The image format of the specified data.</param>
	/// <param name="data">Valid image data in the format specified by <paramref name="imageFormat"/>.</param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	ValueTask DrawImageAsync(Point position, Size size, UInt128 imageId, ImageFormat imageFormat, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}

/// <summary>A feature to implement for an embedded monitor supporting built-in graphics.</summary>
/// <remarks>In absence of this interface, embedded monitors are assumed to support only <see cref="EmbeddedMonitorGraphicsDescription.CustomGraphics"/>.</remarks>
public interface IEmbeddedMonitorBuiltInGraphics : IEmbeddedMonitor
{
	/// <summary>Gets the array of built-in graphics supported by this monitor.</summary>
	/// <remarks>
	/// The returned array must contain at least one element and must not contain duplicate mode IDs.
	/// If the monitor supports showing custom images, as would typically be the case, this must explicitly contain <see cref="EmbeddedMonitorGraphicsDescription.CustomGraphics"/>.
	/// </remarks>
	ImmutableArray<EmbeddedMonitorGraphicsDescription> SupportedGraphics { get; }

	/// <summary>Gets the ID of the currently displayed graphics.</summary>
	Guid CurrentGraphicsId { get; }

	/// <summary>Sets the current built-in graphics mode on the monitor.</summary>
	/// <remarks>
	/// This method can not be used to switch to the custom graphics.
	/// To that effect, one must call <see cref="IEmbeddedMonitor.SetImageAsync(UInt128, ImageFormat, ReadOnlyMemory{byte}, CancellationToken)"/>.
	/// </remarks>
	/// <param name="modeId"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	ValueTask SetCurrentModeAsync(Guid modeId, CancellationToken cancellationToken);
}

public readonly record struct EmbeddedMonitorInformation
{
	public EmbeddedMonitorInformation(MonitorShape shape, Size imageSize, PixelFormat pixelFormat, ImageFormats supportedImageFormats, bool hasAnimationSupport)
	{
		Shape = shape;
		ImageSize = imageSize;
		PixelFormat = pixelFormat;
		SupportedImageFormats = supportedImageFormats;
		HasAnimationSupport = hasAnimationSupport;
	}

	/// <summary>Gets the shape of the monitor.</summary>
	/// <remarks>
	/// Some AIO devices will expose a circular screen, but most embedded monitors are expected to be of rectangular shape.
	/// The shape of the monitor might mainly be used to optimize image compression if the monitor is non-rectangular.
	/// </remarks>
	public MonitorShape Shape { get; }
	/// <summary>Gets the image size of the monitor.</summary>
	public Size ImageSize { get; }
	/// <summary>Gets the effective pixel format of the monitor.</summary>
	/// <remarks>
	/// Monitors should generally support a 32 bits RGB(A) format, but this information is needed in order to feed acceptable images to the device.
	/// This is especially important in case of raw images, but it will matter in other situations, such as when only a reduced number of colors is supported.
	/// </remarks>
	public PixelFormat PixelFormat { get; }
	/// <summary>Gets a description of the image formats that are directly supported by the embedded monitor.</summary>
	public ImageFormats SupportedImageFormats { get; }
	/// <summary>Indicates whether the monitor has hardware support for animated images.</summary>
	/// <remarks>Animated images are only expected to be supported for full refreshes.</remarks>
	public bool HasAnimationSupport { get; }
}

public readonly struct EmbeddedMonitorGraphicsDescription
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
	/// <param name="modeAndNameStringId">The unique ID that will be used to reference both the graphics and the name.</param>
	public EmbeddedMonitorGraphicsDescription(Guid modeAndNameStringId)
		: this(modeAndNameStringId, modeAndNameStringId) { }

	public EmbeddedMonitorGraphicsDescription(Guid modeId, Guid nameStringId)
	{
		GraphicsId = modeId;
		NameStringId = nameStringId;
	}
}
