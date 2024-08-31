using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.HumanInterfaceDevices.Usages;

namespace DeviceTools.RawInput
{
	/// <summary>Describes a RawInput device.</summary>
	/// <remarks>
	/// <para>
	/// Specific functionality is exposed for mouse, keyboard or other HID devices, by derived classes <see cref="RawInputMouseDevice"/>,
	/// <see cref="RawInputKeyboardDevice"/>, <see cref="RawInputHidDevice"/> or the stronger-typed <see cref="RawInputHidDevice{TUsage}"/>.
	/// </para>
	/// <para>
	/// The type of device can be determined by reading <see cref="DeviceType"/>, but C# pattern matching can be used to determine the most appropriate type:
	/// <code>
	/// switch (device)
	/// {
	///     case RawInputMouseDevice mouse:
	///         // …
	///         break;
	///     case RawInputKeyboardDevice mouse:
	///         // …
	///         break;
	///     case RawInputHidDevice&lt;HidGameUsage&gt; game:
	///         // …
	///         break;
	///     case RawInputHidDevice hid:
	///         // …
	///         break;
	/// }
	/// </code>
	/// </para>
	/// <para>
	/// Non-keyboard and non-mouse HID devices will be automatically mapped to the corresponding generic <see cref="RawInputHidDevice{TUsage}"/> when the usages are supported by this library.
	/// In case the usage is unsupported, or cannot be mapped to an enum, the non-generic <see cref="RawInputHidDevice"/> will be used by itself.
	/// </para>
	/// <para>
	/// In any case, it is still possible to access the raw HID <see cref="Usage"/> value from the base class.
	/// </para>
	/// </remarks>
	public abstract class RawInputDevice : HidDevice
	{
		// Most fields of this class are lazy-initialized in order to avoid useless allocations.

		internal static RawInputDevice Create(RawInputDeviceCollection collection, IntPtr handle)
		{
			// We need to request device info for all HID devices that are not mouse or keyboard.
			// So, we can as well do it for mouse and keyboard and retrieve associated info pre-emptively…
			var deviceInfo = NativeMethods.GetDeviceInfo(handle);

			// For some reason, device types returned by GetRawInputDeviceList and GetRawInputDeviceInfo do not always match.
			// I do not know the meaning behind this mismatch, but one example of such as mismatch is the TrackPad in 2012 Retina MBP which returns both a device type of 3 and Mouse.
			return deviceInfo.Type switch
			{
				RawInputDeviceType.Mouse => CreateMouse(collection, handle, deviceInfo.Mouse),
				RawInputDeviceType.Keyboard => CreateKeyboard(collection, handle, deviceInfo.Keyboard),
				RawInputDeviceType.Hid => CreateHid(collection, handle, deviceInfo.Hid),
				_ => throw new InvalidOperationException($"Unsupported device type: {deviceInfo.Type}."),
			};
		}

		private static RawInputMouseDevice CreateMouse(RawInputDeviceCollection collection, IntPtr handle, NativeMethods.RawInputDeviceInfoMouse deviceInfo)
			=> new RawInputMouseDevice(collection, handle, (int)deviceInfo.Id, (int)deviceInfo.NumberOfButtons, (int)deviceInfo.SampleRate, deviceInfo.HasHorizontalWheel != 0);

		private static RawInputKeyboardDevice CreateKeyboard(RawInputDeviceCollection collection, IntPtr handle, NativeMethods.RawInputDeviceInfoKeyboard deviceInfo)
			=> new RawInputKeyboardDevice(collection, handle, (int)deviceInfo.Type, (int)deviceInfo.SubType, deviceInfo.KeyboardMode, deviceInfo.NumberOfFunctionKeys, deviceInfo.NumberOfIndicators, deviceInfo.NumberOfKeysTotal);

