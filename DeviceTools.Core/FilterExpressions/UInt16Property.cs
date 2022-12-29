using System;

namespace DeviceTools.FilterExpressions
{
	public sealed class UInt16Property : Property, IProperty<ushort>
	{
		internal UInt16Property(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

		public override DevicePropertyType Type => DevicePropertyType.UInt16;

		public static DeviceFilterExpression operator ==(UInt16Property left, ushort right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.Equals);
		public static DeviceFilterExpression operator !=(UInt16Property left, ushort right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.NotEquals);
		public static DeviceFilterExpression operator <(UInt16Property left, ushort right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.LessThan);
		public static DeviceFilterExpression operator >(UInt16Property left, ushort right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.GreaterThan);
		public static DeviceFilterExpression operator <=(UInt16Property left, ushort right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.LessThanOrEquals);
		public static DeviceFilterExpression operator >=(UInt16Property left, ushort right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.GreaterThanOrEquals);
	}
}
