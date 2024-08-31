using System;
using Microsoft.Win32.SafeHandles;

namespace Exo.Services
{
	public interface IDeviceHandleNotificationSink<T>
	{
		void OnDeviceArrival(SafeFileHandle deviceFileHandle, T state, IDisposable registration) { }
		bool OnDeviceQueryRemove(SafeFileHandle deviceFileHandle, T state, IDisposable registration) => true;
		void OnDeviceQueryRemoveFailed(SafeFileHandle deviceFileHandle, T state, IDisposable registration) { }
		void OnDeviceRemovePending(SafeFileHandle deviceFileHandle, T state, IDisposable registration) { }
		void OnDeviceRemoveComplete(SafeFileHandle deviceFileHandle, T state, IDisposable registration) { }
	}
}
