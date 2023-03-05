using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice : IDisposable, IAsyncDisposable
{
	private readonly struct ProductCategoryRange
	{
		public readonly ushort Start;
		public readonly ushort End;
		public readonly ProductCategory Category;

		public ProductCategoryRange(ushort start, ushort end, ProductCategory category)
		{
			Start = start;
			End = end;
			Category = category;
		}
	}

	// Logitech Unifying extension for Google Chrome gives a rudimentary mapping between product IDs and categories.
	// This is quite old, so it might not be perfect, but we can build on it to keep a relatively up-to-date mapping.
	// From this mapping, we can infer if the device is corded, wireless, or a receiver.
	// For HID++ 1.0, the device index to use for communicating with the device itself will be 0 for corded devices, but 255 for receivers.
	// For HID++ 2.0, it should always be 255.
	private static readonly ProductCategoryRange[] ProductIdCategoryMappings = new ProductCategoryRange[]
	{
		new(0x0000, 0x00FF, ProductCategory.VirtualUsbGameController),
		new(0x0400, 0x040F, ProductCategory.UsbScanner),
		new(0x0800, 0x08FF, ProductCategory.UsbCamera),
		new(0x0900, 0x09FF, ProductCategory.UsbCamera),
		new(0x0A00, 0x0AFF, ProductCategory.UsbAudio),
		new(0x0B00, 0x0BFF, ProductCategory.UsbHub),
		new(0x1000, 0x1FFF, ProductCategory.QuadMouse),
		new(0x2000, 0x2FFF, ProductCategory.QuadKeyboard),
		new(0x3000, 0x3FFF, ProductCategory.QuadGamingDevice),
		new(0x4000, 0x4FFF, ProductCategory.QuadFapDevice),
		new(0x5000, 0x5FFF, ProductCategory.UsbToolsTransceiver),
		new(0x8000, 0x87FF, ProductCategory.QuadMouseTransceiver),
		new(0x8800, 0x88FF, ProductCategory.QuadDesktopTransceiver),
		new(0x8900, 0x89FF, ProductCategory.UsbCamera),
		new(0x8A00, 0x8FFF, ProductCategory.QuadDesktopTransceiver),
		new(0x9000, 0x98FF, ProductCategory.QuadGamingTransceiver),
		new(0x9900, 0x99FF, ProductCategory.UsbCamera),
		new(0x9A00, 0x9FFF, ProductCategory.QuadGamingTransceiver),
		new(0xA000, 0xAFFF, ProductCategory.UsbSpecial),
		new(0xB000, 0xB0FF, ProductCategory.BluetoothMouse),
		new(0xB300, 0xB3DF, ProductCategory.BluetoothKeyboard),
		new(0xB3E0, 0xB3FF, ProductCategory.BluetoothNumpad),
		new(0xB400, 0xB4FF, ProductCategory.BluetoothRemoteControl),
		new(0xB500, 0xB5FF, ProductCategory.BluetoothReserved),
		new(0xBA00, 0xBAFF, ProductCategory.BluetoothAudio),
		new(0xC000, 0xC0FF, ProductCategory.UsbMouse),
		new(0xC100, 0xC1FF, ProductCategory.UsbRemoteControl),
		new(0xC200, 0xC2FF, ProductCategory.UsbPcGamingDevice),
		new(0xC300, 0xC3FF, ProductCategory.UsbKeyboard),
		new(0xC400, 0xC4FF, ProductCategory.UsbTrackBall),
		new(0xC500, 0xC5FF, ProductCategory.UsbReceiver),
		new(0xC600, 0xC6FF, ProductCategory.Usb3dControlDevice),
		new(0xC700, 0xC7FF, ProductCategory.UsbBluetoothReceiver),
		new(0xC800, 0xC8FF, ProductCategory.UsbOtherPointingDevice),
		new(0xCA00, 0xCCFF, ProductCategory.UsbConsoleGamingDevice),
		new(0xD000, 0xD00F, ProductCategory.UsbCamera),
		new(0xF000, 0xF00F, ProductCategory.UsbToolsTransceiver),
		new(0xF010, 0xF010, ProductCategory.UsbToolsCorded),
		new(0xF011, 0xFFFF, ProductCategory.UsbToolsTransceiver),
	};

	/// <summary>Tries to infer the logitech product category from the Product ID.</summary>
	/// <remarks>
	/// <para>
	/// The results returned by this method can't be guaranteed to be 100% exact, as it is based on static data and we can't predict the future.
	/// However, in many cases, logitech should stick to the current scheme, and this will work on many newer products.
	/// </para>
	/// <para>
	/// Currently, IDs are shared between USB, Bluetooth and WPID (Wireless Product ID) used by the Unifying (Quad) protocol.
	/// In the case of Bluetooth, HID++ devices are explicitly using USB VID/PIDs. This is permitted by Bluetooth,
	/// which allows referencing Vendor IDs either from a separate Bluetooth ID space, or from the USB ID space.
	/// </para>
	/// </remarks>
	/// <param name="productId">The product ID.</param>
	/// <param name="category">The detected product category.</param>
	/// <returns><c>true</c> if the product category could be inferred from known data; otherwise <c>false</c>.</returns>
	public static bool TryInferProductCategory(ushort productId, out ProductCategory category)
	{
		int min = 0;
		int max = ProductIdCategoryMappings.Length - 1;

		while (min <= max)
		{
			int med = (min + max) / 2;

			var item = ProductIdCategoryMappings[med];

			if (productId >= item.Start)
			{
				if (productId <= item.End)
				{
					category = item.Category;
					return true;
				}
				else
				{
					min = med + 1;
				}
			}
			else
			{
				max = med - 1;
			}
		}

		category = ProductCategory.Other;
		return false;
	}

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
		ushort productId,
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

	public Task<TResponseParameters> RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>
	(
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, IMessageParameters
		=> Transport.RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, cancellationToken);

	public Task<TResponseParameters> RegisterAccessGetShortRegisterAsync<TRequestParameters, TResponseParameters>
	(
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, IShortMessageParameters
		=> Transport.RegisterAccessGetShortRegisterAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, cancellationToken);

	public Task<TResponseParameters> RegisterAccessGetLongRegisterAsync<TRequestParameters, TResponseParameters>
	(
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, ILongMessageParameters
		=> Transport.RegisterAccessGetLongRegisterAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, cancellationToken);

	public Task<TResponseParameters> RegisterAccessGetVeryLongRegisterAsync<TRequestParameters, TResponseParameters>
	(
		Address address,
		in TRequestParameters parameters,
		CancellationToken cancellationToken
	)
		where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
		where TResponseParameters : struct, IVeryLongMessageParameters
		=> Transport.RegisterAccessGetVeryLongRegisterAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, cancellationToken);
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
