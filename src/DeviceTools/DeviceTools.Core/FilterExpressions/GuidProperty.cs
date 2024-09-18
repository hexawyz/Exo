namespace DeviceTools.FilterExpressions;

#pragma warning disable CS0660, CS0661
public sealed class GuidProperty : Property<Guid?>, IComparableProperty<Guid>
#pragma warning restore CS0661, CS0660
{
	internal GuidProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.Guid;

	public static DeviceFilterExpression operator ==(GuidProperty left, Guid right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.Equals);
	public static DeviceFilterExpression operator !=(GuidProperty left, Guid right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.NotEquals);
}
