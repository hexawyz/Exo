namespace DeviceTools.FilterExpressions;

#pragma warning disable CS0660, CS0661
public sealed class UInt32Property : Property<uint?>, IComparableProperty<uint>
#pragma warning restore CS0661, CS0660
{
	internal UInt32Property(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.UInt32;

	public static DeviceFilterExpression operator ==(UInt32Property left, uint right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.Equals);
	public static DeviceFilterExpression operator !=(UInt32Property left, uint right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.NotEquals);
	public static DeviceFilterExpression operator <(UInt32Property left, uint right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.LessThan);
	public static DeviceFilterExpression operator >(UInt32Property left, uint right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.GreaterThan);
	public static DeviceFilterExpression operator <=(UInt32Property left, uint right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.LessThanOrEquals);
	public static DeviceFilterExpression operator >=(UInt32Property left, uint right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.GreaterThanOrEquals);
}
