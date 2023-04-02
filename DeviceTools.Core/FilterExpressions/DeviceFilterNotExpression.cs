using System;

namespace DeviceTools.FilterExpressions
{
	public sealed class DeviceFilterNotExpression : DeviceFilterExpression
	{
		public DeviceFilterExpression Operand { get; }

		internal static DeviceFilterExpression Create(DeviceFilterExpression operand) =>
			operand switch
			{
				DeviceFilterNotExpression notExpression => notExpression.Operand,
				DeviceFilterPropertyComparisonExpression propertyComparisonExpression => propertyComparisonExpression.Not(),
				DeviceFilterExistenceExpression propertyExistenceExpression => propertyExistenceExpression.Not(),
				_ => new DeviceFilterNotExpression(operand)
			};

		internal DeviceFilterNotExpression(DeviceFilterExpression operand) => Operand = operand;

		internal override int GetFilterElementCount(bool isRoot) => Operand.GetFilterElementCount(false) + 2;

		internal override void FillExpressions(Span<NativeMethods.DevicePropertyFilterExpression> expressions, bool isRoot, out int count)
		{
			int n = 0;
			{
				expressions[0] = new NativeMethods.DevicePropertyFilterExpression { Operator = NativeMethods.DevPropertyOperator.NotOpen, };
				expressions = expressions.Slice(1);
				n++;
			}
			{
				Operand.FillExpressions(expressions, false, out int m);
				expressions = expressions.Slice(m);
				n += m;
			}
			{
				expressions[0] = new NativeMethods.DevicePropertyFilterExpression { Operator = NativeMethods.DevPropertyOperator.NotClose, };
				expressions = expressions.Slice(1);
				n++;
			}
			count = n;
		}

		internal override void ReleaseExpressionResources() => Operand.ReleaseExpressionResources();
	}
}
