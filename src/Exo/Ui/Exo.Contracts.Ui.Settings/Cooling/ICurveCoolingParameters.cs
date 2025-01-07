namespace Exo.Contracts.Ui.Settings.Cooling;

public interface ICurveCoolingParameters : ICoolingParameters
{
	Guid SensorId { get; }
	CoolingControlCurve ControlCurve { get; }
}
