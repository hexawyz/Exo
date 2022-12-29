using System;

namespace DeviceTools.FilterExpressions
{
	public sealed class BinaryProperty : Property, IProperty<byte[]>
	{
		internal BinaryProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

		public override DevicePropertyType Type => DevicePropertyType.Binary;
	}
}
