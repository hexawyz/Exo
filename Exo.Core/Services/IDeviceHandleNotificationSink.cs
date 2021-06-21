namespace Exo.Core.Services
{
	interface IDeviceHandleNotificationSink
	{
		bool OnDeviceQueryRemove() => true;

		bool OnDeviceRemoved();
	}
}
