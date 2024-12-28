using System.ComponentModel;
using System.Runtime.CompilerServices;
using DeviceTools.Usb;

namespace DeviceTools.WinUsb;

public static class WinUsbDeviceExtensions
{
	// As one would expect, WinUSB.dll is a thin wrapper directly forwarding most calls to the WinUSB driver.
	// Here, we basically reimplement WinUSB.dll by relying on the underlying WinUSB IOCTLs, as this is much easier to get it to work that way.
	// As the work has already been done to provide async IOCTls using .NET internals, it is better to rely on this rather than adding yet another layer of wrappers.
	// Hopefully, the IOCTL codes will not change between versions. Judging by the look of them, they shouldn't.

	private const int WriteIsochronousPipeIoControlCode = 0x3500076;
	private const int ReadIsochronousPipeIoControlCode = 0x350007a;

	private const int GetDescriptorIoControlCode = 0x350c004;
	private const int SetCurrentAlternateSettingIoControlCode = 0x350c008;
	private const int SetPipePolicyIoControlCode = 0x350c00c;
	private const int ResetPipeIoControlCode = 0x350c024;
	private const int AbortPipeIoControlCode = 0x350c028;
	private const int SetPowerPolicyIoControlCode = 0x350c02c;
	private const int ControlTransferIoControlCode = 0x350c03a;
	private const int QueryDeviceInformationIoControlCode = 0x350c04c;
	private const int GetPipePolicyIoControlCode = 0x350c050;
	private const int GetPowerPolicyIoControlCode = 0x350c058;
	private const int GetCurrentAlternateSettingIoControlCode = 0x350c05c;
	private const int StartTrackingForTimeSyncIoControlCode = 0x350c080;
	private const int GetCurrentFrameNumberAndQpcIoControlCode = 0x350c084;
	private const int StopTrackingForTimeSyncIoControlCode = 0x350c088;

	private const int ReadPipeIoControlCode = 0x350401e;
	private const int FlushPipeIoControlCode = 0x3504048;
	private const int RegisterIsochronousBufferInIoControlCode = 0x3504072;
	private const int GetCurrentFrameNumberIoControlCode = 0x350407c;

	private const int WritePipeIoControlCode = 0x3508021;
	private const int RegisterIsochronousBufferOutIoControlCode = 0x350806e;

	// No idea what those IOCTLs are yet.
	private const int InitializeIoControlCode1 = 0x350c068;
	private const int InitializeIoControlCode2 = 0x350c03c;

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public static unsafe ValueTask<int> GetRawDescriptorAsync
	(
		this DeviceStream device,
		UsbDescriptorType descriptorType,
		byte index,
		ushort languageId,
		Memory<byte> buffer,
		CancellationToken cancellationToken
	)
	{
		// TODO: Make this use native memory instead, in order to avoid garbage. (Need to provide unsafe IOCTL methods, which might in fact not be cheap)
		var inputBuffer = GC.AllocateUninitializedArray<byte>(4, false);
		inputBuffer[0] = (byte)descriptorType;
		inputBuffer[1] = index;
		Unsafe.As<byte, ushort>(ref inputBuffer[2]) = languageId;

		return device.IoControlAsync(GetDescriptorIoControlCode, inputBuffer, buffer, cancellationToken);
	}

	/// <summary>Gets the USB device descriptor of the specified device.</summary>
	/// <param name="device">The device on which the descriptor must be retrieved.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The USB device descriptor.</returns>
	public static async ValueTask<UsbDeviceDescriptor> GetDeviceDescriptorAsync(this DeviceStream device, CancellationToken cancellationToken)
	{
		// TODO: Make this use native memory instead, in order to avoid garbage. (Need to provide unsafe IOCTL methods, which might in fact not be cheap)
		var buffer = new byte[22];
		buffer[0] = (byte)UsbDescriptorType.Device;

		_ = await device.IoControlAsync(GetDescriptorIoControlCode, buffer.AsMemory(0, 4), buffer.AsMemory(4), cancellationToken).ConfigureAwait(false);

		return Unsafe.As<byte, UsbDeviceDescriptor>(ref buffer[4]);
	}

	/// <summary>Gets the USB configuration descriptor of the specified device, without any extra data.</summary>
	/// <param name="device">The device on which the descriptor must be retrieved.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The USB device descriptor.</returns>
	public static async ValueTask<UsbConfigurationDescriptor> GetConfigurationDescriptorAsync(this DeviceStream device, CancellationToken cancellationToken)
	{
		// TODO: Make this use native memory instead, in order to avoid garbage. (Need to provide unsafe IOCTL methods, which might in fact not be cheap)
		var buffer = new byte[13];
		buffer[0] = (byte)UsbDescriptorType.Configuration;

		_ = await device.IoControlAsync(GetDescriptorIoControlCode, buffer.AsMemory(0, 4), buffer.AsMemory(4), cancellationToken).ConfigureAwait(false);

		return Unsafe.As<byte, UsbConfigurationDescriptor>(ref buffer[4]);
	}

	/// <summary>Gets the USB configuration descriptor of the specified device, including any extra data.</summary>
	/// <param name="device">The device on which the descriptor must be retrieved.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The USB device descriptor.</returns>
	public static async ValueTask<UsbConfiguration> GetConfigurationAsync(this DeviceStream device, CancellationToken cancellationToken)
	{
		// TODO: Make this use native memory instead, in order to avoid garbage. (Need to provide unsafe IOCTL methods, which might in fact not be cheap)
		var buffer = new byte[13];
		buffer[0] = (byte)UsbDescriptorType.Configuration;

		_ = await device.IoControlAsync(GetDescriptorIoControlCode, buffer.AsMemory(0, 4), buffer.AsMemory(4), cancellationToken).ConfigureAwait(false);

		var resultBuffer = new byte[Unsafe.As<byte, UsbConfigurationDescriptor>(ref buffer[4]).TotalLength];

		_ = await device.IoControlAsync(GetDescriptorIoControlCode, buffer.AsMemory(0, 4), resultBuffer, cancellationToken).ConfigureAwait(false);

		return new(resultBuffer);
	}
}
