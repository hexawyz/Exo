using System;

namespace DeviceTools.FilterExpressions
{
	public sealed class GuidProperty : Property, IProperty<Guid>
	{
		internal GuidProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

		public override DevicePropertyType Type => DevicePropertyType.Guid;

		public static DeviceFilterExpression operator ==(GuidProperty left, Guid right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.Equals);
		public static DeviceFilterExpression operator !=(GuidProperty left, Guid right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.NotEquals);
	}
}
