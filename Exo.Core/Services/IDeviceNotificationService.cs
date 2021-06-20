using System;
using Microsoft.Win32.SafeHandles;

namespace Exo.Core.Services
{
	public interface IDeviceNotificationService
	{
		IDisposable Register(SafeFileHandle deviceHandle, IDeviceHandleNotificationSink sink);

		IDisposable Register(string deviceName, IDeviceNotificationSink sink);

		IDisposable Register(Guid deviceInterfaceClassGuid, IDeviceNotificationSink sink);
	}
}