		private static RawInputHidDevice CreateHid(RawInputDeviceCollection collection, IntPtr handle, NativeMethods.RawInputDeviceInfoHid deviceInfo)
		{
			var (vendorId, productId) = checked(((ushort)deviceInfo.VendorId, (ushort)deviceInfo.ProductId));
			// Manually map usage pages to the appropriately strongly typed usage enum when appropriate.
			// Some usage pages don't have a corresponding enumeration (e.g. Ordinal, Unicode characters), and some usage pages may not be (yet) supported by the library.
			return deviceInfo.UsagePage switch
			{
				HidUsagePage.GenericDesktop => new RawInputHidDevice<HidGenericDesktopUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.Simulation => new RawInputHidDevice<HidSimulationUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.Vr => new RawInputHidDevice<HidVrUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.Sport => new RawInputHidDevice<HidSportUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.Game => new RawInputHidDevice<HidGameUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.GenericDevice => new RawInputHidDevice<HidGenericDeviceUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.Keyboard => new RawInputHidDevice<HidKeyboardUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.Led => new RawInputHidDevice<HidLedUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.Telephony => new RawInputHidDevice<HidTelephonyUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.Consumer => new RawInputHidDevice<HidConsumerUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.Digitizer => new RawInputHidDevice<HidDigitizerUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.Haptics => new RawInputHidDevice<HidHapticUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.EyeAndHeadTrackers => new RawInputHidDevice<HidEyeAndHeadTrackerUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.AuxiliaryDisplay => new RawInputHidDevice<HidAuxiliaryDisplayUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.Sensor => new RawInputHidDevice<HidSensorUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.MedicalInstrument => new RawInputHidDevice<HidMedicalInstrumentUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.BrailleDisplay => new RawInputHidDevice<HidBrailleDisplayUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.LightingAndIllumination => new RawInputHidDevice<HidLightingAndIlluminationUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.CameraControl => new RawInputHidDevice<HidCameraControlUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.PowerDevice => new RawInputHidDevice<HidPowerDeviceUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.BatterySystem => new RawInputHidDevice<HidBatterySystemUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				HidUsagePage.FidoAlliance => new RawInputHidDevice<HidFidoAllianceUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
				_ => new RawInputHidDevice(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, vendorId, productId, deviceInfo.VersionNumber),
			};
		}

		private readonly RawInputDeviceCollection _owner;
		internal IntPtr Handle { get; }
		private string? _deviceName;

		private protected override object Lock => _owner._lock;

		public abstract RawInputDeviceType DeviceType { get; }
		public abstract HidUsagePage UsagePage { get; }
		public ushort Usage => GetRawUsage();
		public override bool IsDisposed => _owner.IsDisposed;

		private protected RawInputDevice(RawInputDeviceCollection owner, IntPtr handle)
			=> (_owner, Handle) = (owner, handle);

		// Only called from RawInputDeviceCollection within the lock. Will case the base class destructor.
		internal void OnCollectionDisposed() => base.Dispose();

		// Maybe a bit contrieved, but in order to let the collection manage the lifetime of the instances, this method must be "neutralized".
		public sealed override void Dispose() { }

		protected abstract ushort GetRawUsage();

		public override string DeviceName => _deviceName ?? SlowGetDeviceName();

		private string SlowGetDeviceName()
		{
			if (Volatile.Read(ref _deviceName) is string value) return value;

			// We may end up allocating more than once in case this method is called concurrently, but it shouldn't matter that much.
			value = NativeMethods.GetDeviceName(Handle);

			// Give priority to the previously assigned value, if any.
			return Interlocked.CompareExchange(ref _deviceName, value, null) ?? value;
		}

		private protected override ValueTask<byte[]> GetPreparsedDataAsync(CancellationToken cancellationToken)
		{
			// Rely on RawInput to get the preparsed data for now.
			// It might be better to just rely on the base implementation in all cases, but let's revisit this later.
			try
			{
				return new(NativeMethods.GetPreparsedData(Handle));
			}
			catch (Exception)
			{
				return base.GetPreparsedDataAsync(cancellationToken);
			}
		}
	}

	/// <summary>Represents a device that is either a mouse or a keyboard.</summary>
	/// <remarks>These HID collections are reserved by the system and exposed slightly differently by Raw Input than other HID devices.</remarks>
	public abstract class RawInputSystemDevice : RawInputDevice
	{
		private DeviceId? _deviceId;

		private DeviceId SlowGetDeviceId()
		{
			if (TryResolveDeviceIdFromNames(out var deviceId))
			{
				_deviceId = deviceId;
				return deviceId;
			}
			else
			{
				deviceId = DeviceId.Invalid;
				_deviceId = deviceId;
				return deviceId;
			}
		}

		public override DeviceId DeviceId => _deviceId ?? SlowGetDeviceId();

		private protected RawInputSystemDevice(RawInputDeviceCollection owner, IntPtr handle) : base(owner, handle)
		{
		}
	}

	public sealed class RawInputMouseDevice : RawInputSystemDevice
	{
		public int Id { get; }
		public int ButtonCount { get; }
		public int SampleRate { get; }
		public bool HasHorizontalWheel { get; }

