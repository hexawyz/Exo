using System.Diagnostics.CodeAnalysis;
using Exo.Core;

namespace Exo.Service
{
	public interface ISystemDeviceDriverRegistry
	{
		void RegisterDriver(Driver driver, string deviceName);
		bool TryGetDriver(string deviceName, [NotNullWhen(true)] out Driver? driver);
	}
}
