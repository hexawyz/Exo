using System.ComponentModel;
using System.Runtime.InteropServices;
using DeviceTools.DisplayDevices.Mccs;

namespace DeviceTools.DisplayDevices;

public sealed class PhysicalMonitor : IDisposable
{
	/// <summary>Gets the native handle associated with this monitor.</summary>
	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public SafePhysicalMonitorHandle Handle { get; }

	/// <summary>Gets the description associated with this monitor.</summary>
	public string Description { get; }

	internal PhysicalMonitor(SafePhysicalMonitorHandle handle, string description)
	{
		Handle = handle;
		Description = description;
	}

	public void Dispose()
	{
		Handle.Dispose();
	}

	private static Exception GetExceptionForError(int errorCode)
	{
		switch (errorCode)
		{
		case NativeMethods.ErrorGraphicsI2cNotSupported: throw new I2cNotSupportedException();
		case NativeMethods.ErrorGraphicsI2cDeviceDoesNotExist: throw new I2cDeviceNotFoundException();
		case NativeMethods.ErrorGraphicsI2cErrorTransmittingData: throw new I2cTransmissionException();
		case NativeMethods.ErrorGraphicsI2cErrorReceivingData: throw new I2cReceptionException();
		case NativeMethods.ErrorGraphicsDdcCiInvalidMessageCommand: throw new InvalidDdcCiMessageCommandException();
		case NativeMethods.ErrorGraphicsDdcCiInvalidMessageLength: throw new InvalidDdcCiMessageLengthException();
		case NativeMethods.ErrorGraphicsDdcCiInvalidMessageChecksum: throw new InvalidDdcCiMessageChecksumException();
		case NativeMethods.ErrorGraphicsMonitorNoLongerExists: throw new MonitorNoLongerExistsException();
		default: throw new Win32Exception(errorCode);
		}
	}

	private static byte[] GetNullTerminatedBytes(ReadOnlySpan<byte> buffer)
	{
		int length = buffer.IndexOf((byte)0);
		return (length >= 0 ? buffer[..length] : buffer).ToArray();
	}

	/// <summary>Tries to get the raw Capabilities string for the monitor.</summary>
	/// <remarks>
	/// This method will return the capabilities string in the provided buffer, if the buffer is large enough.
	/// Upon success, the returned capabilities will be null-terminated.
	/// In case there is a transmission error, the method <b>will</b> throw an exception derived from <see cref="MonitorControlCommunicationException"/>.
	/// </remarks>
	/// <param name="buffer">A buffer that will hold the contents of the string.</param>
	/// <returns><see langword="true"/> if the capabilities string could be retrieved; <see langword="false"/> if the provided buffer was too small.</returns>
	/// <exception cref="I2cNotSupportedException"></exception>
	/// <exception cref="I2cDeviceNotFoundException"></exception>
	/// <exception cref="I2cTransmissionException"></exception>
	/// <exception cref="I2cReceptionException"></exception>
	/// <exception cref="InvalidDdcCiMessageCommandException">Can occur if there are conflicting requests on the monitor's DDC/CI channel, or in case of a problem with the monitor.</exception>
	/// <exception cref="InvalidDdcCiMessageLengthException">Can occur if there are conflicting requests on the monitor's DDC/CI channel, or in case of a problem with the monitor.</exception>
	/// <exception cref="InvalidDdcCiMessageChecksumException">Can occur if there are conflicting requests on the monitor's DDC/CI channel, or in case of a problem with the monitor.</exception>
	/// <exception cref="MonitorNoLongerExistsException">This physical monitor is not valid anymore.</exception>
	/// <exception cref="Win32Exception"></exception>
	public unsafe bool TryGetCapabilitiesUtf8String(Memory<byte> buffer)
	{
		using (var handle = buffer.Pin())
		{
			if (NativeMethods.CapabilitiesRequestAndCapabilitiesReply(Handle, (byte*)handle.Pointer, (uint)buffer.Length) != 0) return true;

			var errorCode = Marshal.GetLastWin32Error();
			if (errorCode == NativeMethods.ErrorInsufficientBuffer) return false;

			throw GetExceptionForError(errorCode);
		}
	}

	/// <summary>Tries to get the raw Capabilities string for the monitor using a stack-based buffer.</summary>
	/// <remarks>
	/// This method will return the capabilities string in the provided buffer, if the buffer is large enough.
	/// Upon success, the returned capabilities will be null-terminated.
	/// In case there is a transmission error, the method <b>will</b> throw an exception derived from <see cref="MonitorControlCommunicationException"/>.
	/// </remarks>
	/// <param name="buffer">A buffer that will hold the contents of the string.</param>
	/// <returns>The capabilities string, or <see langword="null"/> if the capabilities are longer than <c>4096</c> bytes.</returns>
	/// <exception cref="I2cNotSupportedException"></exception>
	/// <exception cref="I2cDeviceNotFoundException"></exception>
	/// <exception cref="I2cTransmissionException"></exception>
	/// <exception cref="I2cReceptionException"></exception>
	/// <exception cref="InvalidDdcCiMessageCommandException">Can occur if there are conflicting requests on the monitor's DDC/CI channel, or in case of a problem with the monitor.</exception>
	/// <exception cref="InvalidDdcCiMessageLengthException">Can occur if there are conflicting requests on the monitor's DDC/CI channel, or in case of a problem with the monitor.</exception>
	/// <exception cref="InvalidDdcCiMessageChecksumException">Can occur if there are conflicting requests on the monitor's DDC/CI channel, or in case of a problem with the monitor.</exception>
	/// <exception cref="MonitorNoLongerExistsException">This physical monitor is not valid anymore.</exception>
	/// <exception cref="Win32Exception"></exception>
	public unsafe byte[]? TryGetCapabilitiesUtf8String()
	{
		const int Length = 4096;

		byte* buffer = stackalloc byte[Length];
		if (NativeMethods.CapabilitiesRequestAndCapabilitiesReply(Handle, buffer, Length) != 0) return GetNullTerminatedBytes(new(buffer, Length));

		var errorCode = Marshal.GetLastWin32Error();
		if (errorCode == NativeMethods.ErrorInsufficientBuffer) return null;

		GetExceptionForError(errorCode);
		return null;
	}


