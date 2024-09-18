namespace DeviceTools.FilterExpressions;

#pragma warning disable CS0660, CS0661
public sealed class BooleanProperty : Property<bool?>, IComparableProperty<bool>
#pragma warning restore CS0661, CS0660
{
	internal BooleanProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.Boolean;

	public static DeviceFilterExpression operator ==(BooleanProperty left, bool right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.Equals);
	public static DeviceFilterExpression operator !=(BooleanProperty left, bool right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.NotEquals);
}
