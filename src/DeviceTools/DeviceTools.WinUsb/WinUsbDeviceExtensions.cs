using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
	public static ValueTask<int> GetRawDescriptorAsync
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

	/// <summary>Gets the USB device descriptor of the specified WinUSB device.</summary>
	/// <param name="device">The WinUSB device on which the descriptor must be retrieved.</param>
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

	/// <summary>Gets the USB configuration descriptor of the specified WinUSB device, without any extra data.</summary>
	/// <param name="device">The WinUSB device on which the descriptor must be retrieved.</param>
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

	/// <summary>Gets the USB configuration descriptor of the specified WinUSB device, including any extra data.</summary>
	/// <param name="device">The WinUSB device on which the descriptor must be retrieved.</param>
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

	/// <summary>Writes data to a pipe of a WinUSB device.</summary>
	/// <param name="device">The WinUSB device to which the pipe belongs.</param>
	/// <param name="interfaceIndex">The index of the interface as returned by .</param>
	/// <param name="address">The pipe address.</param>
	/// <param name="buffer">The buffer containing the data that must be written.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The number of bytes written to the pipe.</returns>
	public static async ValueTask<int> WritePipeAsync(this DeviceStream device, byte interfaceIndex, byte address, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
	{
		// TODO: Make this use native memory instead, in order to avoid garbage. (Need to provide unsafe IOCTL methods, which might in fact not be cheap)
		var inputBuffer = new byte[2];
		// This stuff with the interface index is kinda weird, but it should be correct
		inputBuffer[0] = interfaceIndex > 0 ? (byte)(interfaceIndex + 1) : (byte)0;
		inputBuffer[1] = address;

		return await device.IoControlAsync(WritePipeIoControlCode, inputBuffer, MemoryMarshal.AsMemory(buffer), cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Reads data from a USB pipe of a WinUSB device.</summary>
	/// <param name="device">The WinUSB device to which the pipe belongs.</param>
	/// <param name="interfaceIndex">The index of the interface in the list of interfaces.</param>
	/// <param name="address">The pipe address.</param>
	/// <param name="buffer">The buffer that will hold the data that has been read.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The number of bytes read from the pipe.</returns>
	public static async ValueTask<int> ReadPipeAsync(this DeviceStream device, byte interfaceIndex, byte address, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		// TODO: Make this use native memory instead, in order to avoid garbage. (Need to provide unsafe IOCTL methods, which might in fact not be cheap)
		var inputBuffer = new byte[2];
		// This stuff with the interface index is kinda weird, but it should be correct
		inputBuffer[0] = interfaceIndex > 0 ? (byte)(interfaceIndex + 1) : (byte)0;
		inputBuffer[1] = address;

		return await device.IoControlAsync(ReadPipeIoControlCode, inputBuffer, buffer, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Resets a USB pipe of a WinUSB device.</summary>
	/// <param name="device">The WinUSB device to which the pipe belongs.</param>
	/// <param name="interfaceIndex">The index of the interface in the list of interfaces.</param>
	/// <param name="address">The pipe address.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The number of bytes read from the pipe.</returns>
	public static async ValueTask ResetPipeAsync(this DeviceStream device, byte interfaceIndex, byte address, CancellationToken cancellationToken)
	{
		// TODO: Make this use native memory instead, in order to avoid garbage. (Need to provide unsafe IOCTL methods, which might in fact not be cheap)
		var inputBuffer = new byte[2];
		// This stuff with the interface index is kinda weird, but it should be correct
		inputBuffer[0] = interfaceIndex > 0 ? (byte)(interfaceIndex + 1) : (byte)0;
		inputBuffer[1] = address;

		await device.IoControlAsync(ResetPipeIoControlCode, (ReadOnlyMemory<byte>)inputBuffer, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Aborts a USB pipe of a WinUSB device.</summary>
	/// <param name="device">The WinUSB device to which the pipe belongs.</param>
	/// <param name="interfaceIndex">The index of the interface in the list of interfaces.</param>
	/// <param name="address">The pipe address.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The number of bytes read from the pipe.</returns>
	public static async ValueTask AbortPipeAsync(this DeviceStream device, byte interfaceIndex, byte address, CancellationToken cancellationToken)
	{
		// TODO: Make this use native memory instead, in order to avoid garbage. (Need to provide unsafe IOCTL methods, which might in fact not be cheap)
		var inputBuffer = new byte[2];
		// This stuff with the interface index is kinda weird, but it should be correct
		inputBuffer[0] = interfaceIndex > 0 ? (byte)(interfaceIndex + 1) : (byte)0;
		inputBuffer[1] = address;

		await device.IoControlAsync(AbortPipeIoControlCode, (ReadOnlyMemory<byte>)inputBuffer, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Flushes a USB pipe of a WinUSB device.</summary>
	/// <param name="device">The WinUSB device to which the pipe belongs.</param>
	/// <param name="interfaceIndex">The index of the interface in the list of interfaces.</param>
	/// <param name="address">The pipe address.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The number of bytes read from the pipe.</returns>
	public static async ValueTask FlushPipeAsync(this DeviceStream device, byte interfaceIndex, byte address, CancellationToken cancellationToken)
	{
		// TODO: Make this use native memory instead, in order to avoid garbage. (Need to provide unsafe IOCTL methods, which might in fact not be cheap)
		var inputBuffer = new byte[2];
		// This stuff with the interface index is kinda weird, but it should be correct
		inputBuffer[0] = interfaceIndex > 0 ? (byte)(interfaceIndex + 1) : (byte)0;
		inputBuffer[1] = address;

		await device.IoControlAsync(FlushPipeIoControlCode, (ReadOnlyMemory<byte>)inputBuffer, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Queries information on a WinUSB device.</summary>
	/// <param name="device">The WinUSB device to which the pipe belongs.</param>
	/// <param name="informationType">The type of information to retrieve.</param>
	/// <param name="buffer">The buffer that will hold the data that has been read.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The number of bytes read from the pipe.</returns>
	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public static async ValueTask<int> QueryDeviceInformationAsync(this DeviceStream device, UsbDeviceInformationType informationType, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		// TODO: Make this use native memory instead, in order to avoid garbage. (Need to provide unsafe IOCTL methods, which might in fact not be cheap)
		var inputBuffer = new byte[4];
		Unsafe.As<byte, uint>(ref inputBuffer[0]) = (uint)informationType;

		return await device.IoControlAsync(QueryDeviceInformationIoControlCode, inputBuffer, buffer, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Gets the speed of a WinUSB device.</summary>
	/// <remarks>
	/// The device speed can also be retrieved by calling <see cref="QueryDeviceInformationAsync(DeviceStream, UsbDeviceInformationType, Memory{byte}, CancellationToken)"/> with
	/// <see cref="UsbDeviceInformationType.DeviceSpeed"/>, but this call should be slightly more efficient.
	/// </remarks>
	/// <param name="device">The WinUSB device to which the pipe belongs.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The number of bytes read from the pipe.</returns>
	public static async ValueTask<UsbDeviceSpeed> GetDeviceSpeedAsync(this DeviceStream device, CancellationToken cancellationToken)
	{
		// TODO: Make this use native memory instead, in order to avoid garbage. (Need to provide unsafe IOCTL methods, which might in fact not be cheap)
		var buffer = new byte[5];
		Unsafe.As<byte, uint>(ref buffer[0]) = (uint)UsbDeviceInformationType.DeviceSpeed;

		_ = await device.IoControlAsync(QueryDeviceInformationIoControlCode, buffer.AsMemory(0, 4), buffer.AsMemory(4), cancellationToken).ConfigureAwait(false);

		return (UsbDeviceSpeed)buffer[4];
	}
}
