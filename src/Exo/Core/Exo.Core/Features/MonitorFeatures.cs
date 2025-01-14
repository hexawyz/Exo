using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
using Exo.Images;
using Exo.Monitors;

namespace Exo.Features.Monitors;

/// <summary>This feature allows to retrieve the raw capabilities of the monitor, as cached by the driver.</summary>
public interface IMonitorRawCapabilitiesFeature : IMonitorDeviceFeature
{
	ReadOnlySpan<byte> RawCapabilities { get; }
}

/// <summary>This feature allows to retrieve the capabilities of the monitor.</summary>
/// <remarks>These capabilities do not necessarily map to the raw capabilities value returned by the monitor.</remarks>
public interface IMonitorCapabilitiesFeature : IMonitorDeviceFeature
{
	MonitorCapabilities Capabilities { get; }
}

public interface IMonitorRawVcpFeature : IMonitorDeviceFeature
{
	ValueTask SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken);
	ValueTask<VcpFeatureReply> GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken);
}

public readonly record struct ContinuousValue : IEquatable<ContinuousValue>
{
	public ContinuousValue(ushort current, ushort minimum, ushort maximum)
	{
		Current = current;
		Minimum = minimum;
		Maximum = maximum;
	}

	public ushort Current { get; init; }
	public ushort Minimum { get; init; }
	public ushort Maximum { get; init; }
}

/// <summary>Provides information about a non continuous value, such as an input source.</summary>
/// <remarks>
/// While there is a standard for some non-continuous settings, it is not always exhaustive and various implementations can decide to extend it.
/// As such, we provide explicit information about non-continuous values, with the best of our knowledge.
/// </remarks>
public readonly struct NonContinuousValueDescription
{
	public NonContinuousValueDescription(ushort value, Guid nameIdString, string? customName)
	{
		Value = value;
		NameStringId = nameIdString;
		CustomName = customName;
	}

	/// <summary>Gets the value associated with this input source.</summary>
	public ushort Value { get; }
	/// <summary>Gets the name string ID for this value.</summary>
	/// <remarks>
	/// Non-continuous values that have been mapped by an explicit monitor description should generally provide this value.
	/// If possible, the name will be provided based on the values defined in the standard.
	/// If this ID is not provided, the value of the property will be <see cref="Guid.Empty"/>.
	/// </remarks>
	public Guid NameStringId { get; }
	/// <summary>Gets the custom name of the value.</summary>
	/// <remarks>
	/// Although rarely used, if used at all, monitors can provide custom name for non continuous values as part of their capabilities string.
	/// If this were to be the case, this property will contain the value contained in the capabilities string.
	/// </remarks>
	public string? CustomName { get; }
}

public interface IContinuousVcpFeature
{
	ValueTask<ContinuousValue> GetValueAsync(CancellationToken cancellationToken);
	ValueTask SetValueAsync(ushort value, CancellationToken cancellationToken);
}

public interface INonContinuousVcpFeature
{
	ImmutableArray<NonContinuousValueDescription> AllowedValues { get; }
	ValueTask<ushort> GetValueAsync(CancellationToken cancellationToken);
	ValueTask SetValueAsync(ushort value, CancellationToken cancellationToken);
}

public interface IBooleanVcpFeature
{
	ValueTask<bool> GetValueAsync(CancellationToken cancellationToken);
	ValueTask SetValueAsync(bool value, CancellationToken cancellationToken);
}

public interface IMonitorBrightnessFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorContrastFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorSharpnessFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorRedVideoGainFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorGreenVideoGainFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorBlueVideoGainFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorRedVideoBlackLevelFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorGreenVideoBlackLevelFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorBlueVideoBlackLevelFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorRedSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorYellowSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorGreenSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorCyanSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorBlueSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorMagentaSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorRedSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorYellowSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorGreenSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorCyanSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorBlueSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorMagentaSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }

public interface IMonitorInputSelectFeature : IMonitorDeviceFeature, INonContinuousVcpFeature { }
public interface IMonitorSpeakerAudioVolumeFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }

public interface IMonitorOsdLanguageFeature : IMonitorDeviceFeature, INonContinuousVcpFeature { }
public interface IMonitorResponseTimeFeature : IMonitorDeviceFeature, INonContinuousVcpFeature { }
public interface IMonitorInputLagFeature : IMonitorDeviceFeature, INonContinuousVcpFeature { }
public interface IMonitorBlueLightFilterLevelFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorPowerIndicatorToggleFeature : IMonitorDeviceFeature, IBooleanVcpFeature { }

public interface IEmbeddedMonitorFeature : IEmbeddedMonitor, IMonitorDeviceFeature
{
}

/// <summary>To be used for a device exposing multiple embedded monitors.</summary>
/// <remarks>
/// <para>This feature is necessary to support devices such as the various Elgato StreamDecks.</para>
/// <para>THis feature is exclusive with <see cref="IEmbeddedMonitorFeature"/>.</para>
/// </remarks>
public interface IEmbeddedMonitorControllerFeature : IMonitorDeviceFeature
{
	/// <summary>Gets a list of the embedded monitors exposed by this device.</summary>
	ImmutableArray<IEmbeddedMonitor> EmbeddedMonitors { get; }
}

public interface IEmbeddedMonitorScreenSaverFeature : IEmbeddedMonitor, IMonitorDeviceFeature
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
	/// </remarks>
	/// <param name="imageId">An opaque image ID used to identify an image.</param>
	/// <param name="imageFormat">The image format of the specified data.</param>
	/// <param name="data">Valid image data in the format specified by <paramref name="imageFormat"/>.</param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	ValueTask SetImageAsync(UInt128 imageId, ImageFormat imageFormat, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}

public interface IDrawableEmbeddedMonitor
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
