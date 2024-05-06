using System.Numerics;
using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcSensorService : ISensorService
{
	private interface ISensorDataPointConverter<TValue>
		where TValue : struct, INumber<TValue>
	{
		abstract static SensorDataPoint ConvertDataPoint(SensorDataPoint<TValue> dataPoint);
	}

	private sealed class ByteDataPointConverter : ISensorDataPointConverter<byte>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<byte> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = dataPoint.Value };
	}

	private sealed class SByteDataPointConverter : ISensorDataPointConverter<sbyte>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<sbyte> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = dataPoint.Value };
	}

	private sealed class Int16DataPointConverter : ISensorDataPointConverter<short>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<short> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = dataPoint.Value };
	}

	private sealed class UInt16DataPointConverter : ISensorDataPointConverter<ushort>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<ushort> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = dataPoint.Value };
	}

	private sealed class Int32DataPointConverter : ISensorDataPointConverter<int>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<int> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = dataPoint.Value };
	}

	private sealed class UInt32DataPointConverter : ISensorDataPointConverter<uint>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<uint> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = dataPoint.Value };
	}

	private sealed class Int64DataPointConverter : ISensorDataPointConverter<long>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<long> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = dataPoint.Value };
	}

	private sealed class UInt64DataPointConverter : ISensorDataPointConverter<ulong>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<ulong> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = dataPoint.Value };
	}

	private sealed class Int128DataPointConverter : ISensorDataPointConverter<Int128>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<Int128> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = (double)dataPoint.Value };
	}

	private sealed class UInt128DataPointConverter : ISensorDataPointConverter<UInt128>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<UInt128> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = (double)dataPoint.Value };
	}

	private sealed class HalfDataPointConverter : ISensorDataPointConverter<Half>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<Half> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = (double)dataPoint.Value };
	}

	private sealed class SingleDataPointConverter : ISensorDataPointConverter<float>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<float> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = dataPoint.Value };
	}

	private sealed class DoubleDataPointConverter : ISensorDataPointConverter<double>
	{
		public static SensorDataPoint ConvertDataPoint(SensorDataPoint<double> dataPoint) => new() { DateTime = dataPoint.DateTime, Value = dataPoint.Value };
	}

	private readonly SensorService _sensorService;
	private readonly ILogger<GrpcSensorService> _logger;

	public GrpcSensorService(SensorService sensorService, ILogger<GrpcSensorService> logger)
	{
		_sensorService = sensorService;
		_logger = logger;
	}

	public async IAsyncEnumerable<Contracts.Ui.Settings.SensorDeviceInformation> WatchSensorDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcSensorServiceDeviceWatchStart();
		try
		{
			await foreach (var device in _sensorService.WatchDevicesAsync(cancellationToken))
			{
				yield return device.ToGrpc();
			}
		}
		finally
		{
			_logger.GrpcSensorServiceDeviceWatchStop();
		}
	}

	public IAsyncEnumerable<SensorDataPoint> WatchValuesAsync(SensorReference sensor, CancellationToken cancellationToken)
	{
		if (!_sensorService.TryGetSensorInformation(sensor.DeviceId, sensor.SensorId, out var info)) return EmptyAsyncEnumerable<SensorDataPoint>.Instance;

		return info.DataType switch
		{
			SensorDataType.UInt8 => WatchValuesAsync<byte, ByteDataPointConverter>(sensor, cancellationToken),
			SensorDataType.UInt16 => WatchValuesAsync<ushort, UInt16DataPointConverter>(sensor, cancellationToken),
			SensorDataType.UInt32 => WatchValuesAsync<uint, UInt32DataPointConverter>(sensor, cancellationToken),
			SensorDataType.UInt64 => WatchValuesAsync<ulong, UInt64DataPointConverter>(sensor, cancellationToken),
			SensorDataType.UInt128 => WatchValuesAsync<UInt128, UInt128DataPointConverter>(sensor, cancellationToken),
			SensorDataType.SInt8 => WatchValuesAsync<sbyte, SByteDataPointConverter>(sensor, cancellationToken),
			SensorDataType.SInt16 => WatchValuesAsync<short, Int16DataPointConverter>(sensor, cancellationToken),
			SensorDataType.SInt32 => WatchValuesAsync<int, Int32DataPointConverter>(sensor, cancellationToken),
			SensorDataType.SInt64 => WatchValuesAsync<long, Int64DataPointConverter>(sensor, cancellationToken),
			SensorDataType.SInt128 => WatchValuesAsync<Int128, Int128DataPointConverter>(sensor, cancellationToken),
			SensorDataType.Float16 => WatchValuesAsync<float, SingleDataPointConverter>(sensor, cancellationToken),
			SensorDataType.Float32 => WatchValuesAsync<float, SingleDataPointConverter>(sensor, cancellationToken),
			SensorDataType.Float64 => WatchValuesAsync<double, DoubleDataPointConverter>(sensor, cancellationToken),
			_ => throw new InvalidOperationException($"Non supported data type: {info.DataType}."),
		};
	}

	private async IAsyncEnumerable<SensorDataPoint> WatchValuesAsync<TValue, TConverter>(SensorReference sensor, [EnumeratorCancellation] CancellationToken cancellationToken)
		where TValue : struct, INumber<TValue>
		where TConverter : ISensorDataPointConverter<TValue>
	{
		_logger.GrpcSensorServiceSensorWatchStart(sensor.DeviceId, sensor.SensorId);
		try
		{
			await foreach (var dataPoint in _sensorService.WatchValuesAsync<TValue>(sensor.DeviceId, sensor.SensorId, cancellationToken))
			{
				yield return TConverter.ConvertDataPoint(dataPoint);
			}
		}
		finally
		{
			_logger.GrpcSensorServiceSensorWatchStop(sensor.DeviceId, sensor.SensorId);
		}
	}
}
