using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using DeviceTools.HumanInterfaceDevices;
using Exo.Devices.Logitech.HidPlusPlus.FeatureAccessProtocol;
using Exo.Devices.Logitech.HidPlusPlus.RegisterAccessProtocol;

namespace Exo.Devices.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice : IDisposable, IAsyncDisposable
{
	/// <summary>Creates and initializes the engine from a HID device streams.</summary>
	/// <remarks>
	/// <para>
	/// This method has the ability to detect the HID++ protocol used by the device.
	/// It will do so when <paramref name="expectedProtocolFlavor"/> is set to <see cref="HidPlusPlusProtocolFlavor.Auto"/>.
	/// </para>
	/// <para>
	/// If will return an instance of <see cref="RegisterAccessDevice"/> for HID++ 1.0 (RAP) devices, and an instance of <see cref="FeatureAccessDevice"/> for HID++ 2.0 (FAP) devices.
	/// </para>
	/// <para>
	/// At least one of <paramref name="shortMessageStream"/> or <paramref name="longMessageStream"/> must be provided. <paramref name="veryLongMessageStream"/> is always optional.
	/// Very old devices may only support short messages, while many more recent will support both short and long messages, and some modern (HID++ 2.0) devices only support long messages.
	/// A device that does not support short messages must be based on HID++ 2.0 (Feature Access Protocol).
	/// </para>
	/// </remarks>
	/// <param name="shortMessageStream">A stream to use for communication with the device using short reports.</param>
	/// <param name="longMessageStream">A stream to use for communication with the device using long reports.</param>
	/// <param name="veryLongMessageStream">A stream to use for communication with the device using very long reports.</param>
	/// <param name="expectedProtocolFlavor">The protocol flavor expected to be supported by the device. It can be <see cref="HidPlusPlusProtocolFlavor.Unknown"/>.</param>
	/// <param name="softwareId">The software ID to use for tagging HID messages.</param>
	/// <returns>An instance of <see cref="HidPlusPlusDevice"/> that can be used to access HID++ features of the device.</returns>
	/// <exception cref="ArgumentNullException">A mandatory stream is missing.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An invalid protocol flavor was specified.</exception>
	public static async Task<HidPlusPlusDevice> CreateAsync
	(
		HidFullDuplexStream? shortMessageStream,
		HidFullDuplexStream? longMessageStream,
		HidFullDuplexStream? veryLongMessageStream,
		HidPlusPlusProtocolFlavor expectedProtocolFlavor,
		byte softwareId,
		TimeSpan requestTimeout
	)
	{
		if ((uint)expectedProtocolFlavor is >= (uint)HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess) throw new ArgumentOutOfRangeException(nameof(expectedProtocolFlavor));
		if (shortMessageStream is null && expectedProtocolFlavor is HidPlusPlusProtocolFlavor.RegisterAccess) throw new ArgumentNullException(nameof(shortMessageStream));

		try
		{
			// Creating the device should pose little to no problem, but we will do additional checks once the instance is created.
			var transport = new HidPlusPlusTransport(shortMessageStream, longMessageStream, veryLongMessageStream, softwareId, requestTimeout);
			try
			{
				// Protocol version check.
				// TODO: Make this check a bit better and explicitly list the supported versions.
				HidPlusPlusVersion protocolVersion;
				try
				{
					protocolVersion = await transport.GetProtocolVersionAsync(255, default).ConfigureAwait(false);
					transport.SetProtocolFlavor(255, HidPlusPlusProtocolFlavor.FeatureAccess);
				}
				catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocolErrorCode.InvalidSubId)
				{
					if (expectedProtocolFlavor is not HidPlusPlusProtocolFlavor.Unknown and not HidPlusPlusProtocolFlavor.RegisterAccess)
					{
						throw new Exception("Protocol flavor does not match.");
					}

					transport.SetProtocolFlavor(255, HidPlusPlusProtocolFlavor.RegisterAccess);
					return new RegisterAccessDevice(transport, 255);
				}

				if (expectedProtocolFlavor is not HidPlusPlusProtocolFlavor.Unknown and not HidPlusPlusProtocolFlavor.FeatureAccess)
				{
					throw new Exception("Protocol flavor does not match.");
				}
				else if (protocolVersion.Major >= 2 && protocolVersion.Major <= 4)
				{
					return new FeatureAccessDevice(transport, 255);
				}
				else
				{
					throw new Exception($"Unsupported protocol version: {protocolVersion.Major}.{protocolVersion.Minor}.");
				}
			}
			catch
			{
				await transport.DisposeAsync().ConfigureAwait(false);
				throw;
			}
		}
		catch
		{
			if (shortMessageStream is not null) await shortMessageStream.DisposeAsync().ConfigureAwait(false);
			if (longMessageStream is not null) await longMessageStream.DisposeAsync().ConfigureAwait(false);
			if (veryLongMessageStream is not null) await veryLongMessageStream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	protected HidPlusPlusTransport Transport { get; }
	protected byte DeviceIndex { get; }
	private readonly bool _shouldReleaseTransport;
	private readonly ConcurrentDictionary<byte, HidPlusPlusDevice> _devices;

	public abstract HidPlusPlusProtocolFlavor ProtocolFlavor { get; }

	private HidPlusPlusDevice(HidPlusPlusTransport transport, ConcurrentDictionary<byte, HidPlusPlusDevice> devices, byte deviceIndex, bool shouldReleaseTransport)
	{
		Transport = transport;
		_devices = devices;
		DeviceIndex = deviceIndex;
		_shouldReleaseTransport = shouldReleaseTransport;
	}

	private protected HidPlusPlusDevice(HidPlusPlusTransport transport, byte deviceIndex)
		: this(transport, new(), deviceIndex, true)
	{
	}

	private protected HidPlusPlusDevice(HidPlusPlusDevice @base, byte deviceIndex)
		: this(@base.Transport, @base._devices, deviceIndex, false)
	{
	}

	public void Dispose()
	{
		if (_shouldReleaseTransport)
		{
			Transport.Dispose();
		}
	}

	public ValueTask DisposeAsync() => _shouldReleaseTransport ? Transport.DisposeAsync() : ValueTask.CompletedTask;

	public HidPlusPlusDevice GetForDevice(byte deviceIndex)
		=> _devices.GetOrAdd(deviceIndex, CreateForDevice);

	protected abstract HidPlusPlusDevice CreateForDevice(byte deviceIndex);
}

public class RegisterAccessDevice : HidPlusPlusDevice
{
	public RegisterAccessDevice(HidPlusPlusTransport transport, byte deviceIndex)
		: base(transport, deviceIndex)
	{
	}

