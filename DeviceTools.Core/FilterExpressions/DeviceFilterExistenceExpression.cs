using System;

namespace DeviceTools.FilterExpressions;

public sealed class DeviceFilterExistenceExpression : DeviceFilterExpression
{
	public Property Property { get; }
	public bool Exists { get; }

	internal static DeviceFilterExpression Create(DeviceFilterExpression operand) =>
		operand switch
		{
			DeviceFilterNotExpression notExpression => notExpression.Operand,
			DeviceFilterPropertyComparisonExpression propertyComparisonExpression => propertyComparisonExpression.Not(),
			_ => new DeviceFilterNotExpression(operand)
		};

	internal DeviceFilterExistenceExpression(Property property, bool exists)
	{
		Property = property;
		Exists = exists;
	}

	internal override int GetFilterElementCount(bool isRoot) => 1;

	internal override void FillExpressions(Span<NativeMethods.DevicePropertyFilterExpression> expressions, bool isRoot, out int count)
	{
		expressions[0] = new NativeMethods.DevicePropertyFilterExpression
		{
			Operator = Exists ? NativeMethods.DevPropertyOperator.Exists : NativeMethods.DevPropertyOperator.NotExists,
			Property =
			{
				CompoundKey =
				{
					Key = Property.Key,
					Store = NativeMethods.DevicePropertyStore.Sytem,
				},
				Type = NativeMethods.DevicePropertyType.Empty,
				BufferLength = 0,
				Buffer = (IntPtr)0,
			}
		};
		count = 1;
	}

	internal override void ReleaseExpressionResources() { }

	internal DeviceFilterExistenceExpression Not() => new DeviceFilterExistenceExpression(Property, !Exists);
}
