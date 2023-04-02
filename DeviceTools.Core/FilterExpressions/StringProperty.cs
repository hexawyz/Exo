using System;

namespace DeviceTools.FilterExpressions;

public sealed class StringProperty : Property<string?>, IComparableProperty<string?>
{
	internal StringProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.String;

	public static DeviceFilterExpression operator ==(StringProperty left, string? right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.Equals);
	public static DeviceFilterExpression operator !=(StringProperty left, string? right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.NotEquals);

	public DeviceFilterExpression EqualsIgnoreCase(string? right) => DeviceFilterPropertyComparisonExpression.Create(this, right, StringOperator.EqualsIgnoreCase);
}
