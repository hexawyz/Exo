using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceTools.DisplayDevices.Mccs
{
	public readonly struct ValueDefinition
	{
		public ValueDefinition(byte value, string? name)
		{
			Value = value;
			Name = name;
		}

		public byte Value { get; }
		public string? Name { get; }
	}

	public readonly struct ValueDefinition<T>
		where T : struct, Enum
	{
		public T Value { get; }
		public string Name { get; }
	}
}