	/// <summary>Gets the raw Capabilities string for the monitor using retry logic.</summary>
	/// <remarks>
	/// Because communication on the monitor I2C bus, it can be necessary to retry fetching capabilities multiple times.
	/// The retry delay should be large enough to allow further retries to succeed.
	/// Because of this, it is expected that this method can block for a relatively long time.
	/// </remarks>
	/// <returns>A UTF-8 string describing the monitor capabilities according to MCCI specifications.</returns>
	/// <param name="retryCount">The number of times the operations can be retried. Can be <c>0</c>.</param>
	/// <param name="initialRetryDelay">The initial delay between retries. Must be at least 10ms.</param>
	/// <param name="maxRetryDelay">The maximum delay between two retries. Must be at least <paramref name="initialRetryDelay"/>.</param>
	/// <exception cref="Win32Exception"></exception>
	public unsafe ReadOnlyMemory<byte> GetCapabilitiesUtf8String(int retryCount = 4, int initialRetryDelay = 200, int maxRetryDelay = 2000)
	{
#if NET8_0_OR_GREATER
		ArgumentOutOfRangeException.ThrowIfNegative(retryCount);
		ArgumentOutOfRangeException.ThrowIfNegative(initialRetryDelay);
		ArgumentOutOfRangeException.ThrowIfNegative(maxRetryDelay);
		ArgumentOutOfRangeException.ThrowIfLessThan(initialRetryDelay, 10);
		ArgumentOutOfRangeException.ThrowIfLessThan(maxRetryDelay, initialRetryDelay);
#endif
		// NB: We need to have a retry logic in there because monitors can occasionally fail to correctly answer the DDC/CI commands.
		// This is likely due to conflicts with other devices on the bus such as shitty HDCP stuff.
		int currentRetryCount = retryCount;
		int retryDelay = initialRetryDelay;
		uint capabilitiesStringLength;
		while (true)
		{
			if (NativeMethods.GetCapabilitiesStringLength(Handle, out capabilitiesStringLength) != 0) break;

			var errorCode = Marshal.GetLastWin32Error();
			if (errorCode is NativeMethods.ErrorGraphicsDdcCiInvalidMessageCommand or NativeMethods.ErrorGraphicsDdcCiInvalidMessageLength or NativeMethods.ErrorGraphicsDdcCiInvalidMessageChecksum && currentRetryCount > 0)
			{
				currentRetryCount--;
			}
			else
			{
				throw GetExceptionForError(errorCode);
			}

			// Thread.Sleep is not ideal as we are might be running on the thread pool. Let's do something better if necessary.
			Thread.Sleep(retryDelay);
			retryDelay = (int)Math.Min((uint)retryDelay * 2, (uint)maxRetryDelay);
		}

		if (capabilitiesStringLength == 0)
		{
			return default;
		}

		var buffer = new byte[capabilitiesStringLength];
		fixed (byte* bufferStart = buffer)
		{
			currentRetryCount = retryCount;
			retryDelay = initialRetryDelay;
			while (true)
			{
				if (NativeMethods.CapabilitiesRequestAndCapabilitiesReply(Handle, bufferStart, capabilitiesStringLength) != 0) break;

				var errorCode = Marshal.GetLastWin32Error();
				if (errorCode is NativeMethods.ErrorGraphicsDdcCiInvalidMessageCommand or NativeMethods.ErrorGraphicsDdcCiInvalidMessageLength or NativeMethods.ErrorGraphicsDdcCiInvalidMessageChecksum && currentRetryCount > 0)
				{
					currentRetryCount--;
				}
				else
				{
					throw GetExceptionForError(errorCode);
				}

				// Thread.Sleep is not ideal as we are might be running on the thread pool. Let's do something better if necessary.
				Thread.Sleep(retryDelay);
				retryDelay = (int)Math.Min((uint)retryDelay * 2, (uint)maxRetryDelay);
			}
		}

		// Some capabilities string seem to be returned with trailing null characters, so we trim them to be sure 😑
		return buffer.AsMemory(0, buffer.Length - 1).TrimEnd((byte)0);
	}

	public void SetVcpFeature(byte vcpCode, uint value)
	{
		if (NativeMethods.SetVcpFeature(Handle, vcpCode, value) == 0)
		{
			throw GetExceptionForError(Marshal.GetLastWin32Error());
		}
	}

	public VcpFeatureReply GetVcpFeature(byte vcpCode)
	{
		if (NativeMethods.GetVcpFeatureAndVcpFeatureReply(Handle, vcpCode, out var type, out uint currentValue, out uint maximumValue) == 0)
		{
			throw GetExceptionForError(Marshal.GetLastWin32Error());
		}

		return new VcpFeatureReply((ushort)currentValue, (ushort)maximumValue, type == NativeMethods.VcpCodeType.Momentary);
	}

	public void SaveCurrentSettings()
	{
		if (NativeMethods.SaveCurrentSettings(Handle) == 0)
		{
			throw GetExceptionForError(Marshal.GetLastWin32Error());
		}
	}
}
