namespace Exo.SystemManagementBus;

/// <summary>Define a standard SMBus interface.</summary>
/// <remarks>
/// <para>This interface allows accessing SMBus features by exposing the standard SMBus methods.</para>
/// <para>
/// While there is generally one SMBus on each computer there can be more than one bus.
/// Because SMBus is very low level, access to this feature is quite opaque.
/// This means that OS-level support is often poor or inexistant. This also means that discovery of SMBuses and connected devices is not guaranteed.
/// </para>
/// <para>
/// Knowledge of supported SMBuses and connected devices is often system-specific, but SMBus is used to communicate with some RGB DRAM sticks.
/// In the case of DRAM sticks, the SMBus involved is likely to be the main one, provided by the CPU/Chipset. Other cases are hardware-dependent.
/// </para>
/// <para>
/// This service can be implemented in various way that would be mostly hardware-specific.
/// Such ways could be through WMI (ACPI) calls in the case of Gigabyte motherboards, proprietary drivers (such as Gigabyte's YCC driver that is bundled with RGB Fusion), or other implementations.
/// </para>
/// <para>
/// We should ideally not need to rely on a low-level SMBus interface, but it seems difficult to envision anything without this at the moment.
/// </para>
/// </remarks>
public interface ISystemManagementBus
{
	/// <summary>Acquires the SMBus mutex.</summary>
	/// <remarks>
	/// <para>
	/// It is necessary to acquire the SMBus Mutex in order to prevent conflicting usages on the SMBus, especially by different applications.
	/// Some SMBus controllers, such as the Intel one, will provide a hardware mutex in order to prevent conflicting uses, others may not.
	/// At the very least, this method will enforce the use of a process-local mutex, avoiding multiple subsystems conflicting with each other.
	/// </para>
	/// <para>
	/// Like all locks, the SMBus lock should be held for the smallest amount of time possible.
	/// The mutex will install a task scheduler.
	/// </para>
	/// <para>
	/// On Windows, and for the main SMBus, this method should, first and foremost, always acquire the system-wide <c>Global\Access_SMBUS.HTP.Method</c> Mutex.
	/// This Mutex seems to be a common convention used by all software to avoid conflicting accesses from user code.
	/// By acquiring and properly releasing that mutex, SMBus accesses from user code should be relatively safe.
	/// </para>
	/// <para>
	/// Failing to release the Mutex may block proper execution of system software on the computer, and will definitely block other components relying on <see cref="ISystemManagementBus"/> from working altogether.
	/// </para>
	/// </remarks>
	/// <returns>An object that must be disposed to release the Mutex.</returns>
	ValueTask<OwnedMutex> AcquireMutexAsync();

	ValueTask QuickWriteAsync(byte address);
	ValueTask SendByteAsync(byte address, byte value);

	ValueTask WriteByteAsync(byte address, byte command, byte value);
	ValueTask WriteWordAsync(byte address, byte command, ushort value);

	/// <summary>Writes the specified block of memory.</summary>
	/// <remarks>The data that is passed as parameter will be copied into an internal buffer as needed. (i.e. if the call is actually async or if it's required by the implementation)</remarks>
	/// <param name="address">The SMBus device address.</param>
	/// <param name="command">The command code.</param>
	/// <param name="data">The data to write.</param>
	ValueTask WriteBlockAsync(byte address, byte command, Span<byte> data);

	ValueTask QuickReadAsync(byte address);
	ValueTask<byte> ReceiveByteAsync(byte address);

	ValueTask<byte> ReadByteAsync(byte address, byte command);
	ValueTask<ushort> ReadWordAsync(byte address, byte command);
	ValueTask<byte[]> ReadBlockAsync(byte address, byte command);
}

public interface ISystemManagementBusProvider
{
	ValueTask<ISystemManagementBus> GetSystemBusAsync(CancellationToken cancellationToken);
}

public interface ISystemManagementBusRegistry
{
	IDisposable RegisterSystemBus(ISystemManagementBus bus);
}