	private RegisterAccessDevice(RegisterAccessDevice @base, byte deviceIndex)
		: base(@base, deviceIndex)
	{
	}

	public override HidPlusPlusProtocolFlavor ProtocolFlavor => HidPlusPlusProtocolFlavor.RegisterAccess;

	public new RegisterAccessDevice GetForDevice(byte deviceIndex) => Unsafe.As<RegisterAccessDevice>(base.GetForDevice(deviceIndex));

	protected override HidPlusPlusDevice CreateForDevice(byte deviceIndex)
		=> new RegisterAccessDevice(this, DeviceIndex);
}

public class FeatureAccessDevice : HidPlusPlusDevice
{
	private ReadOnlyDictionary<HidPlusPlusFeature, byte>? _cachedFeatures;

	internal FeatureAccessDevice(HidPlusPlusTransport transport, byte deviceIndex)
		: base(transport, deviceIndex)
	{
	}

	private FeatureAccessDevice(FeatureAccessDevice @base, byte deviceIndex)
		: base(@base, deviceIndex)
	{
	}

	public override HidPlusPlusProtocolFlavor ProtocolFlavor => HidPlusPlusProtocolFlavor.FeatureAccess;

	public new FeatureAccessDevice GetForDevice(byte deviceIndex) => Unsafe.As<FeatureAccessDevice>(base.GetForDevice(deviceIndex));

	protected override HidPlusPlusDevice CreateForDevice(byte deviceIndex)
		=> new FeatureAccessDevice(this, DeviceIndex);

	public ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>> GetFeaturesAsync(CancellationToken cancellationToken)
		=> _cachedFeatures is not null ?
			new ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>>(_cachedFeatures) :
			new ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>>(GetFeaturesAsyncCore(cancellationToken));

	private async Task<ReadOnlyDictionary<HidPlusPlusFeature, byte>> GetFeaturesAsyncCore(CancellationToken cancellationToken)
	{
		var features = await Transport.GetFeaturesAsync(DeviceIndex, cancellationToken).ConfigureAwait(false);

		Volatile.Write(ref _cachedFeatures, features);

		return features;
	}

	public Task<byte> GetFeatureIndexAsync(HidPlusPlusFeature feature, CancellationToken cancellationToken)
		=> Transport.GetFeatureIndexAsync(DeviceIndex, feature, cancellationToken);

	public Task<TResponseParameters> SendAsync<TResponseParameters>
	(
		byte featureIndex,
		byte functionId,
		CancellationToken cancellationToken
	)
		where TResponseParameters : struct, IMessageResponseParameters
		=> Transport.FeatureAccessSendAsync<TResponseParameters>(DeviceIndex, featureIndex, functionId, cancellationToken);

	public Task SendAsync<TRequestParameters>
	(
		byte featureIndex,
		byte functionId,
		in TRequestParameters requestParameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageRequestParameters
		=> Transport.FeatureAccessSendAsync(DeviceIndex, featureIndex, functionId, requestParameters, cancellationToken);

	public Task<TResponseParameters> SendAsync<TRequestParameters, TResponseParameters>
	(
		byte featureIndex,
		byte functionId,
		in TRequestParameters requestParameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageRequestParameters
		where TResponseParameters : struct, IMessageResponseParameters
		=> Transport.FeatureAccessSendAsync<TRequestParameters, TResponseParameters>(DeviceIndex, featureIndex, functionId, requestParameters, cancellationToken);
}
