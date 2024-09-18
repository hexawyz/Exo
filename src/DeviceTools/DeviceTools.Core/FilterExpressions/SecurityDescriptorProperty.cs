namespace DeviceTools.FilterExpressions;

#pragma warning disable CS0660, CS0661
public sealed class SecurityDescriptorProperty : Property
#pragma warning restore CS0661, CS0660
{
	internal SecurityDescriptorProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.SecurityDescriptor;
}
