using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace AnyLayout.RawInput
{
    public abstract class RawInputDevice
    {
        // All fields of this class are lazy-initialized in order to avoid useless allocations.

        // Try to reuse the same instances of RawInputDevice across requests to enumerate devices.
        private static Dictionary<IntPtr, RawInputDevice> _knownDevices = new Dictionary<IntPtr, RawInputDevice>();

        public static RawInputDevice[] GetAllDevices()
        {
            var nativeDevices = NativeMethods.GetDevices();
            var previouslyKnownDevices = Volatile.Read(ref _knownDevices);
            var knownDevices = new Dictionary<IntPtr, RawInputDevice>();

            var devices = new RawInputDevice[nativeDevices.Length];
            for (int i = 0; i < nativeDevices.Length; i++)
            {
                var nativeDevice = nativeDevices[i];
                if (!previouslyKnownDevices.TryGetValue(nativeDevice.Handle, out devices[i]))
                {
                    devices[i] = Create(nativeDevice.Handle, nativeDevice.Type);
                }
                knownDevices.Add(nativeDevice.Handle, devices[i]);
            }

            return devices;
        }

        private static RawInputDevice Create(IntPtr handle, RawInputDeviceType deviceType)
        {
            // We need to request device info for all HID devices that are not mouse or keyboard.
            // So, we can as well do it for mouse and keyboard and retrieve associated info pre-emptively…
            var deviceInfo = NativeMethods.GetDeviceInfo(handle);

            // For some reason, device types returned by GetRawInputDeviceList and GetRawInputDeviceInfo do not always match.
            // I do not know the meaning behind this mismatch, but one example of such as mismatch is the TrackPad in 2012 Retina MBP which returns both a device type of 3 and Mouse.
            return deviceInfo.Type switch
            {
                RawInputDeviceType.Mouse => CreateMouse(handle, deviceInfo.Mouse),
                RawInputDeviceType.Keyboard => CreateKeyboard(handle, deviceInfo.Keyboard),
                RawInputDeviceType.Hid => CreateHid(handle, deviceInfo.Hid),
                _ => throw new InvalidOperationException($"Unsupported device type: {deviceType}."),
            };
        }

        private static RawInputMouseDevice CreateMouse(IntPtr handle, NativeMethods.RawInputDeviceInfoMouse deviceInfo)
            => new RawInputMouseDevice(handle, (int)deviceInfo.Id, (int)deviceInfo.NumberOfButtons, (int)deviceInfo.SampleRate, deviceInfo.HasHorizontalWheel != 0);

        private static RawInputKeyboardDevice CreateKeyboard(IntPtr handle, NativeMethods.RawInputDeviceInfoKeyboard deviceInfo)
            => new RawInputKeyboardDevice(handle, (int)deviceInfo.Type, (int)deviceInfo.SubType, deviceInfo.KeyboardMode, deviceInfo.NumberOfFunctionKeys, deviceInfo.NumberOfIndicators, deviceInfo.NumberOfKeysTotal);

        private static RawInputHidDevice CreateHid(IntPtr handle, NativeMethods.RawInputDeviceInfoHid deviceInfo)
        {
            // Manually map usage pages to the appropriately strongly typed usage enum when appropriate.
            // Some usage pages don't have a corresponding enumeration (e.g. Ordinal, Unicode characters), and some usage pages may not be (yet) supported by the library.
            return deviceInfo.UsagePage switch
            {
                HidUsagePage.GenericDesktop => new RawInputHidDevice<HidGenericDesktopUsage>(handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Simulation => new RawInputHidDevice<HidSimulationUsage>(handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Vr => new RawInputHidDevice<HidVrUsage>(handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Sport => new RawInputHidDevice<HidSportUsage>(handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Game => new RawInputHidDevice<HidGameUsage>(handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.GenericDevice => new RawInputHidDevice<HidGenericDeviceUsage>(handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Keyboard => new RawInputHidDevice<HidKeyboardUsage>(handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Digitizer => new RawInputHidDevice<HidDigitizerUsage>(handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Consumer => new RawInputHidDevice<HidConsumerUsage>(handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                _ => new RawInputHidDevice(handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
            };
        }

        private readonly object _lock = new object();
        private readonly IntPtr _handle;
        private string _deviceName;
        private string _productName;
        private string _manufacturerName;
        private SafeFileHandle _fileHandle;

        public abstract RawInputDeviceType DeviceType { get; }
        public abstract HidUsagePage UsagePage { get; }
        public ushort Usage => GetRawUsage();

        private protected RawInputDevice(IntPtr handle)
            => _handle = handle;

        //public void Dispose() => Interlocked.Exchange(ref _fileHandle, null)?.Dispose();

        protected abstract ushort GetRawUsage();

        public string DeviceName => _deviceName ?? SlowGetDeviceName();

        private string SlowGetDeviceName()
        {
            if (Volatile.Read(ref _deviceName) is string value) return value;

            // We may end up allocating more than once in case this method is called concurrently, but it shouldn't matter that much.
            value = NativeMethods.GetDeviceName(_handle);

            // Give priority to the previously assigned value, if any.
            return Interlocked.CompareExchange(ref _deviceName, value, null) ?? value;
        }

        private SafeFileHandle FileHandle => _fileHandle ?? SlowGetFileHandle();

        private SafeFileHandle SlowGetFileHandle()
        {
            // The file handle should not be opened more than once. We can't use optimistic lazy initialization like in the other cases here.
            lock (_lock)
            {
                if (!(_fileHandle is SafeFileHandle fileHandle))
                {
                    fileHandle = NativeMethods.CreateFile(DeviceName, 0, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
                    Volatile.Write(ref _fileHandle, fileHandle);
                }
                return fileHandle;
            }
        }

        public string ProductName => _productName ?? SlowGetProductName();

        private string SlowGetProductName()
        {
            if (Volatile.Read(ref _productName) is string value) return value;

            // We may end up allocating more than once in case this method is called concurrently, but it shouldn't matter that much.
            value = NativeMethods.GetProductString(FileHandle);

            // Give priority to the previously assigned value, if any.
            return Interlocked.CompareExchange(ref _productName, value, null) ?? value;
        }

        public string ManufacturerName => _manufacturerName ?? SlowGetManufacturerName();

        private string SlowGetManufacturerName()
        {
            if (Volatile.Read(ref _manufacturerName) is string value) return value;

            // We may end up allocating more than once in case this method is called concurrently, but it shouldn't matter that much.
            value = NativeMethods.GetManufacturerString(FileHandle);

            // Give priority to the previously assigned value, if any.
            return Interlocked.CompareExchange(ref _manufacturerName, value, null) ?? value;
        }
    }

    public sealed class RawInputMouseDevice : RawInputDevice
    {
        public int Id { get; }
        public int ButtonCount { get; }
        public int SampleRate { get; }
        public bool HasHorizontalWheel { get; }

        internal RawInputMouseDevice(IntPtr handle, int id, int buttonCount, int sampleRate, bool hasHorizontalWheel) : base(handle)
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

    public sealed class RawInputKeyboardDevice : RawInputDevice
    {
        public RawInputKeyboardDevice(IntPtr handle, int keyboardType, int keyboardSubType, uint keyboardMode, uint functionKeyCount, uint indicatorCount, uint keyCount)
            : base(handle)
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
        public uint VendorId { get; }
        public uint ProductId { get; }
        public uint VersionNumber { get; }

        internal RawInputHidDevice(IntPtr handle, HidUsagePage usagePage, ushort usage, uint vendorId, uint productId, uint versionNumber) : base(handle)
        {
            _usagePage = usagePage;
            _usage = usage;
            VendorId = vendorId;
            ProductId = productId;
            VersionNumber = versionNumber;
        }

        public override RawInputDeviceType DeviceType => RawInputDeviceType.Hid;

        public override HidUsagePage UsagePage => _usagePage;
        protected sealed override ushort GetRawUsage() => _usage;
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

        public RawInputHidDevice(IntPtr handle, HidUsagePage usagePage, ushort usage, uint vendorId, uint productId, uint versionNumber)
            : base(handle, usagePage, usage, vendorId, productId, versionNumber)
        {
        }

        public new TUsage Usage => ToUsage(base.Usage);
    }
}
