using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeviceTools
{
	public sealed class DeviceObjectInformation
	{
		private static readonly ReadOnlyDictionary<PropertyKey, object?> EmptyProperties = new ReadOnlyDictionary<PropertyKey, object?>(new Dictionary<PropertyKey, object?>());

		public DeviceObjectInformation(DeviceObjectKind kind, string id, Dictionary<PropertyKey, object?>? properties)
		{
			Kind = kind;
			Id = id;
			Properties = properties is not null ?
				new ReadOnlyDictionary<PropertyKey, object?>(properties) :
				EmptyProperties;
		}

		public DeviceObjectKind Kind { get; }

		public string Id { get; }

		public ReadOnlyDictionary<PropertyKey, object?> Properties { get; }
	}
}
