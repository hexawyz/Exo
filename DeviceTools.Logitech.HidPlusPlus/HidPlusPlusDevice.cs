using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Notifications;
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
				return await CreateAsync
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
					default
				).ConfigureAwait(false);
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

		if (parent is null)
		{
			return new FeatureAccessDirect(transport, productId, deviceIndex, deviceInfo, deviceType, features, friendlyName, serialNumber);
		}
		else
		{
			return new FeatureAccessThroughReceiver(parent, productId, deviceIndex, deviceInfo, deviceType, features, friendlyName, serialNumber);
		}
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

	public abstract class RegisterAccess : HidPlusPlusDevice
	{
		private protected RegisterAccess(object parentOrTransport, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(parentOrTransport, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
		}

		public sealed override HidPlusPlusProtocolFlavor ProtocolFlavor => HidPlusPlusProtocolFlavor.RegisterAccess;

		public Task<TResponseParameters> RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>
		(
			Address address,
			in TRequestParameters parameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
			where TResponseParameters : struct, IMessageParameters
			=> Transport.RegisterAccessGetRegisterWithRetryAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetShortRegisterAsync<TResponseParameters>(Address address, CancellationToken cancellationToken)
			where TResponseParameters : struct, IShortMessageParameters
			=> Transport.RegisterAccessGetShortRegisterWithRetryAsync<TResponseParameters>(DeviceIndex, address, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetShortRegisterAsync<TRequestParameters, TResponseParameters>
		(
			Address address,
			in TRequestParameters parameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
			where TResponseParameters : struct, IShortMessageParameters
			=> Transport.RegisterAccessGetShortRegisterWithRetryAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetLongRegisterAsync<TResponseParameters>(Address address, CancellationToken cancellationToken)
			where TResponseParameters : struct, ILongMessageParameters
			=> Transport.RegisterAccessGetLongRegisterWithRetryAsync<TResponseParameters>(DeviceIndex, address, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetLongRegisterAsync<TRequestParameters, TResponseParameters>
		(
			Address address,
			in TRequestParameters parameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
			where TResponseParameters : struct, ILongMessageParameters
			=> Transport.RegisterAccessGetLongRegisterWithRetryAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetVeryLongRegisterAsync<TResponseParameters>(Address address, CancellationToken cancellationToken)
			where TResponseParameters : struct, IVeryLongMessageParameters
			=> Transport.RegisterAccessGetVeryLongRegisterWithRetryAsync<TResponseParameters>(DeviceIndex, address, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetVeryLongRegisterAsync<TRequestParameters, TResponseParameters>
		(
			Address address,
			in TRequestParameters parameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
			where TResponseParameters : struct, IVeryLongMessageParameters
			=> Transport.RegisterAccessGetVeryLongRegisterWithRetryAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);
	}

	public class RegisterAccessReceiver : RegisterAccess, IUsbReceiver
	{
		private LightweightSingleProducerSingleConsumerQueue<Task> _deviceOperationTaskQueue;
		private LightweightSingleProducerSingleConsumerQueue<(DeviceEventKind kind, HidPlusPlusDevice device, int Version)> _eventQueue;
		private readonly Task _deviceWatcherTask;
		private readonly Task _eventProcessingTask;
		private bool _deviceWatchStarted;

		public event ReceiverDeviceEventHandler? DeviceDiscovered;
		public event ReceiverDeviceEventHandler? DeviceConnected;
		public event ReceiverDeviceEventHandler? DeviceDisconnected;

		internal RegisterAccessReceiver(HidPlusPlusTransport transport, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(transport, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
			transport.NotificationReceived += HandleNotification;
			_deviceOperationTaskQueue = new();
			_eventQueue = new();
			_deviceWatcherTask = WatchDevicesAsync();
			_eventProcessingTask = ProcessEventsAsync();
		}

		// NB: We don't need to unregister our notification handler here, since the transport is owned by the current instance.
		public override async ValueTask DisposeAsync()
		{
			await Transport.DisposeAsync().ConfigureAwait(false);
			_deviceOperationTaskQueue.Dispose();
			_eventQueue.Dispose();
			await _deviceWatcherTask.ConfigureAwait(false);
			await _eventProcessingTask.ConfigureAwait(false);
		}

		private async Task WatchDevicesAsync()
		{
			while (true)
			{
				Task task;

				try
				{
					task = await _deviceOperationTaskQueue.DequeueAsync().ConfigureAwait(false);
				}
				catch (ObjectDisposedException)
				{
					return;
				}

				try
				{
					await task.ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					// TODO: Make the necessary so that this can be observed somewhere ?
				}
			}
		}

		private async Task ProcessEventsAsync()
		{
			while (true)
			{
				var (kind, device, version) = await _eventQueue.DequeueAsync().ConfigureAwait(false);

				try
				{
					switch (kind)
					{
					case DeviceEventKind.DeviceDiscovered:
						DeviceDiscovered?.Invoke(this, device);
						break;
					case DeviceEventKind.DeviceConnected:
						device.RaiseConnected(version);
						break;
					case DeviceEventKind.DeviceDisconnected:
						device.RaiseDisconnected(version);
						break;
					}
				}
				catch (Exception ex)
				{
					// TODO: Make the necessary so that this can be observed somewhere ?
				}
			}
		}

		/// <summary>Gets the devices currently known.</summary>
		/// <remarks>
		/// <para>
		/// This method is executed concurrently with device discovery. It is not guaranteed to return a strictly consistent result.
		/// The list of devices will, however, not change frequently after the initial discovery.
		/// Only device pairing or unpairing can affect the observed list of devices.
		/// </para>
		/// <para>
		/// In order to properly track devices, it is recommended to track devices by first setting up the events <see cref="DeviceDiscovered"/>,
		/// <see cref="DeviceConnected"/> and <see cref="DeviceDisconnected"/> before calling <see cref="StartWatchingDevicesAsync(CancellationToken)"/>.
		/// </para>
		/// <para>Before <see cref="StartWatchingDevicesAsync(CancellationToken)"/> is called, no child device will be registered.</para>
		/// </remarks>
		public async Task<HidPlusPlusDevice[]> GetCurrentDevicesAsync()
		{
			var devices = new List<HidPlusPlusDevice>();

			foreach (var state in Transport.Devices)
			{
				switch (state.CustomState)
				{
				case HidPlusPlusDevice device: devices.Add(device); break;
				case Task<HidPlusPlusDevice> task:
					try
					{
						devices.Add(await task.ConfigureAwait(false));
					}
					catch { }
					break;
				}
			}

			return devices.ToArray();
		}

		public Task StartWatchingDevicesAsync(CancellationToken cancellationToken)
			=> StartWatchingDevicesAsync(HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public async Task StartWatchingDevicesAsync(int retryCount, CancellationToken cancellationToken)
		{
			await EnableDeviceNotificationsAsync(retryCount, cancellationToken).ConfigureAwait(false);
			Volatile.Write(ref _deviceWatchStarted, true);
			await EnumerateDevicesAsync(retryCount, cancellationToken).ConfigureAwait(false);
		}

		internal async Task EnableDeviceNotificationsAsync(int retryCount, CancellationToken cancellationToken)
		{
			// Enable device arrival notifications, and set the "software present" flag.
			await Transport.RegisterAccessSetRegisterWithRetryAsync
			(
				255,
				Address.EnableHidPlusPlusNotifications,
				new EnableHidPlusPlusNotifications.Parameters
				{
					ReceiverReportingFlags = ReceiverReportingFlags.WirelessNotifications | ReceiverReportingFlags.SoftwarePresent
				},
				retryCount,
				cancellationToken
			).ConfigureAwait(false);
		}

		internal async Task EnumerateDevicesAsync(int retryCount, CancellationToken cancellationToken)
		{
			// Enumerate all connected devices.
			await Transport.RegisterAccessSetRegisterWithRetryAsync
			(
				255,
				Address.ConnectionState,
				new ConnectionState.SetRequest
				{
					Action = ConnectionStateAction.FakeDeviceArrival
				},
				retryCount,
				cancellationToken
			).ConfigureAwait(false);
		}

		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<HidPlusPlusTransport>(ParentOrTransport);

		private protected sealed override void OnDeviceDiscovered(HidPlusPlusDevice device) => _eventQueue.Enqueue((DeviceEventKind.DeviceDiscovered, device, 0));
		private protected sealed override void OnDeviceConnected(HidPlusPlusDevice device, int version) => _eventQueue.Enqueue((DeviceEventKind.DeviceConnected, device, version));
		private protected sealed override void OnDeviceDisconnected(HidPlusPlusDevice device, int version) => _eventQueue.Enqueue((DeviceEventKind.DeviceDisconnected, device, version));

		private protected override void RaiseDeviceConnected(HidPlusPlusDevice device) => DeviceConnected?.Invoke(this, device);
		private protected override void RaiseDeviceDisconnected(HidPlusPlusDevice device) => DeviceDisconnected?.Invoke(this, device);

		private protected virtual HidPlusPlusProtocolFlavor InferProtocolFlavor(in DeviceConnectionParameters deviceConnectionParameters)
			=> TryInferProductCategory(deviceConnectionParameters.WirelessProductId, out var productCategory) && productCategory == ProductCategory.QuadFapDevice ?
				HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess :
				HidPlusPlusProtocolFlavor.RegisterAccess;

		// Child devices can forward notifications here when necessary. e.g. When the WPID of a device has changed.
		internal void HandleNotification(ReadOnlySpan<byte> message)
		{
			if (message.Length < 7) return;

			var header = Unsafe.ReadUnaligned<RegisterAccessHeader>(ref MemoryMarshal.GetReference(message));

			// If we receive a device connect notification, it indicates a new device. (In the sense of not yet known; not necessarily a new pairing)
			// Once we create the device object, it will automatically process the notifications.
			if (header.SubId == SubId.DeviceConnect)
			{
				// Ignore device connect notifications if the StartWatchingDevicesAsync method ahs not been called.
				// This ensures that consumers of the class have a chance to observe all devices through events.
				if (!Volatile.Read(ref _deviceWatchStarted)) return;

				var parameters = Unsafe.ReadUnaligned<DeviceConnectionParameters>(ref Unsafe.AsRef(message[3]));

				// All (Quad) HID++ 2.0 should adhere to this product ID mapping for now. It is important to know this in advance because the device might be offline.
				var protocolFlavor = InferProtocolFlavor(parameters);

				RegisterNotificationTask(ProcessDeviceArrivalAsync(parameters.WirelessProductId, header.DeviceId, parameters.DeviceInfo, protocolFlavor));
			}
		}

		// Get device information from the pairing data in the receiver.
		// For HID++ 2.0 devices, this may be less good than what will be discovered through the device itself, but this will work even when the device is disconnected.
		protected virtual async Task<(RegisterAccessProtocol.DeviceType DeviceType, string? DeviceName, string? SerialNumber)> GetPairedDeviceInformationAsync
		(
			byte deviceIndex,
			int retryCount,
			CancellationToken cancellationToken
		)
		{
			if (deviceIndex is 0x00 or > 0x0F) return default;

			RegisterAccessProtocol.DeviceType deviceType = RegisterAccessProtocol.DeviceType.Unknown;
			string? deviceName = null;
			string? serialNumber = null;

			try
			{
				var pairingInformationResponse = await Transport.RegisterAccessGetRegisterWithRetryAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.PairingInformationResponse>
				(
					255,
					Address.NonVolatileAndPairingInformation,
					new(NonVolatileAndPairingInformation.Parameter.PairingInformation1 + (deviceIndex - 1)),
					retryCount,
					cancellationToken
				);

				deviceType = (RegisterAccessProtocol.DeviceType)pairingInformationResponse.DeviceType;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.InvalidParameter)
			{
			}

			try
			{
				var extendedPairingInformationResponse = await Transport.RegisterAccessGetRegisterWithRetryAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.ExtendedPairingInformationResponse>
				(
					255,
					Address.NonVolatileAndPairingInformation,
					new(NonVolatileAndPairingInformation.Parameter.ExtendedPairingInformation1 + (deviceIndex - 1)),
					retryCount,
					cancellationToken
				);

				serialNumber = extendedPairingInformationResponse.SerialNumber.ToString("X8");
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.InvalidParameter)
			{
			}

			try
			{
				var deviceNameResponse = await Transport.RegisterAccessGetRegisterWithRetryAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.DeviceNameResponse>
				(
					255,
					Address.NonVolatileAndPairingInformation,
					new(NonVolatileAndPairingInformation.Parameter.DeviceName1 + (deviceIndex - 1)),
					retryCount,
					cancellationToken
				);

				deviceName = deviceNameResponse.GetDeviceName();
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.InvalidParameter)
			{
			}

			return (deviceType, deviceName, serialNumber);
		}

		// To call in order to register a task started within a notification handler.
		// It should *only* be called within a notification handler, as notifications are processed sequentially and will not generate race conditions here.
		// The tasks registered here will be tracked internally for completion.
		internal void RegisterNotificationTask(Task task) => _deviceOperationTaskQueue.Enqueue(task);

		// This method can never finish synchronously because it will wait on message processing. (This is kinda important for the correct order of state updates)
		private async Task ProcessDeviceArrivalAsync(ushort productId, byte deviceIndex, DeviceConnectionInfo deviceInfo, HidPlusPlusProtocolFlavor protocolFlavor)
		{
			// Don't (try to) create the same device object twice. The custom state acts as some kind of lock, while also storing the associated device object.
			if (Volatile.Read(ref Transport.Devices[deviceIndex].CustomState) is null)
			{
				try
				{
					var task = CreateConnectedDeviceAsync(protocolFlavor, productId, deviceIndex, deviceInfo, HidPlusPlusTransportExtensions.DefaultRetryCount, default);

					Volatile.Write(ref Transport.Devices[deviceIndex].CustomState, task);

					await task.ConfigureAwait(false);
				}
				catch
				{
					Volatile.Write(ref Transport.Devices[deviceIndex].CustomState, null);
					throw;
				}
			}
		}

		private async Task<HidPlusPlusDevice> CreateConnectedDeviceAsync
		(
			HidPlusPlusProtocolFlavor protocolFlavor,
			ushort productId,
			byte deviceIndex,
			DeviceConnectionInfo deviceInfo,
			int retryCount,
			CancellationToken cancellationToken
		)
		{
			var (_, deviceName, serialNumber) = await GetPairedDeviceInformationAsync(deviceIndex, retryCount, cancellationToken).ConfigureAwait(false);
			return await CreateAsync(this, Transport, protocolFlavor, productId, deviceIndex, deviceInfo, deviceName, serialNumber, retryCount, default).ConfigureAwait(false);
		}
	}

	public sealed class UnifyingReceiver : RegisterAccessReceiver
	{
		internal UnifyingReceiver(HidPlusPlusTransport transport, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(transport, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
		}
	}

	public sealed class BoltReceiver : RegisterAccessReceiver
	{
		internal BoltReceiver(HidPlusPlusTransport transport, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(transport, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
		}

		// NB: Do HID++ 1.0 Bolt devices exist ?
		// If so, we'll need to change the algorithm here. Bolt devices seem to use their Bluetooth (USB) PID as WPID directly.
		// This kinda makes sense, as Bolt is based upon Bluetooth, but it does not provide any information on the underlying protocol used by the device.
		private protected override HidPlusPlusProtocolFlavor InferProtocolFlavor(in DeviceConnectionParameters deviceConnectionParameters)
			=> HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess;

		protected override async Task<(RegisterAccessProtocol.DeviceType DeviceType, string? DeviceName, string? SerialNumber)> GetPairedDeviceInformationAsync
		(
			byte deviceIndex,
			int retryCount,
			CancellationToken cancellationToken
		)
		{
			// The serial number should probably be somewhere in there, but apart from the WPID (which seems to be the BT/USB PID in this case), nothing appears obvious.
			var boltPairingInformation = await Transport.RegisterAccessGetLongRegisterWithRetryAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.BoltPairingInformationResponse>
			(
				DeviceIndex,
				Address.NonVolatileAndPairingInformation,
				new NonVolatileAndPairingInformation.Request { Parameter = NonVolatileAndPairingInformation.Parameter.BoltPairingInformation1 - 1 + deviceIndex },
				retryCount,
				cancellationToken
			).ConfigureAwait(false);

			// Seems like this could be a multipart query, but I don't have any device with a name longer than 13 characters to verify this.
			var deviceNameResponse = await Transport.RegisterAccessGetLongRegisterWithRetryAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.BoltDeviceNameResponse>
			(
				DeviceIndex,
				Address.NonVolatileAndPairingInformation,
				new NonVolatileAndPairingInformation.Request { Parameter = NonVolatileAndPairingInformation.Parameter.BoltDeviceName1 - 1 + deviceIndex, Index = 1 },
				retryCount,
				cancellationToken
			).ConfigureAwait(false);

			var deviceName = deviceNameResponse.GetDeviceName();

			return (0, deviceName, null);
		}
	}

	public sealed class RegisterAccessDirect : RegisterAccess
	{
		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<HidPlusPlusTransport>(ParentOrTransport);

		internal RegisterAccessDirect(HidPlusPlusTransport transport, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(transport, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
		}

		public override ValueTask DisposeAsync() => Transport.DisposeAsync();
	}

	public class RegisterAccessThroughReceiver : RegisterAccess, IDeviceThroughReceiver
	{
		public new byte DeviceIndex => base.DeviceIndex;

		public event DeviceEventHandler? Connected;
		public event DeviceEventHandler? Disconnected;

		// 0 (default) if last published device state is disconnected; 1 if last published device state is connected.
		private bool _lastPublishedStateWasConnected;
		// Track the number of connect/disconnect events, and allow skipping .
		private int _version;

		protected RegisterAccessReceiver Receiver => Unsafe.As<RegisterAccessReceiver>(ParentOrTransport);
		protected sealed override HidPlusPlusTransport Transport => Receiver.Transport;

		internal RegisterAccessThroughReceiver(RegisterAccessReceiver parent, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(parent, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
			var device = Transport.Devices[deviceIndex];
			device.NotificationReceived += HandleNotification;
			Volatile.Write(ref device.CustomState, this);
			Receiver.OnDeviceDiscovered(this);
			if (deviceConnectionInfo.IsLinkEstablished) Receiver.OnDeviceConnected(this, _version);
		}

		private protected override void RaiseConnected(int version)
		{
			if (Volatile.Read(ref _version) == version)
			{
				if (!_lastPublishedStateWasConnected)
				{
					try
					{
						Receiver.RaiseDeviceConnected(this);
					}
					finally
					{
						try
						{
							Connected?.Invoke(this);
						}
						finally
						{
							_lastPublishedStateWasConnected = true;
						}
					}
				}
			}
		}

		private protected override void RaiseDisconnected(int version)
		{
			if (Volatile.Read(ref _version) == version)
			{
				if (_lastPublishedStateWasConnected)
				{
					try
					{
						Receiver.RaiseDeviceDisconnected(this);
					}
					finally
					{
						try
						{
							Disconnected?.Invoke(this);
						}
						finally
						{
							_lastPublishedStateWasConnected = false;
						}
					}
				}
			}
		}

		private void HandleNotification(ReadOnlySpan<byte> message)
		{
			if (message.Length < 7) return;

			var header = Unsafe.ReadUnaligned<RegisterAccessHeader>(ref MemoryMarshal.GetReference(message));

			// If we receive a device connect notification, it indicates a new device. (In the sense of not yet known; not necessarily a new pairing)
			// Once we create the device object, it will automatically process the notifications.
			if (header.SubId == SubId.DeviceConnect)
			{
				var parameters = Unsafe.ReadUnaligned<DeviceConnectionParameters>(ref Unsafe.AsRef(message[3]));

				// If the WPID has changed, unregister the current instance and forward the notification to the receiver. (It will recreate a new device)
				if (parameters.WirelessProductId != ProductId)
				{
					DisposeInternal();
					Receiver.HandleNotification(message);
					return;
				}

				bool wasConnected = DeviceConnectionInfo.IsLinkEstablished;
				bool isConnected = parameters.DeviceInfo.IsLinkEstablished;

				// Update the connection information.
				DeviceConnectionInfo = parameters.DeviceInfo;

				if (isConnected != wasConnected)
				{
					int version = Interlocked.Increment(ref _version);
					if (isConnected)
					{
						Receiver.OnDeviceConnected(this, version);
					}
					else
					{
						Receiver.OnDeviceDisconnected(this, version);
					}
				}
			}
			else
			{
				var connectionInfo = DeviceConnectionInfo;

				bool wasConnected = DeviceConnectionInfo.IsLinkEstablished;

				if (wasConnected)
				{
					connectionInfo.ConnectionFlags |= DeviceConnectionFlags.LinkNotEstablished;

					// Update the connection information.
					DeviceConnectionInfo = connectionInfo;

					int version = Interlocked.Increment(ref _version);
					Receiver.OnDeviceDisconnected(this, version);
				}
			}
		}

		private void DisposeInternal()
		{
			var device = Transport.Devices[DeviceIndex];
			device.NotificationReceived += HandleNotification;
			Volatile.Write(ref device.CustomState, null);
		}

		public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	public abstract class FeatureAccess : HidPlusPlusDevice
	{
		// Provide cached indices of supported features.
		// Unsupported features will have the default value of zero and can be detected as such.
		private protected sealed class FeatureIndices
		{
			public FeatureIndices(ReadOnlyDictionary<HidPlusPlusFeature, byte> features)
			{
				byte index;
				
				if (features.TryGetValue(HidPlusPlusFeature.BatteryUnifiedLevelStatus, out index))
				{
					BatteryUnifiedLevelStatusFeatureIndex = index;
				}
				if (features.TryGetValue(HidPlusPlusFeature.UnifiedBattery, out index))
				{
					UnifiedBatteryFeatureIndex = index;
				}
			}

			public readonly byte BatteryUnifiedLevelStatusFeatureIndex;
			public readonly byte UnifiedBatteryFeatureIndex;
		}

		// Fields are not readonly because devices seen through a receiver can be discovered while disconnected.
		// The values should be updated when the device is connected.
		private protected ReadOnlyDictionary<HidPlusPlusFeature, byte>? CachedFeatures;
		private protected FeatureIndices? CachedFeatureIndices;
		private FeatureAccessProtocol.DeviceType _deviceType;

		// NB: We probably don't need Volatile reads here, as this data isn't supposed to be updated often, and we expect it to be read as a response to a connection notification.
		public new FeatureAccessProtocol.DeviceType DeviceType
		{
			get => _deviceType;
			private protected set => Volatile.Write(ref Unsafe.As<FeatureAccessProtocol.DeviceType, byte>(ref _deviceType), (byte)value);
		}

		public event Action<FeatureAccess, byte> BatteryLevelChanged;

		private protected FeatureAccess
		(
			object parentOrTransport,
			ushort productId,
			byte deviceIndex,
			DeviceConnectionInfo deviceConnectionInfo,
			FeatureAccessProtocol.DeviceType deviceType,
			ReadOnlyDictionary<HidPlusPlusFeature, byte>? cachedFeatures,
			string? friendlyName,
			string? serialNumber
		)
			: base(parentOrTransport, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
			_deviceType = deviceType;
			CachedFeatures = cachedFeatures;
			CachedFeatureIndices = cachedFeatures is not null ? new FeatureIndices(cachedFeatures) : null;
			var device = Transport.Devices[deviceIndex];
			device.NotificationReceived += HandleNotification;
		}

		public override HidPlusPlusProtocolFlavor ProtocolFlavor => HidPlusPlusProtocolFlavor.FeatureAccess;

		public bool HasBatteryInformation
			=> CachedFeatureIndices is not null ?
				CachedFeatureIndices.UnifiedBatteryFeatureIndex != 0 || CachedFeatureIndices.BatteryUnifiedLevelStatusFeatureIndex != 0 :
				throw new InvalidOperationException("The device has not yet been connected.");

		protected virtual void HandleNotification(ReadOnlySpan<byte> message)
		{
			if (message.Length < 7) return;

			// The cached feature indices should technically never be null once the device is connected and sending actual notifications.
			if (CachedFeatureIndices is not { } featureIndices) return;

			ref var header = ref Unsafe.As<byte, FeatureAccessHeader>(ref MemoryMarshal.GetReference(message));

			if (header.FeatureIndex == featureIndices.UnifiedBatteryFeatureIndex)
			{
				if (header.FunctionId == UnifiedBattery.GetStatus.EventId && message.Length >= 20)
				{
					ref var data = ref Unsafe.As<byte, FeatureAccessLongMessage<UnifiedBattery.GetStatus.Response>>(ref MemoryMarshal.GetReference(message)).Parameters;

					byte chargeLevel = data.StateOfCharge;

					if (BatteryLevelChanged is { } batteryLevelChanged)
					{
						_ = Task.Run
						(
							() =>
							{
								batteryLevelChanged.Invoke(this, chargeLevel);
							}
						);
					}
				}
			}
			else if (header.FeatureIndex == featureIndices.BatteryUnifiedLevelStatusFeatureIndex && featureIndices.UnifiedBatteryFeatureIndex == 0)
			{
				ref var data = ref Unsafe.As<byte, FeatureAccessShortMessage<BatteryUnifiedLevelStatus.GetBatteryLevelStatus.Response>>(ref MemoryMarshal.GetReference(message)).Parameters;

				byte chargeLevel = data.BatteryDischargeLevel;

				// It seems that the charge level can be reported as zero when the device is charging. (Which explains the Windows 0% notification when starting the keyboard plugged)
				// We can try to rely on the battery status to provide a better approximate in some cases.
				if (data.BatteryDischargeLevel == 0)
				{
					switch (data.BatteryStatus)
					{
					case BatteryUnifiedLevelStatus.BatteryStatus.ChargeComplete:
						chargeLevel = 100;
						break;
					case BatteryUnifiedLevelStatus.BatteryStatus.ChargeInFinalStage:
						chargeLevel = 80;
						break;
					case BatteryUnifiedLevelStatus.BatteryStatus.Recharging:
						// TODO: Must make it possible to communicate unknown battery level.
						chargeLevel = 50;
						break;
					}
				}

				if (BatteryLevelChanged is { } batteryLevelChanged)
				{
					_ = Task.Run
					(
						() =>
						{
							batteryLevelChanged.Invoke(this, chargeLevel);
						}
					);
				}
			}
		}

		public ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>> GetFeaturesAsync(CancellationToken cancellationToken)
			=> GetFeaturesWithRetryAsync(HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		protected ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>> GetFeaturesWithRetryAsync(int retryCount, CancellationToken cancellationToken)
			=> CachedFeatures is not null ?
				new ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>>(CachedFeatures) :
				new ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>>(GetFeaturesWithRetryAsyncCore(retryCount, cancellationToken));

		private async Task<ReadOnlyDictionary<HidPlusPlusFeature, byte>> GetFeaturesWithRetryAsyncCore(int retryCount, CancellationToken cancellationToken)
		{
			var features = await Transport.GetFeaturesWithRetryAsync(DeviceIndex, retryCount, cancellationToken).ConfigureAwait(false);

			Volatile.Write(ref CachedFeatureIndices, new(features));
			Volatile.Write(ref CachedFeatures, features);

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
			=> Transport.FeatureAccessSendWithRetryAsync<TResponseParameters>(DeviceIndex, featureIndex, functionId, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task SendAsync<TRequestParameters>
		(
			byte featureIndex,
			byte functionId,
			in TRequestParameters requestParameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageRequestParameters
			=> Transport.FeatureAccessSendWithRetryAsync(DeviceIndex, featureIndex, functionId, requestParameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> SendAsync<TRequestParameters, TResponseParameters>
		(
			byte featureIndex,
			byte functionId,
			in TRequestParameters requestParameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageRequestParameters
			where TResponseParameters : struct, IMessageResponseParameters
			=> Transport.FeatureAccessSendWithRetryAsync<TRequestParameters, TResponseParameters>(DeviceIndex, featureIndex, functionId, requestParameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);
	}

	public sealed class FeatureAccessDirect : FeatureAccess
	{
		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<HidPlusPlusTransport>(ParentOrTransport);

		internal FeatureAccessDirect
		(
			HidPlusPlusTransport transport,
			ushort productId,
			byte deviceIndex,
			DeviceConnectionInfo deviceConnectionInfo,
			FeatureAccessProtocol.DeviceType deviceType,
			ReadOnlyDictionary<HidPlusPlusFeature, byte>? cachedFeatures,
			string? friendlyName,
			string? serialNumber
		)
			: base(transport, productId, deviceIndex, deviceConnectionInfo, deviceType, cachedFeatures, friendlyName, serialNumber)
		{
		}

		public override ValueTask DisposeAsync() => Transport.DisposeAsync();
	}

	public class FeatureAccessThroughReceiver : FeatureAccess, IDeviceThroughReceiver
	{
		public new byte DeviceIndex => base.DeviceIndex;

		public event DeviceEventHandler? Connected;
		public event DeviceEventHandler? Disconnected;

		protected RegisterAccessReceiver Receiver => Unsafe.As<RegisterAccessReceiver>(ParentOrTransport);
		protected sealed override HidPlusPlusTransport Transport => Receiver.Transport;

		private bool _isInitialized;

		// 0 (default) if last published device state is disconnected; 1 if last published device state is connected.
		private bool _lastPublishedStateWasConnected;
		// Track the number of connect/disconnect events, and allow skipping .
		private int _version;

		internal FeatureAccessThroughReceiver
		(
			RegisterAccessReceiver parent,
			ushort productId,
			byte deviceIndex,
			DeviceConnectionInfo deviceConnectionInfo,
			FeatureAccessProtocol.DeviceType deviceType,
			ReadOnlyDictionary<HidPlusPlusFeature, byte>? cachedFeatures,
			string? friendlyName,
			string? serialNumber
		)
			: base(parent, productId, deviceIndex, deviceConnectionInfo, deviceType, cachedFeatures, friendlyName, serialNumber)
		{
			// If the device is connected and we reach this point in the code, relevant information will already have been fetched and passed through the parameters.
			_isInitialized = deviceConnectionInfo.IsLinkEstablished;
			var device = Transport.Devices[deviceIndex];
			device.NotificationReceived += HandleNotification;
			Volatile.Write(ref device.CustomState, this);
			Receiver.OnDeviceDiscovered(this);
			if (deviceConnectionInfo.IsLinkEstablished) Receiver.OnDeviceConnected(this, _version);
		}

		private protected override void RaiseConnected(int version)
		{
			if (Volatile.Read(ref _version) == version)
			{
				if (!_lastPublishedStateWasConnected)
				{
					try
					{
						Receiver.RaiseDeviceConnected(this);
					}
					finally
					{
						try
						{
							Connected?.Invoke(this);
						}
						finally
						{
							_lastPublishedStateWasConnected = true;
						}
					}
				}
			}
		}

		private protected override void RaiseDisconnected(int version)
		{
			if (Volatile.Read(ref _version) == version)
			{
				if (_lastPublishedStateWasConnected)
				{
					try
					{
						Receiver.RaiseDeviceDisconnected(this);
					}
					finally
					{
						try
						{
							Disconnected?.Invoke(this);
						}
						finally
						{
							_lastPublishedStateWasConnected = false;
						}
					}
				}
			}
		}

		protected override void HandleNotification(ReadOnlySpan<byte> message)
		{
			if (message.Length < 7) return;

			var header = Unsafe.ReadUnaligned<RegisterAccessHeader>(ref MemoryMarshal.GetReference(message));

			// If we receive a device connect notification, it indicates a new device. (In the sense of not yet known; not necessarily a new pairing)
			// Once we create the device object, it will automatically process the notifications.
			if (header.SubId == SubId.DeviceConnect)
			{
				var parameters = Unsafe.ReadUnaligned<DeviceConnectionParameters>(ref Unsafe.AsRef(message[3]));

				// If the WPID has changed, unregister the current instance and forward the notification to the receiver. (It will recreate a new device)
				if (parameters.WirelessProductId != ProductId)
				{
					DisposeInternal();
					Receiver.HandleNotification(message);
					return;
				}

				bool wasConnected = DeviceConnectionInfo.IsLinkEstablished;
				bool isConnected = parameters.DeviceInfo.IsLinkEstablished;

				// Update the connection information.
				DeviceConnectionInfo = parameters.DeviceInfo;

				if (isConnected != wasConnected)
				{
					int version = Interlocked.Increment(ref _version);
					if (isConnected)
					{
						if (!Volatile.Read(ref _isInitialized))
						{
							Receiver.RegisterNotificationTask(UpdateDeviceInformationAsync(HidPlusPlusTransportExtensions.DefaultRetryCount, version, default));
							Volatile.Write(ref _isInitialized, true);
						}
						else
						{
							Receiver.OnDeviceConnected(this, version);
						}
					}
					else
					{
						Receiver.OnDeviceDisconnected(this, version);
					}
				}
			}
			else if (header.SubId == SubId.DeviceDisconnect)
			{
				var connectionInfo = DeviceConnectionInfo;

				bool wasConnected = DeviceConnectionInfo.IsLinkEstablished;

				if (wasConnected)
				{
					connectionInfo.ConnectionFlags |= DeviceConnectionFlags.LinkNotEstablished;

					// Update the connection information.
					DeviceConnectionInfo = connectionInfo;

					int version = Interlocked.Increment(ref _version);
					Receiver.OnDeviceDisconnected(this, version);
				}
			}
			else
			{
				base.HandleNotification(message);
			}
		}

		private async Task UpdateDeviceInformationAsync(int retryCount, int version, CancellationToken cancellationToken)
		{
			var transport = Transport;

			var features = await GetFeaturesWithRetryAsync(retryCount, cancellationToken).ConfigureAwait(false);

			var (retrievedType, retrievedName) = await FeatureAccessGetDeviceNameAndTypeAsync(transport, features, DeviceIndex, retryCount, cancellationToken).ConfigureAwait(false);

			// Update the device information if we were able to retrieve the device name.
			if (retrievedName is not null)
			{
				DeviceType = retrievedType;
				FriendlyName = retrievedName;
			}

			if (HasBatteryInformation)
			{
				// TODO: Initialize battery information. (Especially important for devices with very low frequency charge information)
			}

			Volatile.Write(ref _isInitialized, true);

			Receiver.OnDeviceConnected(this, version);
		}

		private void DisposeInternal()
		{
			var device = Transport.Devices[DeviceIndex];
			device.NotificationReceived += HandleNotification;
			Volatile.Write(ref device.CustomState, null);
		}

		// Instances of this class should not be disposed externally. The DisposeAsync method does nothing.
		public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
