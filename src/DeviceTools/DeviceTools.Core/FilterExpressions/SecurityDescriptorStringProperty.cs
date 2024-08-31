using System;

namespace DeviceTools.FilterExpressions
{
	public sealed class SecurityDescriptorStringProperty : Property
	{
		internal SecurityDescriptorStringProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

		public override DevicePropertyType Type => DevicePropertyType.SecurityDescriptorString;
	}
}
