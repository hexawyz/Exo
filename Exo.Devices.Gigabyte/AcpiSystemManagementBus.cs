using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Firmware.Uefi;
using Exo.Features;
using Exo.SystemManagementBus;
using Microsoft.Management.Infrastructure;

namespace Exo.Devices.Gigabyte;

/// <summary>Implements a firmware-specific driver for (some) Gigabyte motherboards.</summary>
/// <remarks>
/// Availability of this feature is not guaranteed but it is a relatively safe way to access SMBus, as it doesn't require fiddling with raw IO ports.
/// Despite this, it requires administrator rights, and it could be a bit slower than more direct kernel-driver based approaches.
/// </remarks>
internal sealed class AcpiSystemManagementBus : ISystemManagementBus, IMotherboardSystemManagementBusFeature
{
	// From what I found, gigabyte's utilities acquire a system-wide mutex using SetFirmwareEnvironmentVariable.
	// I don't know yet if this is compatible with the ACPI WMI methods, but we may need this if we want to implement our own SMB driver at some point.
	// I actually don't know if that Mutex is the chipset mutex or something firmware-specific, but using the firmware mutex should be a safe choice anyway.
	private sealed class MutexLifecycle : IMutexLifetime
	{
		public static readonly MutexLifecycle Instance = new();

		// Not exactly sure what this GUID maps to, but it does not seem to be Gigabyte-specific, so I'm assuming it is from AMI.
		private static readonly Guid AmiGuid = new Guid(0x01368881, 0xC4AD, 0x4B1D, 0xB6, 0x31, 0xD5, 0x7A, 0x8E, 0xC8, 0xDB, 0x6B);

		private static unsafe void SetSystemMutex(bool enable)
		{
			byte value = enable ? (byte)1 : (byte)0;
			EfiEnvironment.SetVariable("SMBMutex", AmiGuid, MemoryMarshal.CreateReadOnlySpan(ref value, 1));
		}

		public void OnAfterAcquire()
		{
			//SetSystemMutex(true);
		}

		public void OnBeforeRelease()
		{
			//SetSystemMutex(false);
		}
	}

	private const string CimNamespace = @"ROOT/wmi";
	private const string AcpiMethodClassName = "GSA1_ACPIMethod";

	private const string SmbQuickReadMethodName = "SMBQuickRead";
	private const string SmbReceiveByteMethodName = "SMBReceiveByte";
	private const string SmbReadByteMethodName = "SMBReadByte";
	private const string SmbReadWordMethodName = "SMBReadWord";
	private const string SmbBlockReadMethodName = "SMBBlockRead";

	private const string SmbQuickWriteMethodName = "SMBQuickWrite";
	private const string SmbSendByteMethodName = "SMBSendByte";
	private const string SmbWriteByteMethodName = "SMBWriteByte";
	private const string SmbWriteWordMethodName = "SMBWriteWord";
	private const string SmbBlockWriteMethodName = "SMBBlockWrite";

	private readonly CimSession _cimSession;
	private readonly CimInstance _cimInstance;
	private readonly byte[] _writeBuffer;
	private readonly byte _busIndex;

	public static async Task<AcpiSystemManagementBus> CreateAsync()
	{
		// NB: We need to use a local connection. Despite what you would think, connecting to "localhost" is NOT a local connection.
		var cimSession = await CimSession.CreateAsync(null);
		// NB: This is weird and really annoying, but the CimSession.GetInstance(Async) method DOES NOT work.
		// The PowerShell cmdlet for Get-CimInstance is using EnumerateInstances(Async) instead, and it does indeed work perfectly fine in this case.
		// If the instance is retrieved via GetInstance(Async), it will look superficially fine, but it will be profoundly incorrect for some reason.
		// The most visible symptom being that Instance Properties of the CIM instance returned by GetInstance(Async) are not properly filled. (Values are missing)
		// But these instances will not be able to execute any methods, and throw an exception for incorrect parameter.
		// AFAIK, there's close to no documentation on how to work on this API, and I've had to work out the quirks on my own,
		// as many of the few developers that confronted themselves to this on internet had similar problems in various parts of the API, but none had a use case as specific as this one.
		// What saved me is knowing that the PowerShell cmdlets work fine, as I've used them for prototyping this in the past, and thankfully, we have the code for these commands available on GitHub.
		var acpiMethodInstance = await cimSession.EnumerateInstancesAsync(CimNamespace, AcpiMethodClassName).SingleAsync();

		// In the motherboard I own, the only valid value to pass as bus index is 2.
		// This can be verified by looking at the ACPI data. (Methods are exposed in SSDT1 here)
		return new(cimSession, acpiMethodInstance, 2);
	}

	private AcpiSystemManagementBus(CimSession cimSession, CimInstance cimInstance, byte busIndex)
	{
		_cimSession = cimSession;
		_cimInstance = cimInstance;
		_busIndex = busIndex;
		_writeBuffer = new byte[260];
	}

	private static uint ParseResult(CimMethodResult? cimResult)
	{
		if (cimResult is null ||
			!(bool)cimResult.ReturnValue.Value ||
			cimResult.OutParameters["ret"].Value is not CimInstance returnValueInstance ||
			returnValueInstance.CimInstanceProperties["data"].Value is not uint result)
		{
			throw new SystemManagementBusException();
		}

		return result;
	}

