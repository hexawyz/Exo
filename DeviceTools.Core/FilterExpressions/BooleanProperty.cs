using System;

namespace DeviceTools.FilterExpressions
{
	public sealed class BooleanProperty : Property, IProperty<bool>
	{
		internal BooleanProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

		public override DevicePropertyType Type => DevicePropertyType.Boolean;

		public static DeviceFilterExpression operator ==(BooleanProperty left, bool right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.Equals);
		public static DeviceFilterExpression operator !=(BooleanProperty left, bool right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.NotEquals);
	}
}
