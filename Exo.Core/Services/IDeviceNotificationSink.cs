using System;

namespace Exo.Core.Services
{
	public interface IDeviceNotificationSink
	{
		void OnDeviceArrival(Guid deviceInterfaceClassGuid, string deviceName) { }
		void OnDeviceQueryRemoveFailed(Guid deviceInterfaceClassGuid, string deviceName) { }
		void OnDeviceQueryRemoveFailed(Guid deviceInterfaceClassGuid, string deviceName) { }

		bool OnDeviceQueryRemove(Guid deviceInterfaceClassGuid, string deviceName) => true;
	}
}
