using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.HumanInterfaceDevices.Usages;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Lighting;
using Exo.Features.Mouses;
using Exo.Lighting;
using Exo.Lighting.Effects;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Razer;

// This is a tentative driver for DeathAdder 3.5G.
public sealed partial class RazerDeathAdder35GDeviceDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<ILightingDeviceFeature>,
	IDeviceDriver<IMouseDeviceFeature>,
	IDeviceIdFeature,
	IMouseConfigurablePollingFrequencyFeature,
	ILightingControllerFeature,
	ILightingDeferredChangesFeature,
	IMouseDpiPresetsFeature
{
	// I believe the original intent in the driver was for us to send the whole URB contents,
	// but this data will actually be entirely discarded and recreated by the driver, which is likely safer.
	private static ReadOnlySpan<byte> UrbSetupData => [0x21, 0x09, 0x10, 0x00, 0x00, 0x00, 0x04, 0x00];

	private const int RazerDeviceEnumerationIoControlCode = unchecked((int)0x88883000);
	private const int RazerSpecialFunctionsIoControlCode = unchecked((int)0x88883004);
	private const int RazerDriverEventIoControlCode = unchecked((int)0x88883008);
	private const int RazerUrbIoControlCode = unchecked((int)0x88883020);

	private const ushort RazerVendorId = 0x1532;
	private const ushort DeathAdder35GProductId = 0x0016;

	private static readonly ImmutableArray<ushort> PollingFrequencies = [125, 500, 1000];

	private static readonly Guid WheelLightingZoneGuid = new(0xEF92DD34, 0x3DE7, 0x4D22, 0xB4, 0x6E, 0x02, 0x34, 0xCD, 0x86, 0xFF, 0x25);
	private static readonly Guid LogoLightingZoneGuid = new(0x531208F2, 0x499B, 0x4779, 0x82, 0xEE, 0x8E, 0x8A, 0xD2, 0xAA, 0xE4, 0xC6);

	private static readonly ImmutableArray<DotsPerInch> DpiPresets3500 = [new(450), new(900), new(3500)];
	private static readonly ImmutableArray<DotsPerInch> DpiPresets1800 = [new(450), new(900), new(1800)];

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[ProductId(VendorIdSource.Usb, RazerVendorId, DeathAdder35GProductId)]
	public static async Task<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		string friendlyName,
		ushort productId,
		ushort version,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		ImmutableArray<DeviceObjectInformation> devices,
		string topLevelDeviceName,
		ILoggerFactory loggerFactory,
		CancellationToken cancellationToken
	)
	{
		string? mouseDeviceInterfaceName = null;
		string? keyboardDeviceInterfaceName = null;
		string? usbDeviceInterfaceName = null;

		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];

			if (!deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid interfaceClassGuid))
			{
				continue;
			}

			if (interfaceClassGuid == DeviceInterfaceClassGuids.UsbDevice)
			{
				usbDeviceInterfaceName = deviceInterface.Id;
			}
			else if (interfaceClassGuid == DeviceInterfaceClassGuids.Hid &&
				deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage) &&
				usagePage == (ushort)HidUsagePage.GenericDesktop &&
				deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId))
			{
				if (usageId == (ushort)HidGenericDesktopUsage.Mouse)
				{
					mouseDeviceInterfaceName = deviceInterface.Id;
				}
				else if (usageId == (ushort)HidGenericDesktopUsage.Keyboard)
				{
					keyboardDeviceInterfaceName = deviceInterface.Id;
				}
			}
		}

		//uint deviceAddress = uint.MaxValue;
		for (int i = 0; i < devices.Length; i++)
		{
			var device = devices[i];

			if (device.Properties.TryGetValue(Properties.System.Devices.BusTypeGuid.Key, out Guid busTypeGuid) && busTypeGuid == DeviceBusTypeGuids.Usb)
			{
				if (!device.Properties.TryGetValue(Properties.System.Devices.LowerFilters.Key, out string[]? lowLevelFilters) ||
					!lowLevelFilters.Contains("rzdaendpt")) return null;

				//if (!device.Properties.TryGetValue(Properties.System.Devices.Address.Key, out deviceAddress))
				//{
				//	throw new InvalidOperationException("Could not find the bus address of the device.");
				//}
			}
		}

		//string endPointDeviceName = @$"\\.\RzEpt_Pid0016&{deviceAddress:X8}";

		if (usbDeviceInterfaceName is null || mouseDeviceInterfaceName is null)
		{
			throw new InvalidOperationException("The devices interface for the device were not found.");
		}

		string razerDriverDeviceName;

		var buffers = GC.AllocateUninitializedArray<byte>(24 + 32, true);
		using (var razerUddDevice = new HidDeviceStream(Device.OpenHandle(@"\\.\RzUdd", DeviceAccess.None, FileShare.ReadWrite), FileAccess.ReadWrite, 0, true))
		{
			var inputBuffer = MemoryMarshal.CreateFromPinnedArray(buffers, 0, 24);
			var outputBuffer = MemoryMarshal.CreateFromPinnedArray(buffers, 24, 32);

			inputBuffer.Span[8] = 2;
			inputBuffer.Span[12] = 1;

			int index = 0;
			while (true)
			{
				Unsafe.As<byte, int>(ref inputBuffer.Span[16]) = index;
				await razerUddDevice.IoControlAsync(RazerDeviceEnumerationIoControlCode, inputBuffer, outputBuffer, cancellationToken).ConfigureAwait(false);
				if (Unsafe.As<byte, ushort>(ref outputBuffer.Span[16]) == productId)
				{
					razerDriverDeviceName = string.Create(CultureInfo.InvariantCulture, @$"\\.\Razer_00000000{Unsafe.As<byte, int>(ref outputBuffer.Span[8]):X8}");
					break;
				}

				if (++index < 0) throw new InvalidOperationException("Tried too many indices and didn't find the device.");
			}
		}

		var razerDevice = new HidDeviceStream(Device.OpenHandle(razerDriverDeviceName, DeviceAccess.None, FileShare.ReadWrite), FileAccess.ReadWrite, 0, true);
		//try
		//{
		//	Array.Clear(buffers);

		//	var inputBuffer = MemoryMarshal.CreateFromPinnedArray(buffers, 0, 24);
		//	var outputBuffer = MemoryMarshal.CreateFromPinnedArray(buffers, 24, 16);

		//	// This is likely a function ID in the driver. Various functions have a different input length, but that's all I know.
		//	inputBuffer.Span[12] = 0x7;
		//	inputBuffer.Span[8] = 3;

		//	await razerDevice.IoControlAsync(RazerSpecialFunctionsIoControlCode, inputBuffer, outputBuffer, cancellationToken).ConfigureAwait(false);
		//}
		//catch
		//{
		//	razerDevice.Dispose();
		//	throw;
		//}

		var driver = new RazerDeathAdder35GDeviceDriver
		(
			razerDevice,
			DpiPresets3500,
			version,
			friendlyName,
			topLevelDeviceName
		);

		return new DriverCreationResult<SystemDevicePath>(keys, driver, null);
	}

	// This is a device exposed by the razer kernel driver to access features of the physical device.
	private readonly DeviceStream _controlDevice;
	private readonly AsyncLock _lock;
	private readonly byte[] _buffer;

	private readonly ushort _versionNumber;
	// The whole mouse state should fit in a single byte, but we'll separate it between lighting and non-lighting for atomicity of updates.
	private byte _lightingState;
	private byte _performanceState;

	private readonly ImmutableArray<DotsPerInch> _dpiPresets;
	private readonly IReadOnlyCollection<ILightingZone> _lightingZones;

	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<IMouseDeviceFeature> _mouseFeatures;
	private readonly IDeviceFeatureSet<ILightingDeviceFeature> _lightingFeatures;

	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _eventProcessingTask;

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => _mouseFeatures;
	IDeviceFeatureSet<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;

	DeviceId IDeviceIdFeature.DeviceId => new(DeviceIdSource.Usb, VendorIdSource.Usb, RazerVendorId, DeathAdder35GProductId, _versionNumber);

	public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

	private event Action<Driver, MouseDpiStatus> DpiChanged;

	private RazerDeathAdder35GDeviceDriver
	(
		HidDeviceStream controlDevice,
		ImmutableArray<DotsPerInch> dpiPresets,
		ushort versionNumber,
		string friendlyName,
		string topLevelDeviceName
	) : base(friendlyName, new("da35g", topLevelDeviceName, $"{RazerVendorId:X4}:{DeathAdder35GProductId:X4}", null))
	{
		_controlDevice = controlDevice;
		_lock = new();
		_buffer = GC.AllocateUninitializedArray<byte>(24 + 640, true);
		UrbSetupData.CopyTo(_buffer);
		_versionNumber = versionNumber;
		_lightingState = 0x0F;
		_performanceState = 0x05;
		_dpiPresets = dpiPresets;
		_lightingZones = Array.AsReadOnly<ILightingZone>([new WheelLightingZone(this), new LogoLightingZone(this)]);
		_genericFeatures = FeatureSet.Create<IGenericDeviceFeature, RazerDeathAdder35GDeviceDriver, IDeviceIdFeature>(this);
		_mouseFeatures = FeatureSet.Create<IMouseDeviceFeature, RazerDeathAdder35GDeviceDriver, IMouseConfigurablePollingFrequencyFeature, IMouseDpiPresetsFeature>(this);
		_lightingFeatures = FeatureSet.Create<ILightingDeviceFeature, RazerDeathAdder35GDeviceDriver, ILightingControllerFeature, ILightingDeferredChangesFeature>(this);
		_cancellationTokenSource = new();
		_eventProcessingTask = ReceiveDriverEventsAsync(_cancellationTokenSource.Token);
	}

	public override async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;

		cts.Cancel();
		await _controlDevice.DisposeAsync();
		try
		{
			await _eventProcessingTask;
		}
		catch
		{
		}
		cts.Dispose();
	}

	IReadOnlyCollection<ILightingZone> ILightingControllerFeature.LightingZones => _lightingZones;

	ushort IMouseConfigurablePollingFrequencyFeature.PollingFrequency => PollingFrequencies[3 - (_performanceState & 3)];

	ImmutableArray<ushort> IMouseConfigurablePollingFrequencyFeature.SupportedPollingFrequencies => PollingFrequencies;

	private Task ReceiveDriverEventsAsync(CancellationToken cancellationToken)
	{
		var tasks = new Task[10];
		for (int i = 0; i < tasks.Length; i++)
		{
			tasks[i] = ReceiveDriverEventsAsync(i, cancellationToken);
		}
		return Task.WhenAll(tasks);
	}

	private async Task ReceiveDriverEventsAsync(int index, CancellationToken cancellationToken)
	{
		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffer, 24 + index * 64, 64);
		while (true)
		{
			buffer.Span.Clear();
			try
			{
				await _controlDevice.IoControlAsync(RazerDriverEventIoControlCode, buffer, buffer, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
		}
	}

	async ValueTask IMouseConfigurablePollingFrequencyFeature.SetPollingFrequencyAsync(ushort pollingFrequency, CancellationToken cancellationToken)
	{
		byte newPollingState = pollingFrequency switch
		{
			125 => 3,
			500 => 2,
			1000 => 1,
			_ => throw new ArgumentOutOfRangeException(nameof(pollingFrequency)),
		};
		if ((_performanceState & 3) == newPollingState) return;
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			await ApplySettingsAsync(newPollingState, (byte)((_performanceState >> 2) & 0x3), (byte)((_lightingState >> 2) & 3), cancellationToken).ConfigureAwait(false);

			Volatile.Write(ref _performanceState, (byte)(_performanceState & 0xFC | newPollingState));
		}
	}

	ImmutableArray<DotsPerInch> IMouseDpiPresetsFeature.DpiPresets => _dpiPresets;

	async ValueTask IMouseDpiPresetsFeature.ChangeCurrentPresetAsync(byte activePresetIndex, CancellationToken cancellationToken)
	{
		if (activePresetIndex > 2) throw new ArgumentOutOfRangeException(nameof(activePresetIndex));

		byte newDpiState = (byte)(3 - activePresetIndex);

		if (((_performanceState >> 2) & 3) == newDpiState) return;

		Action<Driver, MouseDpiStatus>? dpiChanged;
		MouseDpiStatus newStatus;
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			await ApplySettingsAsync((byte)(_performanceState & 0x3), newDpiState, (byte)((_lightingState >> 2) & 3), cancellationToken).ConfigureAwait(false);

			Volatile.Write(ref _performanceState, (byte)(_performanceState & 0xF3 | (newDpiState << 2)));

			dpiChanged = DpiChanged;
			newStatus = new() { PresetIndex = activePresetIndex, Dpi = _dpiPresets[activePresetIndex] };
		}
		dpiChanged?.Invoke(this, newStatus);
	}

	DotsPerInch IMouseDynamicDpiFeature.MaximumDpi => _dpiPresets[^1];

	bool IMouseDynamicDpiFeature.AllowsSeparateXYDpi => false;

	event Action<Driver, MouseDpiStatus> IMouseDynamicDpiFeature.DpiChanged
	{
		add => DpiChanged += value;
		remove => DpiChanged -= value;
	}

	MouseDpiStatus IMouseDpiFeature.CurrentDpi
	{
		get
		{
			var state = Volatile.Read(ref _performanceState);

			byte dpiIndex = (byte)(3 - ((state >> 2) & 3));

			return new() { PresetIndex = dpiIndex, Dpi = _dpiPresets[dpiIndex] };
		}
	}

	private async Task ApplySettingsAsync(byte pollingRate, byte dpiIndex, byte lightingState, CancellationToken cancellationToken)
	{
		var buffer = _buffer;
		buffer[8] = pollingRate;
		buffer[9] = 1;
		buffer[10] = 1;
		buffer[11] = lightingState;
		await _controlDevice.IoControlAsync(RazerUrbIoControlCode, MemoryMarshal.CreateFromPinnedArray(buffer, 0, 12), MemoryMarshal.CreateFromPinnedArray(buffer, 12, 12), cancellationToken).ConfigureAwait(false);
	}

	LightingPersistenceMode ILightingDeferredChangesFeature.PersistenceMode => LightingPersistenceMode.NeverPersisted;

	ValueTask ILightingDeferredChangesFeature.ApplyChangesAsync(bool shouldPersist) => ApplyChangesAsync(default);

	private async ValueTask ApplyChangesAsync(CancellationToken cancellationToken)
	{
		// We can reasonably read the state outside of the lock, as lighting updates should not occur concurrently.
		byte lightingState = Volatile.Read(ref _lightingState);
		byte oldLightingState = (byte)((lightingState >> 2) & 3);
		byte newLightingState = (byte)(lightingState & 3);
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			if (oldLightingState == newLightingState) return;

			await ApplySettingsAsync((byte)(_performanceState & 3), (byte)((_performanceState >> 2) & 0x3), newLightingState, cancellationToken).ConfigureAwait(false);

			Volatile.Write(ref _lightingState, (byte)(newLightingState << 2 | newLightingState));
		}
	}

	private abstract class LightingZone : ILightingZone, ILightingZoneEffect<DisabledEffect>, ILightingZoneEffect<EnabledEffect>
	{
		private readonly RazerDeathAdder35GDeviceDriver _driver;

		protected RazerDeathAdder35GDeviceDriver Driver => _driver;

		Guid ILightingZone.ZoneId => ZoneId;

		protected abstract Guid ZoneId { get; }
		protected abstract byte Flag { get; }

		public LightingZone(RazerDeathAdder35GDeviceDriver driver) => _driver = driver;

		void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect) => _driver._lightingState = (byte)(_driver._lightingState & ~Flag);
		void ILightingZoneEffect<EnabledEffect>.ApplyEffect(in EnabledEffect effect) => _driver._lightingState = (byte)(_driver._lightingState | Flag);

		bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect)
		{
			effect = default;
			return (_driver._lightingState & Flag) == 0;
		}

		bool ILightingZoneEffect<EnabledEffect>.TryGetCurrentEffect(out EnabledEffect effect)
		{
			effect = default;
			return (_driver._lightingState & Flag) != 0;
		}

		ILightingEffect ILightingZone.GetCurrentEffect() => (_driver._lightingState & Flag) != 0 ? EnabledEffect.SharedInstance : DisabledEffect.SharedInstance;
	}

	private sealed class WheelLightingZone : LightingZone
	{
		protected override Guid ZoneId => WheelLightingZoneGuid;
		protected override byte Flag => 0x02;

		public WheelLightingZone(RazerDeathAdder35GDeviceDriver driver) : base(driver)
		{
		}
	}

	private sealed class LogoLightingZone : LightingZone
	{
		protected override Guid ZoneId => LogoLightingZoneGuid;
		protected override byte Flag => 0x01;

		public LogoLightingZone(RazerDeathAdder35GDeviceDriver driver) : base(driver)
		{
		}
	}
}