using System;

namespace Exo.Services
{
	public interface IDeviceNotificationSink
	{
		void OnDeviceArrival(Guid deviceInterfaceClassGuid, string deviceName) { }
		bool OnDeviceQueryRemove(Guid deviceInterfaceClassGuid, string deviceName) => true;
		void OnDeviceQueryRemoveFailed(Guid deviceInterfaceClassGuid, string deviceName) { }
		void OnDeviceRemovePending(Guid deviceInterfaceClassGuid, string deviceName) { }
		void OnDeviceRemoveComplete(Guid deviceInterfaceClassGuid, string deviceName) { }
	}
}
