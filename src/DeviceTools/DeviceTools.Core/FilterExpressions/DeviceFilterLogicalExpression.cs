using System;

namespace DeviceTools.FilterExpressions
{
	public abstract class DeviceFilterLogicalExpression : DeviceFilterExpression
	{
		private sealed class DeviceFilterLogical2Expression : DeviceFilterLogicalExpression
		{
			private readonly FixedLengthArray2<DeviceFilterExpression> _array;

			internal DeviceFilterLogical2Expression(LogicalOperator @operator, DeviceFilterExpression expression1, DeviceFilterExpression expression2)
				: base(@operator) =>
				_array = new FixedLengthArray2<DeviceFilterExpression>(expression1, expression2);

			public override int Count => 2;

			private protected override ReadOnlySpan<DeviceFilterExpression> GetSpan() => FixedLengthArray.AsSpan(_array);
		}

		private sealed class DeviceFilterLogical3Expression : DeviceFilterLogicalExpression
		{
			private readonly FixedLengthArray3<DeviceFilterExpression> _array;

			internal DeviceFilterLogical3Expression(LogicalOperator @operator, DeviceFilterExpression expression1, DeviceFilterExpression expression2, DeviceFilterExpression expression3)
				: base(@operator) =>
				_array = new FixedLengthArray3<DeviceFilterExpression>(expression1, expression2, expression3);

			public override int Count => 3;

			private protected override ReadOnlySpan<DeviceFilterExpression> GetSpan() => FixedLengthArray.AsSpan(_array);
		}

		private sealed class DeviceFilterLogical4Expression : DeviceFilterLogicalExpression
		{
			private readonly FixedLengthArray4<DeviceFilterExpression> _array;

			internal DeviceFilterLogical4Expression
			(
				LogicalOperator @operator,
				DeviceFilterExpression expression1,
				DeviceFilterExpression expression2,
				DeviceFilterExpression expression3,
				DeviceFilterExpression expression4
			) : base(@operator) =>
				_array = new FixedLengthArray4<DeviceFilterExpression>(expression1, expression2, expression3, expression4);

			public override int Count => 4;

			private protected override ReadOnlySpan<DeviceFilterExpression> GetSpan() => FixedLengthArray.AsSpan(_array);
		}

		private sealed class DeviceFilterLogicalNExpression : DeviceFilterLogicalExpression
		{
			// We could make this mutable to allow updating this with a larger array when merging/enlarging, but it would add moving parts, so let's not do it for now.
			private readonly DeviceFilterExpression[] _array;

			internal DeviceFilterLogicalNExpression(LogicalOperator @operator, DeviceFilterExpression[] array)
				: base(@operator) =>
				_array = array;

			public override int Count => _array.Length;

			private protected override ReadOnlySpan<DeviceFilterExpression> GetSpan() => _array.AsSpan();
		}

		public LogicalOperator LogicalOperator { get; }

		public DeviceFilterExpression this[int index] => GetSpan()[index];

		public abstract int Count { get; }

		private protected DeviceFilterLogicalExpression(LogicalOperator @operator) => LogicalOperator = @operator;

		private protected abstract ReadOnlySpan<DeviceFilterExpression> GetSpan();

		internal override int GetFilterElementCount(bool isRoot) => isRoot && LogicalOperator != LogicalOperator.Or ? Count : Count + 2;

		internal override void FillExpressions(Span<NativeMethods.DevicePropertyFilterExpression> expressions, bool isRoot, out int count)
		{
			int n = 0;
			bool shouldEnclose = LogicalOperator == LogicalOperator.Or || !isRoot;
			if (shouldEnclose)
			{
				expressions[0] = new NativeMethods.DevicePropertyFilterExpression
				{
					Operator = LogicalOperator switch
					{
						LogicalOperator.And => NativeMethods.DevPropertyOperator.AndOpen,
						LogicalOperator.Or => NativeMethods.DevPropertyOperator.OrOpen,
						_ => throw new NotSupportedException()
					},
				};
				expressions = expressions.Slice(1);
				n++;
			}
			foreach (var expression in GetSpan())
			{
				expression.FillExpressions(expressions, false, out int m);
				expressions = expressions.Slice(m);
				n += m;
			}
			if (shouldEnclose)
			{
				expressions[0] = new NativeMethods.DevicePropertyFilterExpression
				{
					Operator = LogicalOperator switch
					{
						LogicalOperator.And => NativeMethods.DevPropertyOperator.AndClose,
						LogicalOperator.Or => NativeMethods.DevPropertyOperator.OrClose,
						_ => throw new NotSupportedException()
					},
				};
				expressions = expressions.Slice(1);
				n++;
			}
			count = n;
		}

		internal override void ReleaseExpressionResources()
		{
			foreach (var expression in GetSpan())
			{
				expression.ReleaseExpressionResources();
			}
		}

		internal static DeviceFilterLogicalExpression Create(LogicalOperator @operator, DeviceFilterExpression expression1, DeviceFilterExpression expression2)
		{
			if (expression1 is DeviceFilterLogicalExpression l1 && l1.LogicalOperator == @operator)
			{
				if (expression2 is DeviceFilterLogicalExpression l2 && l2.LogicalOperator == l1.LogicalOperator)
				{
					int count = l1.Count + l2.Count;

					// Count can only be ≥ 4 because a logical expression is at least of size 2.
					if (count == 4)
					{
						var s1 = l1.GetSpan();
						var s2 = l2.GetSpan();

						return new DeviceFilterLogical4Expression(@operator, s1[0], s1[1], s2[0], s2[1]);
					}
					else
					{
						var array = new DeviceFilterExpression[count];

						l1.GetSpan().CopyTo(array);
						l2.GetSpan().CopyTo(array.AsSpan(l1.Count));

						return new DeviceFilterLogicalNExpression(@operator, array);
					}
				}
				else
				{
					int count = l1.Count + 1;
					var s1 = l1.GetSpan();

					if (count == 3)
					{
						return new DeviceFilterLogical3Expression(@operator, s1[0], s1[1], expression2);

					}
					else if (count == 4)
					{
						return new DeviceFilterLogical4Expression(@operator, s1[0], s1[1], s1[2], expression2);
					}
					else
					{
						var array = new DeviceFilterExpression[count];

						l1.GetSpan().CopyTo(array);
						array[array.Length - 1] = expression2;

						return new DeviceFilterLogicalNExpression(@operator, array);
					}
				}
			}
			else if (expression2 is DeviceFilterLogicalExpression l2 && l2.LogicalOperator == @operator)
			{
				int count = l2.Count + 1;
				var s2 = l2.GetSpan();

				if (count == 3)
				{
					return new DeviceFilterLogical3Expression(@operator, expression2, s2[0], s2[1]);

				}
				else if (count == 4)
				{
					return new DeviceFilterLogical4Expression(@operator, expression2, s2[0], s2[1], s2[2]);
				}
				else
				{
					var array = new DeviceFilterExpression[count];

					array[0] = expression2;
					l2.GetSpan().CopyTo(array.AsSpan(1));

					return new DeviceFilterLogicalNExpression(@operator, array);
				}
			}
			return new DeviceFilterLogical2Expression(@operator, expression1, expression2);
		}
	}
}
