namespace DeviceTools.FilterExpressions;

#pragma warning disable CS0660, CS0661
public sealed class BinaryProperty : Property<byte[]?>, IComparableProperty<byte[]>
#pragma warning restore CS0661, CS0660
{
	internal BinaryProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.Binary;
}
