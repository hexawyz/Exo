using System;

namespace DeviceTools
{
	public abstract class Property
	{
		private readonly PropertyKey _key;
		public ref readonly PropertyKey Key => ref _key;
		public abstract DevicePropertyType Type { get; }

		private protected Property(Guid categoryId, uint propertyId) => _key = new(categoryId, propertyId);
	}
}
