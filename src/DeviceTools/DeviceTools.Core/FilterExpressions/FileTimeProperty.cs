using System;

namespace DeviceTools.FilterExpressions;

public sealed class FileTimeProperty : Property<DateTime?>, IComparableProperty<long>
{
	internal FileTimeProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.FileTime;

	public static DeviceFilterExpression operator ==(FileTimeProperty left, DateTime right) => DeviceFilterPropertyComparisonExpression.Create(left, right.ToFileTimeUtc(), ComparisonOperator.Equals);
	public static DeviceFilterExpression operator !=(FileTimeProperty left, DateTime right) => DeviceFilterPropertyComparisonExpression.Create(left, right.ToFileTimeUtc(), ComparisonOperator.NotEquals);
}
