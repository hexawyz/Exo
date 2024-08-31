using System;

namespace DeviceTools.FilterExpressions
{
	public sealed class SecurityDescriptorProperty : Property
	{
		internal SecurityDescriptorProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

		public override DevicePropertyType Type => DevicePropertyType.SecurityDescriptor;
	}
}
