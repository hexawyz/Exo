using System.Numerics;
using Exo.Cooling;

namespace Exo.Service;

internal partial class CoolingService
{
	private abstract class HardwareCurveCoolerState
	{
		public abstract CoolerChange CreateCoolerChange(IHardwareCurveCooler cooler);
		public abstract HardwareCurveCoolingMode GetPersistedConfiguration();
	}

	private sealed class HardwareCurveCoolerState<TInput> : HardwareCurveCoolerState
		where TInput : struct, INumber<TInput>
	{
		private readonly Guid _sensorId;
		private readonly InterpolatedSegmentControlCurve<TInput, byte> _controlCurve;

		public HardwareCurveCoolerState(Guid sensorId, InterpolatedSegmentControlCurve<TInput, byte> controlCurve)
		{
			_sensorId = sensorId;
			_controlCurve = controlCurve;
		}

		private static IHardwareCurveCoolerSensorCurveControl<TInput> GetSensor(IHardwareCurveCooler cooler, Guid sensorId)
		{
			foreach (var sensor in cooler.AvailableSensors)
			{
				if (sensor.SensorId == sensorId && sensor is IHardwareCurveCoolerSensorCurveControl<TInput> typedSensor) return typedSensor;
			}
			throw new InvalidOperationException();
		}

		public override CoolerChange CreateCoolerChange(IHardwareCurveCooler cooler) => CoolerChange.CreateHardwareCurve(GetSensor(cooler, _sensorId), _controlCurve);

		public override HardwareCurveCoolingMode GetPersistedConfiguration()
			=> new HardwareCurveCoolingMode()
			{
				SensorId = _sensorId,
				Curve = CreatePersistedCurve(_controlCurve)
			};
	}
}