	private static void ValidateResult(CimMethodResult? cimResult, byte address)
	{
		uint result = ParseResult(cimResult);
		if (result != 0)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
	}

	private static byte ParseByteResult(CimMethodResult? cimResult, byte address)
	{
		uint result = ParseResult(cimResult);
		if (result > 255)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
		return (byte)result;
	}

	private static ushort ParseUInt16Result(CimMethodResult? cimResult, byte address)
	{
		uint result = ParseResult(cimResult);
		if (result > 65535)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
		return (ushort)result;
	}

	public ValueTask<OwnedMutex> AcquireMutexAsync()
	{
		return new ValueTask<OwnedMutex>(AsyncGlobalMutex.SmBus.AcquireAsync(MutexLifecycle.Instance, false));
	}

	public async ValueTask QuickReadAsync(byte address)
		=> ValidateResult
		(
			await _cimSession.InvokeMethodAsync
			(
			_cimInstance.CimSystemProperties.Namespace,
				_cimInstance,
				SmbQuickReadMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
				}
			),
			address
		);

	public async ValueTask<byte> ReceiveByteAsync(byte address)
		=> ParseByteResult
		(
			await _cimSession.InvokeMethodAsync
			(
				_cimInstance.CimSystemProperties.Namespace,
				_cimInstance,
				SmbReceiveByteMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
				}
			),
			address
		);

	public async ValueTask<byte> ReadByteAsync(byte address, byte command)
		=> ParseByteResult
		(
			await _cimSession.InvokeMethodAsync
			(
				_cimInstance.CimSystemProperties.Namespace,
				_cimInstance,
				SmbReadByteMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("cmd", command, CimType.UInt8, CimFlags.In),
				}
			),
			address
		);

	public async ValueTask<ushort> ReadWordAsync(byte address, byte command)
		=> ParseUInt16Result
		(
			await _cimSession.InvokeMethodAsync
			(
				_cimInstance,
				SmbReadWordMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("cmd", command, CimType.UInt8, CimFlags.In),
				}
			),
			address
		);

	public async ValueTask<byte[]> ReadBlockAsync(byte address, byte command)
	{
		var result = await _cimSession.InvokeMethodAsync
		(
			_cimInstance.CimSystemProperties.Namespace,
			_cimInstance,
			SmbBlockReadMethodName,
			new CimMethodParametersCollection
			{
				CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
				CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
				CimMethodParameter.Create("cmd", command, CimType.UInt8, CimFlags.In),
			}
		);

		// TODO
		// NB: I'm not entirely sure about this part, as I've not needed to use the method, but judging by the signature, this should be right.
		var buffer = (byte[])result.OutParameters["ret"].Value;

		int length = Unsafe.ReadUnaligned<int>(ref buffer[0]);

		if ((uint)length > 256) throw new InvalidOperationException();

		return buffer.AsSpan(4, length).ToArray();
	}

	public async ValueTask QuickWriteAsync(byte address)
		=> ValidateResult
		(
			await _cimSession.InvokeMethodAsync
			(
				_cimInstance.CimSystemProperties.Namespace,
				_cimInstance,
				SmbQuickWriteMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
				}
			),
			address
		);

	public async ValueTask SendByteAsync(byte address, byte value)
		=> ValidateResult
		(
			await _cimSession.InvokeMethodAsync
			(
				_cimInstance.CimSystemProperties.Namespace,
				_cimInstance,
				SmbSendByteMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("data", value, CimType.UInt8, CimFlags.In),
				}
			),
			address
		);

	public async ValueTask WriteByteAsync(byte address, byte command, byte value)
		=> ValidateResult
		(
			await _cimSession.InvokeMethodAsync
			(
				_cimInstance.CimSystemProperties.Namespace,
				_cimInstance,
				SmbWriteByteMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("cmd", command, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("data", value, CimType.UInt8, CimFlags.In),
				}
			),
			address
		);

	public async ValueTask WriteWordAsync(byte address, byte command, ushort value)
		=> ValidateResult
		(
			await _cimSession.InvokeMethodAsync
			(
				_cimInstance.CimSystemProperties.Namespace,
				_cimInstance,
				SmbWriteWordMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("cmd", command, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("data", value, CimType.UInt16, CimFlags.In),
				}
			),
			address
		);

	public async ValueTask WriteBlockAsync(byte address, byte command, ReadOnlyMemory<byte> value)
	{
		if (value.Length is 0 or > 256) throw new ArgumentException();

		var buffer = _writeBuffer;

		buffer[0] = (byte)(value.Length & 0xFF);
		buffer[1] = (byte)((value.Length >> 8) & 0xFF);
		buffer[2] = 0;
		buffer[3] = 0;
		value.Span.CopyTo(buffer.AsSpan(4));
		buffer.AsSpan(4 + value.Length).Clear();

		ValidateResult
		(
			await _cimSession.InvokeMethodAsync
			(
				_cimInstance,
				SmbBlockWriteMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("cmd", command, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("data", buffer, CimType.UInt8Array, CimFlags.In),
				}
			),
			address
		);
	}
}
