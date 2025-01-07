namespace Exo.Contracts.Ui.Settings.Cooling;

public interface ICoolingParameters
{
	Guid DeviceId { get; }
	Guid CoolerId { get; }
}
