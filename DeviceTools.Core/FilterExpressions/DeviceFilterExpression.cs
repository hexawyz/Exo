using System;

namespace DeviceTools.FilterExpressions
{
	public abstract class DeviceFilterExpression
	{
		private protected DeviceFilterExpression() { }

		public static DeviceFilterExpression operator &(DeviceFilterExpression left, DeviceFilterExpression right) => DeviceFilterLogicalExpression.Create(LogicalOperator.And, left, right);
		public static DeviceFilterExpression operator |(DeviceFilterExpression left, DeviceFilterExpression right) => DeviceFilterLogicalExpression.Create(LogicalOperator.Or, left, right);
		public static DeviceFilterExpression operator !(DeviceFilterExpression expression) => DeviceFilterNotExpression.Create(expression);

		internal virtual int GetFilterElementCount(bool isRoot) => 1;

		internal abstract void FillExpressions(Span<NativeMethods.DevicePropertyFilterExpression> expressions, bool isRoot, out int count);

		internal abstract void ReleaseExpressionResources();
	}
}
