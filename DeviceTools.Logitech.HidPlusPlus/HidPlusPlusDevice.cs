using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
				return await CreateAsync(null, transport, expectedProtocolFlavor, productId, 255, true, externalFriendlyName, default).ConfigureAwait(false);
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
		RegisterAccessReceiver? parentDevice,
		HidPlusPlusTransport transport,
		HidPlusPlusProtocolFlavor expectedProtocolFlavor,
		ushort productId,
		byte deviceIndex,
		bool isConnected, // In the case of a receiver, attached devices can be discovered while disconnected. There should be enough info do to most basic things.
		string? externalFriendlyName,
		CancellationToken cancellationToken
	)
	{
		// Protocol version check.
		// TODO: Make this check a bit better and explicitly list the supported versions.
		HidPlusPlusVersion protocolVersion;
		try
		{
			protocolVersion = await transport.GetProtocolVersionAsync(deviceIndex, cancellationToken).ConfigureAwait(false);
			transport.Devices[deviceIndex].SetProtocolFlavor(HidPlusPlusProtocolFlavor.FeatureAccess);
		}
		catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == RegisterAccessProtocol.ErrorCode.InvalidSubId)
		{
			if (expectedProtocolFlavor is not HidPlusPlusProtocolFlavor.Unknown and not HidPlusPlusProtocolFlavor.RegisterAccess)
			{
				throw new Exception("Protocol flavor does not match.");
			}

			transport.Devices[deviceIndex].SetProtocolFlavor(HidPlusPlusProtocolFlavor.RegisterAccess);
			return await CreateRegisterAccessAsync(parentDevice, transport, productId, deviceIndex, isConnected, externalFriendlyName, cancellationToken).ConfigureAwait(false);
		}

		if (expectedProtocolFlavor is not HidPlusPlusProtocolFlavor.Unknown and not HidPlusPlusProtocolFlavor.FeatureAccess and not HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess)
		{
			throw new Exception("Protocol flavor does not match.");
		}
		else if (protocolVersion.Major >= 2 && protocolVersion.Major <= 4)
		{
			return await CreateFeatureAccessAsync(parentDevice, transport, productId, deviceIndex, isConnected, externalFriendlyName, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			throw new Exception($"Unsupported protocol version: {protocolVersion.Major}.{protocolVersion.Minor}.");
		}
	}

	private static async Task<HidPlusPlusDevice> CreateRegisterAccessAsync
	(
		RegisterAccessReceiver? parent,
		HidPlusPlusTransport transport,
		ushort productId,
		byte deviceIndex,
		bool isConnected,
		string? externalFriendlyName,
		CancellationToken cancellationToken
	)
	{
		string? friendlyName = externalFriendlyName;
		var deviceKind = RegisterAccessDeviceKind.Default;

		TryInferProductCategory(productId, out var productCategory);

		if (productCategory == ProductCategory.UsbReceiver)
		{
			deviceKind = RegisterAccessDeviceKind.Receiver;
		}

		// Handling of HID++ devices seems to be way more complex, as the standard is not as strictly enforced, and there doesn't seem to be a way to get information of the connected device ?
		// i.e. We can know if the device is a receiver from the Product ID, but that's about it ?
		string? serialNumber = null;

		try
		{
			// Unifying receivers and some other should answer to this relatively undocumented call that will provide the "serial number" among other things.
			// We can find trace of this in the logitech Unifying chrome extension, where the serial number is also called base address. (A radio thing?)
			var receiverInformation = await transport.RegisterAccessGetLongRegisterAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.ReceiverInformationResponse>
			(
				deviceIndex,
				Address.NonVolatileAndPairingInformation,
				new NonVolatileAndPairingInformation.Request(NonVolatileAndPairingInformation.Parameter.ReceiverInformation),
				cancellationToken
			).ConfigureAwait(false);

			serialNumber = FormatReceiverSerialNumber(productId, receiverInformation.SerialNumber);

			// TODO: Don't hardcode Unifying Receivers product IDs if possible. (Can they be auto-detected reliably ?)
			if (productId is 0xC52B or 0xC52B or 0xC531 or 0xC532 or 0xC534)
			{
				deviceKind = RegisterAccessDeviceKind.UnifyingReceiver;
			}
		}
		catch (HidPlusPlus1Exception ex) when (ex.ErrorCode is RegisterAccessProtocol.ErrorCode.InvalidAddress or RegisterAccessProtocol.ErrorCode.InvalidParameter)
		{
		}

		if (serialNumber is null && deviceKind is RegisterAccessDeviceKind.Receiver)
		{
			try
			{
				var boltSerialNumberResponse = await transport.RegisterAccessGetLongRegisterAsync<BoltSerialNumber.Response>
				(
					deviceIndex,
					Address.BoltSerialNumber,
					cancellationToken
				).ConfigureAwait(false);

				serialNumber = boltSerialNumberResponse.ToString();

				deviceKind = RegisterAccessDeviceKind.BoltReceiver;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode is RegisterAccessProtocol.ErrorCode.InvalidAddress or RegisterAccessProtocol.ErrorCode.InvalidParameter)
			{
			}
		}

		if (parent is not null && deviceKind != RegisterAccessDeviceKind.Default) throw new InvalidOperationException($"A receiver cannot be paired to another receiver. (Product ID {productId}");

		switch (deviceKind)
		{
		case RegisterAccessDeviceKind.Default:
			return parent is null ?
				new RegisterAccessDirect(transport, deviceIndex, productId, friendlyName, serialNumber) :
				new RegisterAccessThroughReceiver(parent, deviceIndex, productId, friendlyName, serialNumber);
		case RegisterAccessDeviceKind.Receiver:
			return new RegisterAccessReceiver(transport, deviceIndex, productId, friendlyName, serialNumber);
		case RegisterAccessDeviceKind.UnifyingReceiver:
			return new RegisterAccessReceiver(transport, deviceIndex, productId, friendlyName, serialNumber);
		case RegisterAccessDeviceKind.BoltReceiver:
			return new RegisterAccessReceiver(transport, deviceIndex, productId, friendlyName, serialNumber);
		default:
			throw new InvalidOperationException();
		}
	}

	private static async Task<HidPlusPlusDevice> CreateFeatureAccessAsync
	(
		RegisterAccessReceiver? parent,
		HidPlusPlusTransport transport,
		ushort productId,
		byte deviceIndex,
		bool isConnected,
		string? externalFriendlyName,
		CancellationToken cancellationToken
	)
	{
		string? friendlyName = externalFriendlyName;
		var features = await transport.GetFeaturesAsync(deviceIndex, cancellationToken).ConfigureAwait(false);
		string? serialNumber = null;
		FeatureAccessProtocol.DeviceType? deviceType = null;

		if (features.TryGetValue(HidPlusPlusFeature.DeviceNameAndType, out byte featureIndex))
		{
			var deviceTypeResponse = await transport.FeatureAccessSendAsync<DeviceNameAndType.GetDeviceType.Response>
			(
				deviceIndex,
				featureIndex,
				DeviceNameAndType.GetDeviceType.FunctionId,
				cancellationToken
			).ConfigureAwait(false);

			deviceType = deviceTypeResponse.DeviceType;

			var deviceNameLengthResponse = await transport.FeatureAccessSendAsync<DeviceNameAndType.GetDeviceNameLength.Response>
			(
				deviceIndex,
				featureIndex,
				DeviceNameAndType.GetDeviceNameLength.FunctionId,
				cancellationToken
			).ConfigureAwait(false);

			int length = deviceNameLengthResponse.Length;
			int offset = 0;

			var buffer = new byte[length];

			while (true)
			{
				var deviceNameResponse = await transport.FeatureAccessSendAsync<DeviceNameAndType.GetDeviceName.Request, DeviceNameAndType.GetDeviceName.Response>
				(
					deviceIndex,
					featureIndex,
					DeviceNameAndType.GetDeviceName.FunctionId,
					new DeviceNameAndType.GetDeviceName.Request { Offset = (byte)offset },
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

			friendlyName = Encoding.UTF8.GetString(buffer);
		}

		if (features.TryGetValue(HidPlusPlusFeature.DeviceInformation, out featureIndex))
		{
			var deviceInfoResponse = await transport.FeatureAccessSendAsync<DeviceInformation.GetDeviceInfo.Response>
			(
				deviceIndex,
				featureIndex,
				DeviceInformation.GetDeviceInfo.FunctionId,
				cancellationToken
			).ConfigureAwait(false);

			if ((deviceInfoResponse.Capabilities & DeviceCapabilities.SerialNumber) != 0)
			{
				var serialNumberResponse = await transport.FeatureAccessSendAsync<DeviceInformation.GetDeviceSerialNumber.Response>
				(
					deviceIndex,
					featureIndex,
					DeviceInformation.GetDeviceSerialNumber.FunctionId,
					cancellationToken
				).ConfigureAwait(false);

				serialNumber = serialNumberResponse.SerialNumber;
			}
		}

		if (parent is null)
		{
			return new FeatureAccessDirect(transport, deviceIndex, productId, friendlyName, serialNumber);
		}
		else
		{
			return new FeatureAccessThroughReceiver(parent, deviceIndex, productId, friendlyName, serialNumber);
		}
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
	public ushort ProductId { get; }
	public string? FriendlyName { get; }
	public string? SerialNumber { get; }

	protected abstract HidPlusPlusTransport Transport { get; }
	public abstract HidPlusPlusProtocolFlavor ProtocolFlavor { get; }

	private protected HidPlusPlusDevice(object parentOrTransport, byte deviceIndex, ushort productId, string? friendlyName, string? serialNumber)
	{
		ParentOrTransport = parentOrTransport;
		DeviceIndex = deviceIndex;
		ProductId = productId;
		FriendlyName = friendlyName;
		SerialNumber = serialNumber;
	}

	public abstract ValueTask DisposeAsync();

	public abstract class RegisterAccess : HidPlusPlusDevice
	{
		private protected RegisterAccess(object parentOrTransport, byte deviceIndex, ushort productId, string? friendlyName, string? serialNumber)
			: base(parentOrTransport, deviceIndex, productId, friendlyName, serialNumber)
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
			=> Transport.RegisterAccessGetRegisterAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetShortRegisterAsync<TResponseParameters>(Address address, CancellationToken cancellationToken)
			where TResponseParameters : struct, IShortMessageParameters
			=> Transport.RegisterAccessGetShortRegisterAsync<TResponseParameters>(DeviceIndex, address, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetShortRegisterAsync<TRequestParameters, TResponseParameters>
		(
			Address address,
			in TRequestParameters parameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
			where TResponseParameters : struct, IShortMessageParameters
			=> Transport.RegisterAccessGetShortRegisterAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetLongRegisterAsync<TResponseParameters>(Address address, CancellationToken cancellationToken)
			where TResponseParameters : struct, ILongMessageParameters
			=> Transport.RegisterAccessGetLongRegisterAsync<TResponseParameters>(DeviceIndex, address, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetLongRegisterAsync<TRequestParameters, TResponseParameters>
		(
			Address address,
			in TRequestParameters parameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageGetParameters, IShortMessageParameters
			where TResponseParameters : struct, ILongMessageParameters
			=> Transport.RegisterAccessGetLongRegisterAsync<TRequestParameters, TResponseParameters>(DeviceIndex, address, parameters, cancellationToken);

		public Task<TResponseParameters> RegisterAccessGetVeryLongRegisterAsync<TResponseParameters>(Address address, CancellationToken cancellationToken)
			where TResponseParameters : struct, IVeryLongMessageParameters
			=> Transport.RegisterAccessGetVeryLongRegisterAsync<TResponseParameters>(DeviceIndex, address, cancellationToken);

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

	public class RegisterAccessReceiver : RegisterAccess
	{
		private LightweightSingleProducerSingleConsumerQueue<Task> _deviceCreationTaskQueue;
		private readonly Task _deviceWatcherTask;

		internal RegisterAccessReceiver(HidPlusPlusTransport transport, byte deviceIndex, ushort productId, string? friendlyName, string? serialNumber)
			: base(transport, deviceIndex, productId, friendlyName, serialNumber)
		{
			transport.NotificationReceived += HandleNotification;
			_deviceCreationTaskQueue = new();
			_deviceWatcherTask = WatchDevicesAsync();
		}

		private async Task WatchDevicesAsync()
		{
			while (true)
			{
				var task = await _deviceCreationTaskQueue.DequeueAsync().ConfigureAwait(false);

				try
				{
					await task.ConfigureAwait(false);
				}
				catch (Exception ex)
				{
				}
			}
		}

		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<HidPlusPlusTransport>(ParentOrTransport);

		private void HandleNotification(ReadOnlySpan<byte> message)
		{
			if (message.Length < 7) return;

			var header = Unsafe.ReadUnaligned<RegisterAccessHeader>(ref MemoryMarshal.GetReference(message));

			// If we receive a device connect notification, it indicates a new device. (In the sense of not yet known; not necessarily a new pairing)
			// Once we create the device object, it will automatically process the notifications.
			if (header.SubId == SubId.DeviceConnect)
			{
				var parameters = Unsafe.ReadUnaligned<DeviceConnectionParameters>(ref Unsafe.AsRef(message[3]));

				// All (Quad) HID++ 2.0 should adhere to this product ID mapping for now. It is important to know this in advance because the device might be offline.
				var protocolFlavor = TryInferProductCategory(parameters.WirelessProductId, out var productCategory) && productCategory == ProductCategory.QuadFapDevice ?
					HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess :
					HidPlusPlusProtocolFlavor.RegisterAccess;

				var task = ProcessDeviceArrivalAsync(header.DeviceId, parameters.WirelessProductId, protocolFlavor);

				_deviceCreationTaskQueue.Enqueue(task);
			}
		}

		// This method can never finish synchronously because it will wait on message processing. (This is kinda important for the correct order of state updates)
		private async Task ProcessDeviceArrivalAsync(byte deviceIndex, ushort productId, HidPlusPlusProtocolFlavor protocolFlavor)
		{
			// Don't (try to) create the same device object twice. The custom state acts as some kind of lock, while also storing the associated device object.
			if (Volatile.Read(ref Transport.Devices[deviceIndex].CustomState) is null)
			{
				try
				{
					var task = CreateAsync(this, Transport, protocolFlavor, productId, deviceIndex, null, default);

					Volatile.Write(ref Transport.Devices[deviceIndex].CustomState, task);

					await task;
				}
				catch
				{
					Volatile.Write(ref Transport.Devices[deviceIndex].CustomState, null);
					throw;
				}
			}
		}

		// NB: We don't need to unregister our notification handler here, since the transport is owned by the current instance.
		public override ValueTask DisposeAsync() => Transport.DisposeAsync();
	}

	public sealed class UnifyingReceiver : RegisterAccessReceiver
	{
		internal UnifyingReceiver(HidPlusPlusTransport transport, byte deviceIndex, ushort productId, string? friendlyName, string? serialNumber)
			: base(transport, deviceIndex, productId, friendlyName, serialNumber)
		{
		}
	}

	public sealed class BoltReceiver : RegisterAccessReceiver
	{
		internal BoltReceiver(HidPlusPlusTransport transport, byte deviceIndex, ushort productId, string? friendlyName, string? serialNumber)
			: base(transport, deviceIndex, productId, friendlyName, serialNumber)
		{
		}
	}

	public sealed class RegisterAccessDirect : RegisterAccess
	{
		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<HidPlusPlusTransport>(ParentOrTransport);

		internal RegisterAccessDirect(HidPlusPlusTransport transport, byte deviceIndex, ushort productId, string? friendlyName, string? serialNumber)
			: base(transport, deviceIndex, productId, friendlyName, serialNumber)
		{
		}

		public override ValueTask DisposeAsync() => Transport.DisposeAsync();
	}

	public class RegisterAccessThroughReceiver : RegisterAccess
	{
		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<RegisterAccessReceiver>(ParentOrTransport).Transport;

		internal RegisterAccessThroughReceiver(RegisterAccessReceiver parent, byte deviceIndex, ushort productId, string? friendlyName, string? serialNumber)
			: base(parent, deviceIndex, productId, friendlyName, serialNumber)
		{
			Transport.Devices[deviceIndex].NotificationReceived += HandleNotification;
		}

		private void HandleNotification(ReadOnlySpan<byte> message)
		{
		}

		public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	public abstract class FeatureAccess : HidPlusPlusDevice
	{
		private ReadOnlyDictionary<HidPlusPlusFeature, byte>? _cachedFeatures;

		private protected FeatureAccess(object parentOrTransport, byte deviceIndex, ushort productId, string? friendlyName, string? serialNumber)
			: base(parentOrTransport, deviceIndex, productId, friendlyName, serialNumber)
		{
		}

		public override HidPlusPlusProtocolFlavor ProtocolFlavor => HidPlusPlusProtocolFlavor.FeatureAccess;

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

	public sealed class FeatureAccessDirect : FeatureAccess
	{
		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<HidPlusPlusTransport>(ParentOrTransport);

		internal FeatureAccessDirect(HidPlusPlusTransport transport, byte deviceIndex, ushort productId, string? friendlyName, string? serialNumber)
			: base(transport, deviceIndex, productId, friendlyName, serialNumber)
		{
		}

		public override ValueTask DisposeAsync() => Transport.DisposeAsync();
	}

	public class FeatureAccessThroughReceiver : FeatureAccess
	{
		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<RegisterAccessReceiver>(ParentOrTransport).Transport;

		internal FeatureAccessThroughReceiver(RegisterAccessReceiver parent, byte deviceIndex, ushort productId, string? friendlyName, string? serialNumber)
			: base(parent.Transport, deviceIndex, productId, friendlyName, serialNumber)
		{
			Transport.Devices[deviceIndex].NotificationReceived += HandleNotification;
		}

		private void HandleNotification(ReadOnlySpan<byte> message)
		{
		}

		public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
