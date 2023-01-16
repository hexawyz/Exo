using System.Diagnostics.CodeAnalysis;

namespace Exo;

public interface ISystemDeviceDriverRegistry
{
	bool TryGetDriver(string deviceName, [NotNullWhen(true)] out ISystemDeviceDriver? driver);
	bool TryRegisterDriver(ISystemDeviceDriver driver);
	bool TryUnregisterDriver(ISystemDeviceDriver driver);
}
