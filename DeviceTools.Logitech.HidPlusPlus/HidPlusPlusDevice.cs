using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Registers;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice : IAsyncDisposable
{
	private enum RegisterAccessDeviceKind
	{
		Default = 0,
		Receiver = 1,
		UnifyingReceiver = 2,
		BoltReceiver = 3,
	}

	private enum DeviceEventKind : byte
	{
		DeviceDiscovered = 0,
		DeviceConnected = 1,
		DeviceDisconnected = 2,
	}

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
		string? externalFriendlyName,
		TimeSpan requestTimeout,
		CancellationToken cancellationToken
	)
	{
		if ((uint)expectedProtocolFlavor is >= (uint)HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess) throw new ArgumentOutOfRangeException(nameof(expectedProtocolFlavor));
		if (shortMessageStream is null && expectedProtocolFlavor is HidPlusPlusProtocolFlavor.RegisterAccess) throw new ArgumentNullException(nameof(shortMessageStream));

		try
		{
			// Creating the device should pose little to no problem, but we will do additional checks once the instance is created.
			var transport = new HidPlusPlusTransport(shortMessageStream, longMessageStream, veryLongMessageStream, softwareId, requestTimeout);
			HidPlusPlusDevice device;
			try
			{
				device = await CreateAsync
				(
					null,
					transport,
					expectedProtocolFlavor,
					productId,
					255,
					default,
					externalFriendlyName,
					null,
					HidPlusPlusTransportExtensions.DefaultRetryCount,
					cancellationToken
				).ConfigureAwait(false);
			}
			catch
			{
				await transport.DisposeAsync().ConfigureAwait(false);
				throw;
			}

			try
			{
				await device.InitializeAsync(HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken).ConfigureAwait(false);
				return device;
			}
			catch
			{
				await device.DisposeAsync().ConfigureAwait(false);
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

	private static async Task<HidPlusPlusDevice> CreateAsync
	(
		RegisterAccessReceiver? parent,
		HidPlusPlusTransport transport,
		HidPlusPlusProtocolFlavor expectedProtocolFlavor,
		ushort productId,
		byte deviceIndex,
		DeviceConnectionInfo deviceInfo, // In the case of a receiver, information obtained when the device was discovered.
		string? externalFriendlyName,
		string? serialNumberFromReceiverPairing,
		int retryCount,
		CancellationToken cancellationToken
	)
	{
		if (parent is not null ?
			expectedProtocolFlavor is not HidPlusPlusProtocolFlavor.RegisterAccess and not HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess :
			expectedProtocolFlavor is HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess)
		{
			throw new ArgumentOutOfRangeException(nameof(expectedProtocolFlavor));
		}

		// Protocol version check.
		HidPlusPlusVersion protocolVersion;

		if (parent is null || deviceInfo.IsLinkEstablished)
		{
			protocolVersion = await GetVersionAsync(transport, deviceIndex, retryCount, cancellationToken).ConfigureAwait(false);
		}
		else if (expectedProtocolFlavor is HidPlusPlusProtocolFlavor.RegisterAccess)
		{
			protocolVersion = new HidPlusPlusVersion(1, 0);
		}
		else if (expectedProtocolFlavor is HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess)
		{
			// This will not always match the exact protocol version, but it will be enough until the device is connected.
			protocolVersion = new HidPlusPlusVersion(2, 0);
		}
		else
		{
			throw new InvalidOperationException("Trying to instantiate a child device with an unsupported protocol flavor.");
		}

		if (protocolVersion.Major == 1 && protocolVersion.Minor == 0)
		{
			if (expectedProtocolFlavor is not HidPlusPlusProtocolFlavor.Unknown and not HidPlusPlusProtocolFlavor.RegisterAccess) goto ProtocolFlavorMismatch;

			transport.Devices[deviceIndex].SetProtocolFlavor(HidPlusPlusProtocolFlavor.RegisterAccess);

			return await CreateRegisterAccessAsync
			(
				parent,
				transport,
				productId,
				deviceIndex,
				deviceInfo,
				externalFriendlyName,
				serialNumberFromReceiverPairing,
				retryCount,
				cancellationToken
			).ConfigureAwait(false);
		}
		else if (protocolVersion.Major is >= 2 and <= 4)
		{
			if (expectedProtocolFlavor is HidPlusPlusProtocolFlavor.RegisterAccess) goto ProtocolFlavorMismatch;

			transport.Devices[deviceIndex].SetProtocolFlavor(expectedProtocolFlavor is HidPlusPlusProtocolFlavor.Unknown ? HidPlusPlusProtocolFlavor.FeatureAccess : expectedProtocolFlavor);

			return await CreateFeatureAccessAsync
			(
				parent,
				transport,
				productId,
				deviceIndex,
				deviceInfo,
				externalFriendlyName,
				serialNumberFromReceiverPairing,
				retryCount,
				cancellationToken
			).ConfigureAwait(false);
		}
		else
		{
			throw new Exception($"Unsupported protocol version: {protocolVersion.Major}.{protocolVersion.Minor}.");
		}

	ProtocolFlavorMismatch:;
		throw new Exception("Protocol flavor does not match.");
	}

	private static async Task<HidPlusPlusVersion> GetVersionAsync(HidPlusPlusTransport transport, byte deviceIndex, int retryCount, CancellationToken cancellationToken)
	{
		try
		{
			return await transport.GetProtocolVersionWithRetryAsync(deviceIndex, retryCount, cancellationToken).ConfigureAwait(false);
		}
		catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.InvalidSubId)
		{
			return new HidPlusPlusVersion(1, 0);
		}
	}

	private static async Task<HidPlusPlusDevice> CreateRegisterAccessAsync
	(
		RegisterAccessReceiver? parent,
		HidPlusPlusTransport transport,
		ushort productId,
		byte deviceIndex,
		DeviceConnectionInfo deviceInfo,
		string? externalFriendlyName,
		string? serialNumberFromReceiverPairing,
		int retryCount,
		CancellationToken cancellationToken
	)
	{
		// Handling of HID++ 1.0 devices seems to be way more complex, as the standard is not as strictly enforced, and there doesn't seem to be a way to get information of the connected device ?
		// i.e. We can know if the device is a receiver from the Product ID, but that's about it ?

		string? friendlyName = externalFriendlyName;
		var deviceKind = RegisterAccessDeviceKind.Default;

		TryInferProductCategory(productId, out var productCategory);

		if (productCategory == ProductCategory.UsbReceiver)
		{
			deviceKind = RegisterAccessDeviceKind.Receiver;
		}

		if (parent is null && productCategory != ProductCategory.Other)
		{
			deviceInfo = new DeviceConnectionInfo(productCategory.InferDeviceType(), 0);
		}
		string? serialNumber = serialNumberFromReceiverPairing;

		try
		{
			// Unifying receivers and some other should answer to this relatively undocumented call that will provide the "serial number" among other things.
			// We can find trace of this in the logitech Unifying chrome extension, where the serial number is also called base address. (A radio thing?)
			var receiverInformation = await transport.RegisterAccessGetLongRegisterWithRetryAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.ReceiverInformationResponse>
			(
				deviceIndex,
				Address.NonVolatileAndPairingInformation,
				new NonVolatileAndPairingInformation.Request(NonVolatileAndPairingInformation.Parameter.ReceiverInformation),
				retryCount,
				cancellationToken
			).ConfigureAwait(false);

			serialNumber = FormatReceiverSerialNumber(productId, receiverInformation.SerialNumber);

			// TODO: Don't hardcode Unifying Receivers product IDs if possible. (Can they be auto-detected reliably ?)
			if (productId is 0xC52B or 0xC52B or 0xC531 or 0xC532 or 0xC534)
			{
				deviceKind = RegisterAccessDeviceKind.UnifyingReceiver;
				// Hardcode the device name because there isn't a way to retrieve it otherwise? (Or is there?)
				friendlyName = "Logi Unifying Receiver";
			}
		}
		catch (HidPlusPlus1Exception ex) when (ex.ErrorCode is RegisterAccessProtocol.ErrorCode.InvalidAddress or RegisterAccessProtocol.ErrorCode.InvalidParameter)
		{
		}

		if (serialNumber is null && deviceKind is RegisterAccessDeviceKind.Receiver)
		{
			try
			{
				var boltSerialNumberResponse = await transport.RegisterAccessGetLongRegisterWithRetryAsync<BoltSerialNumber.Response>
				(
					deviceIndex,
					Address.BoltSerialNumber,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				serialNumber = boltSerialNumberResponse.ToString();

				deviceKind = RegisterAccessDeviceKind.BoltReceiver;
				// Hardcode the device name because there isn't a way to retrieve it otherwise? (Or is there?)
				friendlyName = "Logi Bolt Receiver";
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode is RegisterAccessProtocol.ErrorCode.InvalidAddress or RegisterAccessProtocol.ErrorCode.InvalidParameter)
			{
			}
		}

		if (parent is not null && deviceKind != RegisterAccessDeviceKind.Default) throw new InvalidOperationException($"A receiver cannot be paired to another receiver. (Product ID {productId}");

		HidPlusPlusDevice device = deviceKind switch
		{
			RegisterAccessDeviceKind.Default => parent is null ?
				new RegisterAccessDirect(transport, productId, deviceIndex, deviceInfo, friendlyName, serialNumber) :
				new RegisterAccessThroughReceiver(parent, productId, deviceIndex, deviceInfo, friendlyName, serialNumber),
			RegisterAccessDeviceKind.Receiver => new RegisterAccessReceiver(transport, productId, deviceIndex, deviceInfo, friendlyName, serialNumber),
			RegisterAccessDeviceKind.UnifyingReceiver => new RegisterAccessReceiver(transport, productId, deviceIndex, deviceInfo, friendlyName, serialNumber),
			RegisterAccessDeviceKind.BoltReceiver => new BoltReceiver(transport, productId, deviceIndex, deviceInfo, friendlyName, serialNumber),
			_ => throw new InvalidOperationException(),
		};

		return device;
	}

	private static async Task<HidPlusPlusDevice> CreateFeatureAccessAsync
	(
		RegisterAccessReceiver? parent,
		HidPlusPlusTransport transport,
		ushort productId,
		byte deviceIndex,
		DeviceConnectionInfo deviceInfo,
		string? externalFriendlyName,
		string? serialNumberFromReceiverPairing,
		int retryCount,
		CancellationToken cancellationToken
	)
	{
		string? friendlyName = externalFriendlyName;
		string? serialNumber = serialNumberFromReceiverPairing;
		FeatureAccessProtocol.DeviceType deviceType = FeatureAccessProtocol.DeviceType.Unknown;

		// Try to map from HID++ 1.0 device type to HID++ 2.0 device type. It is a best effort before the actual device is queried.
		if (parent is not null)
		{
			deviceType = deviceInfo.DeviceType switch
			{
				RegisterAccessProtocol.DeviceType.Keyboard => FeatureAccessProtocol.DeviceType.Keyboard,
				RegisterAccessProtocol.DeviceType.Mouse => FeatureAccessProtocol.DeviceType.Mouse,
				RegisterAccessProtocol.DeviceType.Numpad => FeatureAccessProtocol.DeviceType.Numpad,
				RegisterAccessProtocol.DeviceType.Presenter => FeatureAccessProtocol.DeviceType.Presenter,
				RegisterAccessProtocol.DeviceType.Trackball => FeatureAccessProtocol.DeviceType.Trackball,
				RegisterAccessProtocol.DeviceType.Touchpad => FeatureAccessProtocol.DeviceType.Trackpad,
				_ => FeatureAccessProtocol.DeviceType.Unknown
			};
		}

		ReadOnlyDictionary<HidPlusPlusFeature, byte>? features = null;

		if (parent is null || deviceInfo.IsLinkEstablished)
		{
			features = await transport.GetFeaturesWithRetryAsync(deviceIndex, retryCount, cancellationToken).ConfigureAwait(false);

			var (retrievedType, retrievedName) = await FeatureAccessGetDeviceNameAndTypeAsync(transport, features, deviceIndex, retryCount, cancellationToken);

			if (retrievedName is not null)
			{
				deviceType = retrievedType;
				friendlyName = retrievedName;
			}

			if (features.TryGetValue(HidPlusPlusFeature.DeviceInformation, out byte featureIndex))
			{
				var deviceInfoResponse = await transport.FeatureAccessSendWithRetryAsync<DeviceInformation.GetDeviceInfo.Response>
				(
					deviceIndex,
					featureIndex,
					DeviceInformation.GetDeviceInfo.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				if ((deviceInfoResponse.Capabilities & DeviceCapabilities.SerialNumber) != 0)
				{
					var serialNumberResponse = await transport.FeatureAccessSendWithRetryAsync<DeviceInformation.GetDeviceSerialNumber.Response>
					(
						deviceIndex,
						featureIndex,
						DeviceInformation.GetDeviceSerialNumber.FunctionId,
						retryCount,
						cancellationToken
					).ConfigureAwait(false);

					serialNumber = serialNumberResponse.SerialNumber;
				}
			}
		}

		HidPlusPlusDevice device = parent is null ?
			new FeatureAccessDirect(transport, productId, deviceIndex, deviceInfo, deviceType, features, friendlyName, serialNumber) :
			new FeatureAccessThroughReceiver(parent, productId, deviceIndex, deviceInfo, deviceType, features, friendlyName, serialNumber);

		return device;
	}

	private static async Task<(FeatureAccessProtocol.DeviceType, string?)> FeatureAccessGetDeviceNameAndTypeAsync
	(
		HidPlusPlusTransport transport,
		ReadOnlyDictionary<HidPlusPlusFeature, byte> features,
		byte deviceIndex,
		int retryCount,
		CancellationToken cancellationToken
	)
	{
		FeatureAccessProtocol.DeviceType deviceType = FeatureAccessProtocol.DeviceType.Unknown;
		string? deviceName = null;

		if (features.TryGetValue(HidPlusPlusFeature.DeviceNameAndType, out byte featureIndex))
		{
			var deviceTypeResponse = await transport.FeatureAccessSendWithRetryAsync<DeviceNameAndType.GetDeviceType.Response>
			(
				deviceIndex,
				featureIndex,
				DeviceNameAndType.GetDeviceType.FunctionId,
				retryCount,
				cancellationToken
			).ConfigureAwait(false);

			deviceType = deviceTypeResponse.DeviceType;

			var deviceNameLengthResponse = await transport.FeatureAccessSendWithRetryAsync<DeviceNameAndType.GetDeviceNameLength.Response>
			(
				deviceIndex,
				featureIndex,
				DeviceNameAndType.GetDeviceNameLength.FunctionId,
				retryCount,
				cancellationToken
			).ConfigureAwait(false);

			int length = deviceNameLengthResponse.Length;
			int offset = 0;

			var buffer = new byte[length];

			while (true)
			{
				var deviceNameResponse = await transport.FeatureAccessSendWithRetryAsync<DeviceNameAndType.GetDeviceName.Request, DeviceNameAndType.GetDeviceName.Response>
				(
					deviceIndex,
					featureIndex,
					DeviceNameAndType.GetDeviceName.FunctionId,
					new DeviceNameAndType.GetDeviceName.Request { Offset = (byte)offset },
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				if (deviceNameResponse.TryCopyTo(buffer.AsSpan(offset), out int count))
				{
					offset += count;

					if (offset == length)
					{
						break;
					}
					else if (count == 16)
					{
						continue;
					}
				}

				throw new InvalidOperationException("Failed to retrieve the device name.");
			}

			deviceName = Encoding.UTF8.GetString(buffer);
		}

		return (deviceType, deviceName);
	}

	private static string FormatReceiverSerialNumber(ushort productId, uint serialNumber)
		=> string.Create
		(
			13,
			(ProductId: productId, SerialNumber: serialNumber),
			static (span, state) =>
			{
				state.ProductId.TryFormat(span[..4], out _, "X4", CultureInfo.InvariantCulture);
				span[4] = '-';
				state.SerialNumber.TryFormat(span[5..], out _, "X8", CultureInfo.InvariantCulture);
			}
		);

	// Root devices will contain the HidPlusPlusTransport instance here. Child devices will contain the parent device reference.
	protected object ParentOrTransport { get; }
	protected byte DeviceIndex { get; }

	private DeviceConnectionInfo _deviceConnectionInfo;
	protected DeviceConnectionInfo DeviceConnectionInfo
	{
		get => (DeviceConnectionInfo)Volatile.Read(ref Unsafe.As<DeviceConnectionInfo, byte>(ref _deviceConnectionInfo));
		set => Volatile.Write(ref Unsafe.As<DeviceConnectionInfo, byte>(ref _deviceConnectionInfo), (byte)value);
	}

	public ushort ProductId { get; }

	private string? _friendlyName;
	public string? FriendlyName
	{
		get => _friendlyName;
		private protected set => Volatile.Write(ref _friendlyName, value);
	}

	private string? _serialNumber;
	public string? SerialNumber
	{
		get => _serialNumber;
		private protected set => Volatile.Write(ref _serialNumber, value);
	}

	protected abstract HidPlusPlusTransport Transport { get; }
	public abstract HidPlusPlusProtocolFlavor ProtocolFlavor { get; }

	// The device type should not change through the life time of the object, so we don't need a volatile read here.
	public RegisterAccessProtocol.DeviceType DeviceType => _deviceConnectionInfo.DeviceType;
	public bool IsConnected => DeviceConnectionInfo.IsLinkEstablished;

	private protected HidPlusPlusDevice(object parentOrTransport, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
	{
		ParentOrTransport = parentOrTransport;
		DeviceIndex = deviceIndex;
		ProductId = productId;
		FriendlyName = friendlyName;
		SerialNumber = serialNumber;
		DeviceConnectionInfo = deviceConnectionInfo;
	}

	public abstract ValueTask DisposeAsync();

	// The initialize method is called immediately after device creation if the device is connected.
	// For devices connected to a receiver, it will be called every time the device is connected, to allow refreshing the cached status.
	// NB: For child devices, calls to this method are handled as part of the lifecycle.
	private protected virtual Task InitializeAsync(int retryCount, CancellationToken cancellationToken) => Task.CompletedTask;

	// The below methods are used to raise connect/disconnect events on receivers and receiver-connected devices.
	// They won't be called on devices that are not of the appropriate kind.
	// It is easier to have them defined here because of the split between RAP/FAP, so that we don't need to care about the exact implementation type.
	// It is admittedly not a very clean way to do this, but as long as it work, it will be ok.
	// The principle is that child devices will determine whether the event must be raised in their Raise(Dis)Connected method, and call the RaiseDevice(Dis)Connected method on the receiver.
	// Event dispatching will be handled by the receiver device, in order to guarantee that events are sent in order, so the call flow can be something like Child->Receiver->Child->Receiver.

	// To be implemented by the receiver for registering an event.
	private protected virtual void OnDeviceDiscovered(HidPlusPlusDevice device) { }
	private protected virtual void OnDeviceConnected(HidPlusPlusDevice device, int version) { }
	private protected virtual void OnDeviceDisconnected(HidPlusPlusDevice device, int version) { }

	// To be implemented by the receiver for raising the event.
	private protected virtual void RaiseDeviceConnected(HidPlusPlusDevice device) { }
	private protected virtual void RaiseDeviceDisconnected(HidPlusPlusDevice device) { }

	// To be implemented by child devices for raising the event at both the receiver and device level. (Should call RaiseDeviceConnected/RaiseDeviceDisconnected before)
	private protected virtual void RaiseConnected(int version) { }
	private protected virtual void RaiseDisconnected(int version) { }
}
