using System;

namespace DeviceTools.FilterExpressions;

public sealed class BinaryProperty : Property<byte[]?>, IComparableProperty<byte[]>
{
	internal BinaryProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.Binary;
}
