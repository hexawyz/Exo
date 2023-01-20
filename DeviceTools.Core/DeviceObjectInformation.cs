using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeviceTools
{
	public sealed class DeviceObjectInformation
	{
		internal static readonly DevicePropertyDictionary EmptyProperties = new DevicePropertyDictionary(new Dictionary<PropertyKey, object?>());

		public DeviceObjectInformation(DeviceObjectKind kind, string id, Dictionary<PropertyKey, object?>? properties)
		{
			Kind = kind;
			Id = id;
			Properties = properties is not null ?
				new DevicePropertyDictionary(properties) :
				EmptyProperties;
		}

		public DeviceObjectKind Kind { get; }

		public string Id { get; }

		public DevicePropertyDictionary Properties { get; }
	}
}
