using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exo.Core.Services;

namespace Exo.Service
{
	internal sealed class DeviceManager
	{
		private readonly IDeviceNotificationService _deviceNotificationService;

		public DeviceManager(IDeviceNotificationService deviceNotificationService) => _deviceNotificationService = deviceNotificationService;


	}
}
