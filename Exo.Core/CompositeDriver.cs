using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exo.Core
{
	/// <summary>Base class for a driver that manages composite devices.</summary>
	/// <remarks>Such a driver manages exposes no features of its own, but exposes its own child driver instances.</remarks>
	public abstract class CompositeDriver
	{
		public IReadOnlyList<Driver> Drivers { get; }
	}
}
