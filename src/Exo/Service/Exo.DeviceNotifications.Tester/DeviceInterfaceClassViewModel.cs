using System;
using System.Collections.Generic;
using System.Reflection;

namespace Exo.DeviceNotifications.Tester
{
	internal sealed class DeviceInterfaceClassViewModel
	{
		private static readonly Dictionary<Guid, DeviceInterfaceClassViewModel> KnownGuids = GetKnownGuids();

		private static Dictionary<Guid, DeviceInterfaceClassViewModel> GetKnownGuids()
		{
			var dictionary = new Dictionary<Guid, DeviceInterfaceClassViewModel>();

			foreach (var field in typeof(DeviceTools.DeviceInterfaceClassGuids).GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				var guid = (Guid)field.GetValue(null)!;
				dictionary.Add(guid, new DeviceInterfaceClassViewModel(guid, field.Name));
			}

			return dictionary;
		}

		public static DeviceInterfaceClassViewModel Get(Guid deviceInterfaceClassGuid)
			=> KnownGuids.TryGetValue(deviceInterfaceClassGuid, out var result) ?
				result :
				new DeviceInterfaceClassViewModel(deviceInterfaceClassGuid, deviceInterfaceClassGuid.ToString("B"));

		private DeviceInterfaceClassViewModel(Guid guid, string name)
		{
			Guid = guid;
			Name = name;
		}

		public Guid Guid { get; }
		public string Name { get; }
	}
}
