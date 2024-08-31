using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exo
{
	public interface IDeviceDriverLifetime
	{
		public void Initialize()
		{
		}

		public void Dispose()
		{
		}
	}
}
