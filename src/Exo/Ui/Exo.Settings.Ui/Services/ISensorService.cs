using System.Numerics;

namespace Exo.Settings.Ui.Services;

internal interface ISensorService
{
	IAsyncEnumerable<TValue> WatchValuesAsync<TValue>(Guid deviceId, Guid sensorId, CancellationToken cancellationToken)
		where TValue : unmanaged, INumber<TValue>;
}