		internal RawInputMouseDevice(RawInputDeviceCollection owner, IntPtr handle, int id, int buttonCount, int sampleRate, bool hasHorizontalWheel)
			: base(owner, handle)
		{
			Id = id;
			ButtonCount = buttonCount;
			SampleRate = sampleRate;
			HasHorizontalWheel = hasHorizontalWheel;
		}

		public override RawInputDeviceType DeviceType => RawInputDeviceType.Mouse;
		public override HidUsagePage UsagePage => HidUsagePage.GenericDesktop;
		public new HidGenericDesktopUsage Usage => HidGenericDesktopUsage.Mouse;

		protected override ushort GetRawUsage() => (ushort)Usage;
	}

	public sealed class RawInputKeyboardDevice : RawInputSystemDevice
	{
		public RawInputKeyboardDevice(RawInputDeviceCollection owner, IntPtr handle, int keyboardType, int keyboardSubType, uint keyboardMode, uint functionKeyCount, uint indicatorCount, uint keyCount)
			: base(owner, handle)
		{
			KeyboardType = keyboardType;
			KeyboardSubType = keyboardSubType;
			KeyboardMode = keyboardMode;
			FunctionKeyCount = functionKeyCount;
			IndicatorCount = indicatorCount;
			KeyCount = keyCount;
		}

		public int KeyboardType { get; }
		public int KeyboardSubType { get; }
		public uint KeyboardMode { get; }
		public uint FunctionKeyCount { get; }
		public uint IndicatorCount { get; }
		public uint KeyCount { get; }

		public override RawInputDeviceType DeviceType => RawInputDeviceType.Keyboard;
		public override HidUsagePage UsagePage => HidUsagePage.GenericDesktop;
		public new HidGenericDesktopUsage Usage => HidGenericDesktopUsage.Keyboard;
		protected override ushort GetRawUsage() => (ushort)Usage;
	}

	public class RawInputHidDevice : RawInputDevice
	{
		private readonly HidUsagePage _usagePage;
		private readonly ushort _usage;
		private bool _isDeviceIdDetected;
		private DeviceId _deviceId;

		public override DeviceId DeviceId => _isDeviceIdDetected ? _deviceId : SlowGetDeviceId();

		internal RawInputHidDevice(RawInputDeviceCollection owner, IntPtr handle, HidUsagePage usagePage, ushort usage, ushort vendorId, ushort productId, uint versionNumber)
			: base(owner, handle)
		{
			_usagePage = usagePage;
			_usage = usage;
			// TODO: Remove the checked() once it has been battle-tested.
			_deviceId = new DeviceId(DeviceIdSource.Unknown, VendorIdSource.Unknown, vendorId, productId, checked((ushort)versionNumber));
		}

		public override RawInputDeviceType DeviceType => RawInputDeviceType.Hid;

		public override HidUsagePage UsagePage => _usagePage;
		protected sealed override ushort GetRawUsage() => _usage;

		private DeviceId SlowGetDeviceId()
		{
			// RawInput information should always come from the USB stack, but it is hard to guarantee.
			// So, in all cases, we'll overwrite this information with what we can find in the device name if we find anything.
			// NB: Information that we overwrite here should have the same VID & PID. What should we do if it differs?
			if (TryResolveDeviceIdFromNames(out var deviceId))
			{
				_deviceId = deviceId;
				_isDeviceIdDetected = true;
				return deviceId;
			}
			else
			{
				_isDeviceIdDetected = true;
				return _deviceId;
			}
		}
	}

	/// <summary>Base class for raw input HID devices whose usage is constrained by an enum.</summary>
	/// <typeparam name="TUsage"><see cref="ushort"/> or an enum whose underlying type is <see cref="ushort"/>.</typeparam>
	public sealed class RawInputHidDevice<TUsage> : RawInputHidDevice
		where TUsage : struct, Enum
	{
		static RawInputHidDevice()
		{
			if (Enum.GetUnderlyingType(typeof(TUsage)) != typeof(ushort))
			{
				throw new InvalidOperationException($"{typeof(TUsage)} doesn't have UInt16 underlying type.");
			}
		}

		//private static ushort ToUInt16(TUsage value) => Unsafe.As<TUsage, ushort>(ref value);
		private static TUsage ToUsage(ushort value) => Unsafe.As<ushort, TUsage>(ref value);

		public RawInputHidDevice(RawInputDeviceCollection owner, IntPtr handle, HidUsagePage usagePage, ushort usage, ushort vendorId, ushort productId, uint versionNumber)
			: base(owner, handle, usagePage, usage, vendorId, productId, versionNumber)
		{
		}

		public new TUsage Usage => ToUsage(base.Usage);
	}
}
