using System;

namespace DeviceTools.FilterExpressions
{
	public sealed class ByteProperty : Property, IProperty<byte>
	{
		internal ByteProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

		public override DevicePropertyType Type => DevicePropertyType.Byte;

		public static DeviceFilterExpression operator ==(ByteProperty left, byte right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.Equals);
		public static DeviceFilterExpression operator !=(ByteProperty left, byte right) => DeviceFilterPropertyComparisonExpression.Create(left, right, ComparisonOperator.NotEquals);
	}
}
