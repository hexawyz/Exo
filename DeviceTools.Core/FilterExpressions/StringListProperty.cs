﻿using System;

namespace DeviceTools.FilterExpressions
{
	public sealed class StringListProperty : Property, IProperty<string>
	{
		internal StringListProperty(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

		public override DevicePropertyType Type => DevicePropertyType.StringList;

		public DeviceFilterExpression Contains(string text) => DeviceFilterPropertyComparisonExpression.Create(this, text, StringListOperator.Contains);
		public DeviceFilterExpression ContainsIgnoreCase(string text) => DeviceFilterPropertyComparisonExpression.Create(this, text, StringListOperator.ContainsIgnoreCase);

		public DeviceFilterExpression ContainsElementStartingWith(string text) => DeviceFilterPropertyComparisonExpression.Create(this, text, StringListOperator.ContainsElementStartingWith);
		public DeviceFilterExpression ContainsElementStartingWithIgnoreCase(string text) => DeviceFilterPropertyComparisonExpression.Create(this, text, StringListOperator.ContainsElementStartingWithIgnoreCase);

		public DeviceFilterExpression ContainsElementEndingWith(string text) => DeviceFilterPropertyComparisonExpression.Create(this, text, StringListOperator.ContainsElementEndingWith);
		public DeviceFilterExpression ContainsElementEndingWithIgnoreCase(string text) => DeviceFilterPropertyComparisonExpression.Create(this, text, StringListOperator.ContainsElementEndingWithIgnoreCase);

		public DeviceFilterExpression ContainsElementContainingWith(string text) => DeviceFilterPropertyComparisonExpression.Create(this, text, StringListOperator.ContainsElementContaining);
		public DeviceFilterExpression ContainsElementContainingWithIgnoreCase(string text) => DeviceFilterPropertyComparisonExpression.Create(this, text, StringListOperator.ContainsElementContainingIgnoreCase);
	}
}
