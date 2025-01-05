using System.Numerics;
using Exo.Cooling;

namespace Exo.Service;

internal partial class CoolingService
{
	private abstract class HardwareCurveCoolerState
	{
		public abstract CoolerChange CreateCoolerChange();
		public abstract HardwareCurveCoolingMode GetPersistedConfiguration();
	}

	private sealed class HardwareCurveCoolerState<TInput> : HardwareCurveCoolerState
		where TInput : struct, INumber<TInput>
	{
		private readonly IHardwareCurveCoolerSensorCurveControl<TInput> _sensor;
		private readonly InterpolatedSegmentControlCurve<TInput, byte> _controlCurve;

		public HardwareCurveCoolerState(IHardwareCurveCoolerSensorCurveControl<TInput> sensor, InterpolatedSegmentControlCurve<TInput, byte> controlCurve)
		{
			_sensor = sensor;
			_controlCurve = controlCurve;
		}

		public override CoolerChange CreateCoolerChange() => CoolerChange.CreateHardwareCurve(_sensor, _controlCurve);

		public override HardwareCurveCoolingMode GetPersistedConfiguration()
			=> new HardwareCurveCoolingMode()
			{
				SensorId = _sensor.SensorId,
				Curve = CreatePersistedCurve(_controlCurve)
			};
	}
}
