using System;
using System.Runtime.InteropServices;

namespace DeviceTools
{
	[StructLayout(LayoutKind.Sequential)]
	public readonly struct PropertyKey
	{
		public readonly Guid CategoryId;
		public readonly uint PropertyId;

		public PropertyKey(Guid categoryId, uint propertyId)
		{
			CategoryId = categoryId;
			PropertyId = propertyId;
		}
	}

	public abstract class Property
	{
		private readonly PropertyKey _key;
		public ref readonly PropertyKey Key => ref _key;
	}

	public static class Properties
	{

	}
}
