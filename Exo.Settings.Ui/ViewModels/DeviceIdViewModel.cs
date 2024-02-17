using System.Globalization;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

public sealed class DeviceIdViewModel
{
	private readonly DeviceId _deviceId;
	private readonly bool _isMainDeviceId;

	public DeviceIdViewModel(DeviceId deviceId, bool isMainDeviceId)
	{
		_deviceId = deviceId;
		_isMainDeviceId = isMainDeviceId;
	}

	public DeviceIdSource Source => _deviceId.Source;
	public VendorIdSource VendorIdSource => _deviceId.VendorIdSource;
	public ushort RawVendorId => _deviceId.VendorId;
	public string VendorId => VendorIdSource == VendorIdSource.PlugAndPlay ?
		string.Create(3, _deviceId.VendorId, (s, v) => (s[0], s[1], s[2]) = ((char)('A' - 1 + (v >> 10)), (char)('A' - 1 + ((v >> 5) & 0x1f)), (char)('A' - 1 + (v & 0x1f)))) :
		_deviceId.VendorId.ToString("X4", CultureInfo.InvariantCulture);
	public ushort ProductId => _deviceId.ProductId;
	public ushort RawVersion => _deviceId.Version;
	public ushort? Version => _deviceId.Version != 0xFFFF ? _deviceId.Version : null;
	public bool IsMainDeviceId => _isMainDeviceId;
}
