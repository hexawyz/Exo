namespace DeviceTools.FilterExpressions;

#pragma warning disable CS0660, CS0661
public sealed class ByteProperty : Property<byte?>, IComparableProperty<byte>
#pragma warning restore CS0661, CS0660
{
	internal ByteProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.Byte;

	public static DeviceFilterExpression operator ==(ByteProperty left, byte right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.Equals);
	public static DeviceFilterExpression operator !=(ByteProperty left, byte right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.NotEquals);
}
