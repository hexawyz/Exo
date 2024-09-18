namespace DeviceTools.FilterExpressions;

#pragma warning disable CS0660, CS0661
public sealed class StringProperty : Property<string?>, IComparableProperty<string?>
#pragma warning restore CS0661, CS0660
{
	internal StringProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.String;

	public static DeviceFilterExpression operator ==(StringProperty left, string? right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.Equals);
	public static DeviceFilterExpression operator !=(StringProperty left, string? right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.NotEquals);

	public DeviceFilterExpression EqualsIgnoreCase(string? right) => DeviceFilterPropertyComparisonExpression.Create(this, right, StringOperator.EqualsIgnoreCase);
}
