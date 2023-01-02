using System;

namespace DeviceTools.FilterExpressions
{
	// There is nothing mapping to the equivalent of VT_UNKNOWN, so I have no idea of those properties work with DevQuery.
	// Having this type at least allows the properties to be declared.
	public sealed class ObjectProperty : Property
	{
		internal ObjectProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

		public override DevicePropertyType Type => DevicePropertyType.Null;
	}
}
