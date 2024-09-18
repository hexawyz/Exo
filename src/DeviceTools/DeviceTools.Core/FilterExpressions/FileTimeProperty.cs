namespace DeviceTools.FilterExpressions;

#pragma warning disable CS0660, CS0661
public sealed class FileTimeProperty : Property<DateTime?>, IComparableProperty<long>
#pragma warning restore CS0661, CS0660
{
	internal FileTimeProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public override DevicePropertyType Type => DevicePropertyType.FileTime;

	public static DeviceFilterExpression operator ==(FileTimeProperty left, DateTime right) => DeviceFilterPropertyComparisonExpression.Create(left, right.ToFileTimeUtc(), ComparisonOperator.Equals);
	public static DeviceFilterExpression operator !=(FileTimeProperty left, DateTime right) => DeviceFilterPropertyComparisonExpression.Create(left, right.ToFileTimeUtc(), ComparisonOperator.NotEquals);
}
