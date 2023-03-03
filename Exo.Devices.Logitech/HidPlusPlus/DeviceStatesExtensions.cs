namespace Exo.Devices.Logitech.HidPlusPlus;

internal static class DeviceStatesExtensions
{
	public static ref T? GetReference<T>(this ref DeviceStates<T> value, byte deviceIndex)
		where T : class
		=> ref DeviceStates<T>.GetReference(ref value, deviceIndex);

	// Gets a reference to the specified state, or a null reference if the state is part of the unallocated array.
	public static ref T? TryGetReference<T>(this ref DeviceStates<T> value, byte deviceIndex)
		where T : class
		=> ref DeviceStates<T>.GetReference(ref value, deviceIndex);
}
