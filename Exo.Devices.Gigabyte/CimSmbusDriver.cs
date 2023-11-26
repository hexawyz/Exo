using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Firmware.Uefi;
using Microsoft.Management.Infrastructure;

namespace Exo.Devices.Gigabyte;

/// <summary>Implements a firmware-specific driver for (some) Gigabyte motherboards.</summary>
/// <remarks>
/// Availability of this feature is not guaranteed but it is a relatively safe way to access SMBus, as it doesn't require fiddling with raw IO ports.
/// Despite this, it
/// </remarks>
public class AcpiSmBusDriver : ISmBusDriver
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
			SetSystemMutex(true);
		}

		public void OnBeforeRelease()
		{
			SetSystemMutex(false);
		}
	}

	private const string CimNamespace = @"root\wmi";
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

	private static async Task<AcpiSmBusDriver> CreateAsync()
	{
		var cimSession = await CimSession.CreateAsync("localhost");
		var acpiMethodDefinition = new CimInstance(AcpiMethodClassName, CimNamespace);
		var acpiMethodInstance = await cimSession.GetInstanceAsync(AcpiMethodClassName, acpiMethodDefinition);

		// In the motherboard I own, the only valid value to pass as bus index is 2.
		// This can be verified by looking at the ACPI data. (Methods are exposed in SSDT1 here)
		return new(cimSession, acpiMethodInstance, 2);
	}

	public AcpiSmBusDriver(CimSession cimSession, CimInstance cimInstance, byte busIndex)
	{
		_cimSession = cimSession;
		_cimInstance = cimInstance;
		_busIndex = busIndex;
		_writeBuffer = new byte[260];
	}

	public ValueTask<OwnedMutex> AcquireMutexAsync()
	{
		return new ValueTask<OwnedMutex>(AsyncGlobalMutex.SmBus.AcquireAsync(MutexLifecycle.Instance, false));
	}

	public ValueTask QuickReadAsync(byte address)
		=> new ValueTask
		(
			_cimSession.InvokeMethodAsync
			(
				_cimInstance,
				SmbQuickReadMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
				}
			).ToTask()
		);

	public async ValueTask<byte> ReceiveByteAsync(byte address)
	{
		var result = await _cimSession.InvokeMethodAsync
		(
			_cimInstance,
			SmbReceiveByteMethodName,
			new CimMethodParametersCollection
			{
				CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
				CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
			}
		);

		return (byte)result.OutParameters["ret"].Value;
	}

	public async ValueTask<byte> ReadByteAsync(byte address, byte command)
	{
		var result = await _cimSession.InvokeMethodAsync
		(
			_cimInstance,
			SmbReadByteMethodName,
			new CimMethodParametersCollection
			{
				CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
				CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("cmd", command, CimType.UInt8, CimFlags.In),
			}
		);

		return (byte)result.OutParameters["ret"].Value;
	}

	public async ValueTask<ushort> ReadWordAsync(byte address, byte command)
	{
		var result = await _cimSession.InvokeMethodAsync
		(
			_cimInstance,
			SmbReadWordMethodName,
			new CimMethodParametersCollection
			{
				CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
				CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
				CimMethodParameter.Create("cmd", command, CimType.UInt8, CimFlags.In),
			}
		);

		return (ushort)result.OutParameters["ret"].Value;
	}

	public async ValueTask<byte[]> ReadBlockAsync(byte address, byte command)
	{
		var result = await _cimSession.InvokeMethodAsync
		(
			_cimInstance,
			SmbBlockReadMethodName,
			new CimMethodParametersCollection
			{
				CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
				CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
				CimMethodParameter.Create("cmd", command, CimType.UInt8, CimFlags.In),
			}
		);

		// NB: I'm not entirely sure about this part, as I've not needed to use the method, but judging by the signature, this should be right.
		var buffer = (byte[])result.OutParameters["ret"].Value;

		int length = Unsafe.ReadUnaligned<int>(ref buffer[0]);

		if ((uint)length > 256) throw new InvalidOperationException();

		return buffer.AsSpan(4, length).ToArray();
	}

	public ValueTask QuickWriteAsync(byte address)
		=> new ValueTask
		(
			_cimSession.InvokeMethodAsync
			(
				_cimInstance,
				SmbQuickWriteMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
				}
			).ToTask()
		);

	public ValueTask SendByteAsync(byte address, byte value)
		=> new ValueTask
		(
			_cimSession.InvokeMethodAsync
			(
				_cimInstance,
				SmbSendByteMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("data", value, CimType.UInt8, CimFlags.In),
				}
			).ToTask()
		);

	public ValueTask WriteByteAsync(byte address, byte command, byte value)
		=> new ValueTask
		(
			_cimSession.InvokeMethodAsync
			(
				_cimInstance,
				SmbWriteByteMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("cmd", command, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("data", value, CimType.UInt8, CimFlags.In),
				}
			).ToTask()
		);

	public ValueTask WriteWordAsync(byte address, byte command, ushort value)
		=> new ValueTask
		(
			_cimSession.InvokeMethodAsync
			(
				_cimInstance,
				SmbWriteWordMethodName,
				new CimMethodParametersCollection
				{
					CimMethodParameter.Create("bus", _busIndex, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("addr", address, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("cmd", command, CimType.UInt8, CimFlags.In),
					CimMethodParameter.Create("data", value, CimType.UInt16, CimFlags.In),
				}
			).ToTask()
		);

	public ValueTask WriteBlockAsync(byte address, byte command, Span<byte> value)
	{
		if (value.Length is 0 or > 256) throw new ArgumentException();

		var buffer = _writeBuffer;

		buffer[0] = (byte)(value.Length & 0xFF);
		buffer[1] = (byte)((value.Length >> 8) & 0xFF);
		buffer[2] = 0;
		buffer[3] = 0;
		value.CopyTo(buffer.AsSpan(4));
		buffer.AsSpan(4 + value.Length).Clear();

		return new ValueTask
		(
			_cimSession.InvokeMethodAsync
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
			).ToTask()
		);
	}
}
