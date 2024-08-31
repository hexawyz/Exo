using System;

namespace DeviceTools.FilterExpressions;

public sealed class UInt64Property : Property<ulong?>, IComparableProperty<ulong>
{
	internal UInt64Property(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.UInt64;

	public static DeviceFilterExpression operator ==(UInt64Property left, ulong right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.Equals);
	public static DeviceFilterExpression operator !=(UInt64Property left, ulong right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.NotEquals);
	public static DeviceFilterExpression operator <(UInt64Property left, ulong right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.LessThan);
	public static DeviceFilterExpression operator >(UInt64Property left, ulong right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.GreaterThan);
	public static DeviceFilterExpression operator <=(UInt64Property left, ulong right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.LessThanOrEquals);
	public static DeviceFilterExpression operator >=(UInt64Property left, ulong right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.GreaterThanOrEquals);
}
