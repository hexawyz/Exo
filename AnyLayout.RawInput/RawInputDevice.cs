using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace AnyLayout.RawInput
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
    public abstract class RawInputDevice
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
            // Manually map usage pages to the appropriately strongly typed usage enum when appropriate.
            // Some usage pages don't have a corresponding enumeration (e.g. Ordinal, Unicode characters), and some usage pages may not be (yet) supported by the library.
            return deviceInfo.UsagePage switch
            {
                HidUsagePage.GenericDesktop => new RawInputHidDevice<HidGenericDesktopUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Simulation => new RawInputHidDevice<HidSimulationUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Vr => new RawInputHidDevice<HidVrUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Sport => new RawInputHidDevice<HidSportUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Game => new RawInputHidDevice<HidGameUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.GenericDevice => new RawInputHidDevice<HidGenericDeviceUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Keyboard => new RawInputHidDevice<HidKeyboardUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Digitizer => new RawInputHidDevice<HidDigitizerUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                HidUsagePage.Consumer => new RawInputHidDevice<HidConsumerUsage>(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
                _ => new RawInputHidDevice(collection, handle, deviceInfo.UsagePage, deviceInfo.Usage, deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.VersionNumber),
            };
        }

        private readonly RawInputDeviceCollection _owner;
        internal IntPtr Handle { get; }
        private string? _deviceName;
        private string? _productName;
        private string? _manufacturerName;
        private byte[]? _preparsedData;
        // It seems that RawInput won't return the preparsed data for top level collection opened by Windows for exclusive access…
        // Thankfully, we still have the lower-level HID (discovery) library at hand, which will not deny us this information.
        // That's kind of dumb, though, as we can't allocate the byte array ourselves in that case… (Or can we?)
        private IntPtr _preparsedDataPointer;
        private SafeFileHandle? _fileHandle;

        private object Lock => _owner._lock;

        public abstract RawInputDeviceType DeviceType { get; }
        public abstract HidUsagePage UsagePage { get; }
        public ushort Usage => GetRawUsage();
        public bool IsDisposed => _owner.IsDisposed;

        private protected RawInputDevice(RawInputDeviceCollection owner, IntPtr handle)
            => (_owner, Handle) = (owner, handle);

        // Only called from RawInputDeviceCollection within the lock.
        internal void Dispose()
        {
            if (FileHandle is SafeFileHandle fileHandle)
            {
                fileHandle.Dispose();
                // Preparsed data always requires accessing the file handle.
                if (_preparsedDataPointer != IntPtr.Zero)
                {
                    NativeMethods.HidDiscoveryFreePreparsedData(_preparsedDataPointer);
                }
            }
        }

        private void EnsureNotDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
        }

        protected abstract ushort GetRawUsage();

        public string DeviceName => _deviceName ?? SlowGetDeviceName();

        private string SlowGetDeviceName()
        {
            if (Volatile.Read(ref _deviceName) is string value) return value;

            // We may end up allocating more than once in case this method is called concurrently, but it shouldn't matter that much.
            value = NativeMethods.GetDeviceName(Handle);

            // Give priority to the previously assigned value, if any.
            return Interlocked.CompareExchange(ref _deviceName, value, null) ?? value;
        }

        private SafeFileHandle FileHandle => _fileHandle ?? SlowGetFileHandle();

        private SafeFileHandle SlowGetFileHandle()
        {
            EnsureNotDisposed();
            // The file handle should not be opened more than once. We can't use optimistic lazy initialization like in the other cases here.
            lock (Lock)
            {
                if (!(_fileHandle is SafeFileHandle fileHandle))
                {
                    EnsureNotDisposed();
                    // Try to acquire the device as R/W shared.
                    fileHandle = NativeMethods.CreateFile(DeviceName, 0, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
                    // Collections opened in exclusive mode by the OS (e.g. Keyboard and Mouse) can still be accessed without requesting read or write.
                    if (fileHandle.IsInvalid)
                    {
                        fileHandle = NativeMethods.CreateFile(DeviceName, 0, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
                    }
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

        // TODO: How should this be exposed?
        private byte[] PreparsedData => _preparsedData ?? SlowGetPreparsedData();

        private byte[] SlowGetPreparsedData()
        {
            if (Volatile.Read(ref _preparsedData) is byte[] value) return value;

            // We may end up allocating more than once in case this method is called concurrently, but it shouldn't matter that much.
            value = NativeMethods.GetPreparsedData(Handle);

            // Give priority to the previously assigned value, if any.
            return Interlocked.CompareExchange(ref _preparsedData, value, null) ?? value;
        }

        // TODO: How should this be exposed?
        private IntPtr PreparsedDataPointer
        {
            get
            {
                var preparsedData = _preparsedDataPointer;
                if (preparsedData != IntPtr.Zero) return preparsedData;
                return SlowGetNativeAllocatedPreparsedData();
            }
        }

        private IntPtr SlowGetNativeAllocatedPreparsedData()
        {
            var preparsedData = Volatile.Read(ref _preparsedDataPointer);

            if (preparsedData != IntPtr.Zero) return preparsedData;

            // We may end up allocating more than once in case this method is called concurrently, but it shouldn't matter that much.
            if (NativeMethods.HidDiscoveryGetPreparsedData(FileHandle, out preparsedData) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (preparsedData == default)
            {
                throw new InvalidOperationException();
            }

            // Give priority to the previously assigned value, if any.
            {
                var previousPreparsedData = Interlocked.CompareExchange(ref _preparsedDataPointer, preparsedData, default);

                // Free the preparsed data we just allocated because it is now redundant.
                if (previousPreparsedData != default)
                {
                    NativeMethods.HidDiscoveryFreePreparsedData(preparsedData);
                    preparsedData = previousPreparsedData;
                }
            }

            return preparsedData;
        }

        private ref byte PreparsedDataFirstByte
        {
            get
            {
                var preparsedData = PreparsedData;

                if (preparsedData.Length == 0)
                {
                    // FIXME: Is there a better way to get "ref null" ?
                    // Basically, the code below is converting "IntPtr" to "ref byte", so that we can have an equivalent API ignoring the origin of preparsed data.
                    return ref Unsafe.Add(ref default(Span<byte>).GetPinnableReference(), PreparsedDataPointer);
                }

                return ref preparsedData[0];
            }
        }

        // TODO: Wrap this in a high level structure.
        public NativeMethods.HidParsingLinkCollectionNode[] GetLinkCollectionNodes()
        {
            ref byte preparsedDataFirstByte = ref PreparsedDataFirstByte;

            NativeMethods.HidParsingGetCaps(ref preparsedDataFirstByte, out var caps);

            uint count = caps.LinkCollectionNodesCount;

            if (caps.LinkCollectionNodesCount == 0)
            {
                return Array.Empty<NativeMethods.HidParsingLinkCollectionNode>();
            }

            var nodes = new NativeMethods.HidParsingLinkCollectionNode[count];
            if (NativeMethods.HidParsingGetLinkCollectionNodes(ref nodes[0], ref count, ref preparsedDataFirstByte) != NativeMethods.HidParsingResult.Success)
            {
                throw new InvalidOperationException();
            }
            return nodes;
        }

        // TODO: Wrap this in a high level structure.
        public NativeMethods.HidParsingButtonCaps[] GetButtonCapabilities(NativeMethods.HidParsingReportType reportType)
        {
            ref byte preparsedDataFirstByte = ref PreparsedDataFirstByte;

            NativeMethods.HidParsingGetCaps(ref preparsedDataFirstByte, out var caps);

            ushort count = reportType switch
            {
                NativeMethods.HidParsingReportType.Input => caps.InputButtonCapsCount,
                NativeMethods.HidParsingReportType.Output => caps.OutputButtonCapsCount,
                NativeMethods.HidParsingReportType.Feature => caps.FeatureButtonCapsCount,
                _ => throw new ArgumentOutOfRangeException(nameof(reportType))
            };

            if (count == 0)
            {
                return Array.Empty<NativeMethods.HidParsingButtonCaps>();
            }

            var buttonCaps = new NativeMethods.HidParsingButtonCaps[count];

            if (NativeMethods.HidParsingGetButtonCaps(reportType, ref buttonCaps[0], ref count, ref preparsedDataFirstByte) != NativeMethods.HidParsingResult.Success)
            {
                throw new InvalidOperationException();
            }
            return buttonCaps;
        }

        // TODO: Wrap this in a high level structure.
        public NativeMethods.HidParsingValueCaps[] GetValueCapabilities(NativeMethods.HidParsingReportType reportType)
        {
            ref byte preparsedDataFirstByte = ref PreparsedDataFirstByte;

            NativeMethods.HidParsingGetCaps(ref preparsedDataFirstByte, out var caps);

            ushort count = reportType switch
            {
                NativeMethods.HidParsingReportType.Input => caps.InputValueCapsCount,
                NativeMethods.HidParsingReportType.Output => caps.OutputValueCapsCount,
                NativeMethods.HidParsingReportType.Feature => caps.FeatureValueCapsCount,
                _ => throw new ArgumentOutOfRangeException(nameof(reportType))
            };

            if (count == 0)
            {
                return Array.Empty<NativeMethods.HidParsingValueCaps>();
            }

            var valueCaps = new NativeMethods.HidParsingValueCaps[count];

            if (NativeMethods.HidParsingGetValueCaps(reportType, ref valueCaps[0], ref count, ref preparsedDataFirstByte) != NativeMethods.HidParsingResult.Success)
            {
                throw new InvalidOperationException();
            }
            return valueCaps;
        }

        // TODO: Wrap this in a high level structure.
        public string GetString(int index)
            => NativeMethods.GetIndexedString(FileHandle, (uint)index);

        public PhysicalDescriptorSetCollection GetPhysicalDescriptorSets()
            => NativeMethods.GetPhysicalDescriptor(FileHandle);
    }

    public sealed class RawInputMouseDevice : RawInputDevice
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

    public sealed class RawInputKeyboardDevice : RawInputDevice
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
        public uint VendorId { get; }
        public uint ProductId { get; }
        public uint VersionNumber { get; }

        internal RawInputHidDevice(RawInputDeviceCollection owner, IntPtr handle, HidUsagePage usagePage, ushort usage, uint vendorId, uint productId, uint versionNumber)
            : base(owner, handle)
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

        public RawInputHidDevice(RawInputDeviceCollection owner, IntPtr handle, HidUsagePage usagePage, ushort usage, uint vendorId, uint productId, uint versionNumber)
            : base(owner, handle, usagePage, usage, vendorId, productId, versionNumber)
        {
        }

        public new TUsage Usage => ToUsage(base.Usage);
    }
}
