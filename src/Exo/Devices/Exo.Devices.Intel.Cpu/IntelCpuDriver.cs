using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks.Sources;
using DeviceTools.Processors;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Sensors;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Intel.Cpu;

public partial class IntelCpuDriver : Driver, IDeviceDriver<ISensorDeviceFeature>, ISensorsFeature, ISensorsGroupedQueryFeature
{
	private const uint Ia32ThermStatus = 0x19C;
	private const uint MsrTemperatureTarget = 0x1A2;
	private const uint Ia32PackageThermStatus = 0x1B1;
	private const uint MsrRaplPowerUnit = 0x606;
	private const uint MsrPkgEnergyStatus = 0x611;
	private const uint MsrPp0EnergyStatus = 0x639;

	[DiscoverySubsystem<CpuDiscoverySubsystem>]
	[X86CpuVendorId("GenuineIntel")]
	public static Task<DriverCreationResult<SystemCpuDeviceKey>?> CreateAsync
	(
		ILogger<IntelCpuDriver> logger,
		ImmutableArray<SystemCpuDeviceKey> keys,
		int processorIndex,
		ProcessorPackageInformation packageInformation
	)
	{
		var tcs = new TaskCompletionSource<DriverCreationResult<SystemCpuDeviceKey>?>(TaskCreationOptions.RunContinuationsAsynchronously);
		var thread = new Thread(InitializeCpu) { IsBackground = true };
		thread.Start(Tuple.Create(tcs, logger, keys, processorIndex, packageInformation));
		return tcs.Task;
	}

	private static void InitializeCpu(object? state)
	{
		ArgumentNullException.ThrowIfNull(state);

		var t = (Tuple<TaskCompletionSource<DriverCreationResult<SystemCpuDeviceKey>?>, ILogger<IntelCpuDriver>, ImmutableArray<SystemCpuDeviceKey>, int, ProcessorPackageInformation>)state;

		PawnIo? pawnIo = null;
		try
		{
			ProcessorAffinity.SetForCurrentThread(t.Item5.GroupAffinities);

			int eax, ebx, ecx, edx;

			(eax, ebx, ecx, edx) = X86Base.CpuId(unchecked((int)0x80000000U), 0);

			if ((eax & int.MinValue) == 0 || eax - unchecked((int)0x80000004U) < 4) throw new PlatformNotSupportedException("Brand String is not supported.");

			// We need to identify the CPU models for some subset of capabilities
			(eax, ebx, ecx, edx) = X86Base.CpuId(1, 0);

			var processorInformation = ParseProcessorInformation((uint)eax);
			t.Item2.IntelProcessorInformation((ushort)(t.Item4 + 1), processorInformation.ProcessorType, processorInformation.FamilyId, processorInformation.ModelId, processorInformation.SteppingId);

			string brandString = ReadBrandString();

			// Read CPU thermal information.
			(eax, ebx, ecx, edx) = X86Base.CpuId(6, 0);

			ProcessorCapabilities capabilities = ProcessorCapabilities.None;

			if ((eax & 1) != 0) capabilities |= ProcessorCapabilities.TemperatureSensor;

			byte tccActivationTemperature = 0;

			try
			{
				pawnIo = new PawnIo();
				pawnIo.LoadModuleFromResource(typeof(IntelCpuDriver).Assembly, "intel_msr.bin");
			}
			catch
			{
				throw new MissingKernelDriverException(brandString);
			}

			if ((capabilities & ProcessorCapabilities.TemperatureSensor) != 0)
			{
				ulong temperatureTarget = ReadMsr(pawnIo, MsrTemperatureTarget);

				tccActivationTemperature = (byte)(temperatureTarget >> 16);
			}

			// Based on the Intel documentation, Intel CPUs seem to have two distinct ways of representing power units.
			// We will need to find if the current CPU fits inside one category or the other.
			// Values are stored at the exact same location, but they can either be 2^N mW/µJ or 2^(-N) W/J/S.

			// I'm assuming all newer processors use the 2^-N version of power units.
			// The condition list is based from a "quick" parsing of the docs to find which versions explicitly reference the old model.
			// Sadly, the documentation is exhaustive but it has many cross-references, so I may have missed some.
			if (processorInformation.FamilyId == 6 && processorInformation.ModelId is 0x2A or 0x2D or 0x37 or 0x4A or 0x4C or 0x4D or 0x5A or 0x5C or 0x5D)
			{
				// NB: From what I understand, all processors that have units expressed in 2^N have a time unit of 1s (N = 0)
				// However, I don't know if we can use the fact that the time unit is 1s to detect that the CPU has the 2^N behavior.
				capabilities |= ProcessorCapabilities.EnergyUnitPositivePowerOfTwo;
			}

			// There doesn't seem to be any flag to indicate that energy sensors are present. So for now, assume that they always are.
			capabilities |= ProcessorCapabilities.EnergySensor;

			ulong powerUnits = ReadMsr(pawnIo, MsrRaplPowerUnit);

			t.Item1.TrySetResult
			(
				new(t.Item3, new IntelCpuDriver(t.Item2, pawnIo, t.Item5, tccActivationTemperature, brandString, t.Item4, capabilities, powerUnits))
			);
		}
		catch (Exception ex)
		{
			pawnIo?.Dispose();
			t.Item1.TrySetException(ex);
		}
	}

	private static ulong ReadMsr(PawnIo pawnIo, uint registerIndex)
	{
		ulong msr = registerIndex;
		ulong result = 0;
		_ = pawnIo.Execute("ioctl_read_msr\0"u8, new(ref msr), new(ref result));
		return result;
	}

	private static ProcessorInformation ParseProcessorInformation(nuint eax)
	{
		nuint steppingId = eax & 0xF;
		nuint modelId = (eax >>> 4) & 0xF;
		nuint familyId = (eax >>> 8) & 0xF;
		nuint processorType = (eax >> 12) & 0x3;

		if (familyId == 0xF)
		{
			familyId += (byte)(eax >>> 20);
			goto IdentifyExtendedModelId;
		}
		else if (familyId == 0x6)
		{
			goto IdentifyExtendedModelId;
		}
		goto ModelIdIdentified;

	IdentifyExtendedModelId:;
		modelId |= (eax >> 12) & 0xF0;
	ModelIdIdentified:;

		return new((byte)steppingId, (byte)modelId, (ushort)familyId, (ProcessorType)(byte)processorType);
	}

	private static string ReadBrandString()
	{
		Span<int> buffer = stackalloc int[12];

		(buffer[0], buffer[1], buffer[2], buffer[3]) = X86Base.CpuId(unchecked((int)0x80000002U), 0);
		(buffer[4], buffer[5], buffer[6], buffer[7]) = X86Base.CpuId(unchecked((int)0x80000003U), 0);
		(buffer[8], buffer[9], buffer[10], buffer[11]) = X86Base.CpuId(unchecked((int)0x80000004U), 0);

		var brandName = MemoryMarshal.Cast<int, byte>(buffer);

		int endIndex = brandName.IndexOf((byte)0);

		return Encoding.UTF8.GetString(endIndex < 0 ? brandName : brandName[..endIndex]);
	}

	private readonly PawnIo? _pawnIo;
	private readonly ProcessorPackageInformation _packageInformation;
	private readonly ManualResetEvent? _groupedQueryEventRed;
	private readonly ManualResetEvent? _groupedQueryEventBlue;
	private readonly GroupedQueryValueTaskSource? _groupedQueryValueTaskSource;
	private readonly uint _tccActivationTemperature;
	private readonly ImmutableArray<ISensor> _sensors;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly double _energyFactor;
	private uint _groupQueryThreadCount;
	private uint _pendingGroupQueryThreadCount;
	private bool _groupedQueryColor;
	private readonly ProcessorCapabilities _capabilities;
	private readonly byte _powerUnit;
	private readonly byte _energyUnit;
	private readonly byte _timeUnit;
	private bool _isDisposed;
	private readonly MonitoringThreadState[] _monitoringThreads;

	private IntelCpuDriver
	(
		ILogger<IntelCpuDriver> logger,
		PawnIo? pawnIo,
		ProcessorPackageInformation packageInformation,
		byte tccActivationTemperature,
		string brandString,
		int processorIndex,
		ProcessorCapabilities capabilities,
		ulong powerUnits
	) : base(brandString, new("IntelX86", string.Create(CultureInfo.InvariantCulture, $"{processorIndex}:{brandString}"), brandString, null))
	{
		_pawnIo = pawnIo;
		_packageInformation = packageInformation;
		_tccActivationTemperature = tccActivationTemperature;
		_capabilities = capabilities;
		_powerUnit = (byte)(powerUnits & 0xF);
		_energyUnit = (byte)((powerUnits >>> 8) & 0xF);
		_powerUnit = (byte)((powerUnits >>> 16) & 0xF);

		if ((capabilities & ProcessorCapabilities.EnergySensor) != 0)
		{
			// The energy counters are expressed in joules but we will compute the diff, which will give us a value per second.
			_energyFactor = (capabilities & ProcessorCapabilities.EnergyUnitPositivePowerOfTwo) != 0
				? (1 << _energyUnit) / 1000d
				: 1d / (1 << _energyUnit);
		}

		Sensor[] sensors = [];
		MonitoringThreadState[] monitoringThreads = [];
		try
		{
			// For user-friendliness and consistence in the naming of threads.
			int processorNumber = processorIndex + 1;
			if (_pawnIo is not null && (capabilities & (ProcessorCapabilities.TemperatureSensor | ProcessorCapabilities.EnergySensor)) != 0)
			{
				// This will hopefully be enough to process the small amount of operations of each thread.
				const int ThreadStackSize = 4096;

				// We likely shouldn't try to expose per-core temperature sensors if there is only one CPU core?
				// Also, this sensor code might be expensive for high core count, as we spawn a thread for each core.
				// While this is the easiest way to handle everything, it might be worth it to use the same thread for N cores at the cost of rescheduling.
				int sensorCount = 0;
				int threadCount = 0;
				int sensorIndex = 0;
				int threadIndex = 0;

				if ((capabilities & ProcessorCapabilities.TemperatureSensor) != 0) sensorCount += 1;
				if ((capabilities & ProcessorCapabilities.EnergySensor) != 0) sensorCount += 1;
				threadCount += 1;

				if (packageInformation.Cores.Length > 1)
				{
					if ((capabilities & ProcessorCapabilities.TemperatureSensor) != 0) sensorCount += packageInformation.Cores.Length;
					if ((capabilities & ProcessorCapabilities.EnergySensor) != 0) sensorCount += packageInformation.Cores.Length;
					threadCount += packageInformation.Cores.Length;
				}

				sensors = new Sensor[sensorCount];
				monitoringThreads = new MonitoringThreadState[threadCount];

				{
					var @event = new ManualResetEvent(false);
					var thread = new Thread
					(
						new ParameterizedThreadStart(ReadPackageSensors),
						ThreadStackSize
					)
					{
						IsBackground = true,
						Name = string.Create(CultureInfo.InvariantCulture, $"Intel CPU #{processorNumber} - Metrics"),
					};

					PackageTemperatureSensor? temperatureSensor = null;
					PackagePowerSensor? powerSensor = null;

					if ((capabilities & ProcessorCapabilities.TemperatureSensor) != 0) sensors[sensorIndex++] = temperatureSensor = new PackageTemperatureSensor(this);
					if ((capabilities & ProcessorCapabilities.EnergySensor) != 0) sensors[sensorIndex++] = powerSensor = new PackagePowerSensor(this);

					var state = new MonitoringThreadState(@event, thread, temperatureSensor, powerSensor);
					monitoringThreads[threadIndex++] = state;
					thread.Start(state);
				}

				if (packageInformation.Cores.Length > 1)
				{
					var readCoreSensors = new ParameterizedThreadStart(ReadCoreSensors);

					for (int i = 0; i < packageInformation.Cores.Length; i++)
					{
						var @event = new ManualResetEvent(false);
						var thread = new Thread
						(
							readCoreSensors,
							ThreadStackSize
						)
						{
							IsBackground = true,
							Name = string.Create(CultureInfo.InvariantCulture, $"Intel CPU #{processorNumber} Core #{i} - Metrics"),
						};

						CoreTemperatureSensor? temperatureSensor = null;
						CorePowerSensor? powerSensor = null;

						if ((capabilities & ProcessorCapabilities.TemperatureSensor) != 0) sensors[sensorIndex++] = temperatureSensor = new CoreTemperatureSensor(this, i);
						if ((capabilities & ProcessorCapabilities.EnergySensor) != 0) sensors[sensorIndex++] = powerSensor = new CorePowerSensor(this, i);

						var state = new MonitoringThreadState(@event, thread, temperatureSensor, powerSensor);
						monitoringThreads[threadIndex++] = state;
						thread.Start(state);
					}
				}

				_groupedQueryEventRed = new(false);
				_groupedQueryEventBlue = new(false);
				_groupedQueryValueTaskSource = new();
			}
			_monitoringThreads = monitoringThreads;
			_sensors = ImmutableCollectionsMarshal.AsImmutableArray(Unsafe.As<ISensor[]>(sensors));
			_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, IntelCpuDriver, ISensorsFeature, ISensorsGroupedQueryFeature>(this);
		}
		catch
		{
			// In case of error, make sure to clean up all the threads.
			Volatile.Write(ref _isDisposed, true);
			SignalAndWaitAllThreads();
			throw;
		}
	}

	public override ValueTask DisposeAsync()
	{
		if (!Interlocked.Exchange(ref _isDisposed, true))
		{
			if (_groupedQueryEventRed is not null && _groupedQueryEventBlue is not null)
			{
				_groupedQueryEventRed.Set();
				_groupedQueryEventBlue.Set();
				SignalAndWaitAllThreads();
			}
			_pawnIo?.Dispose();
		}
		return ValueTask.CompletedTask;
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Processor;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => _sensors;

	void ISensorsGroupedQueryFeature.AddSensor(IPolledSensor sensor)
		=> EnableSensor((Sensor)sensor);

	void ISensorsGroupedQueryFeature.RemoveSensor(IPolledSensor sensor)
		=> DisableSensor((Sensor)sensor);

	private void EnableSensor(Sensor sensor)
	{
		lock (this)
		{
			if (sensor._isQueried) return;
			Volatile.Write(ref sensor._isQueried, true);
			var threadState = sensor.GetThreadState();
			if (threadState.IsEnabled) return;
			Volatile.Write(ref threadState.IsEnabled, true);
			threadState.Event.Set();
			++_groupQueryThreadCount;
		}
	}

	private void DisableSensor(Sensor sensor)
	{
		lock (this)
		{
			if (!sensor._isQueried) return;
			Volatile.Write(ref sensor._isQueried, false);
			var threadState = sensor.GetThreadState();
			if (!threadState.IsEnabled) return;
			Volatile.Write(ref threadState.IsEnabled, false);
			threadState.Event.Reset();
			--_groupQueryThreadCount;
		}
	}

	// NB: This will break if the method is called more than once at a time.
	ValueTask ISensorsGroupedQueryFeature.QueryValuesAsync(CancellationToken cancellationToken)
	{
		lock (this)
		{
			if (_groupedQueryEventRed is null || _groupedQueryEventBlue is null || Volatile.Read(ref _isDisposed) || _groupQueryThreadCount == 0) return ValueTask.CompletedTask;
			if (Volatile.Read(ref _pendingGroupQueryThreadCount) != 0) throw new InvalidOperationException("There should not be any running query at the moment.");
			Volatile.Write(ref _pendingGroupQueryThreadCount, _groupQueryThreadCount);
			if (_groupedQueryColor)
			{
				_groupedQueryEventBlue.Reset();
				_groupedQueryColor = false;
				_groupedQueryEventRed.Set();
			}
			else
			{
				_groupedQueryEventRed.Reset();
				_groupedQueryColor = true;
				_groupedQueryEventBlue.Set();
			}
		}
		return _groupedQueryValueTaskSource!.AsValueTask();
	}

	private void SignalAndWaitAllThreads()
	{
		foreach (var monitoringThread in _monitoringThreads)
		{
			// As soon as we reach a structure is not initialized, it means that all threads have been handled.
			if (monitoringThread.Event is null) break;
			monitoringThread.Event.Set();
		}
		foreach (var monitoringThread in _monitoringThreads)
		{
			// As soon as we reach a structure is not initialized, it means that all threads have been handled.
			if (monitoringThread.Event is null) break;
			monitoringThread.Thread.Join();
		}
	}

	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref _isDisposed)) throw new ObjectDisposedException(GetType().FullName);
	}

	private void ReadPackageSensors(object? state)
		=> ReadPackageSensors(Unsafe.As<MonitoringThreadState>(state!));

	private void ReadPackageSensors(MonitoringThreadState state)
	{
		ProcessorAffinity.SetForCurrentThread(_packageInformation.GroupAffinities);
		// Start counting time 100s in the past. (That way, we invalidate any possible reading)
		ulong lastReadingTimestamp = (ulong)(Stopwatch.GetTimestamp() - 100 * Stopwatch.Frequency);
		ulong readingTimestamp = lastReadingTimestamp;
		short temperature = 0;
		uint lastPowerReading = 0;
		uint powerReading = 0;
		while (true)
		{
			state.Event.WaitOne();

			if (Volatile.Read(ref _isDisposed)) return;

			while (true)
			{
				(_groupedQueryColor ? _groupedQueryEventRed : _groupedQueryEventBlue)!.WaitOne();

				readingTimestamp = (ulong)Stopwatch.GetTimestamp();

				if (readingTimestamp - lastReadingTimestamp >= (ulong)Stopwatch.Frequency)
				{
					if (state.TemperatureSensor is not null) temperature = (short)(_tccActivationTemperature - (ReadMsr(_pawnIo!, Ia32PackageThermStatus) >> 16) & 0x7F);
					if (state.PowerSensor is not null) powerReading = (uint)ReadMsr(_pawnIo!, MsrPkgEnergyStatus);

					if (state.TemperatureSensor is not null) state.TemperatureSensor._value = temperature;
					// Generate a NaN reading if the previous data point was too old.
					if (state.PowerSensor is not null)
					{
						state.PowerSensor._value = readingTimestamp - lastReadingTimestamp >= 60 * (ulong)Stopwatch.Frequency ?
							double.NaN :
							(ulong)Stopwatch.Frequency * (powerReading - lastPowerReading) * _energyFactor / (readingTimestamp - lastReadingTimestamp);
					}

					lastPowerReading = powerReading;
					lastReadingTimestamp = readingTimestamp;
				}

				if (Interlocked.Decrement(ref _pendingGroupQueryThreadCount) == 0) _groupedQueryValueTaskSource!.SetResult();

				if (Volatile.Read(ref _isDisposed)) return;

				if (!(state.TemperatureSensor?._isQueried == true || state.PowerSensor?._isQueried == true)) break;
			}

			state.Event.Reset();
		}
	}

	private void ReadCoreSensors(object? state)
		=> ReadCoreSensors(Unsafe.As<MonitoringThreadState>(state!));

	private void ReadCoreSensors(MonitoringThreadState state)
	{
		SetAffinityForCore(Unsafe.As<CoreTemperatureSensor>(state.TemperatureSensor)?.CoreIndex ?? Unsafe.As<CorePowerSensor>(state.TemperatureSensor)!.CoreIndex);
		// Start counting time 100s in the past. (That way, we invalidate any possible reading)
		ulong lastReadingTimestamp = (ulong)(Stopwatch.GetTimestamp() - 10 * Stopwatch.Frequency);
		ulong readingTimestamp = lastReadingTimestamp;
		short temperature = 0;
		uint lastPowerReading = 0;
		uint powerReading = 0;
		while (true)
		{
			state.Event.WaitOne();

			if (Volatile.Read(ref _isDisposed)) return;

			while (true)
			{
				(_groupedQueryColor ? _groupedQueryEventRed : _groupedQueryEventBlue)!.WaitOne();

				readingTimestamp = (ulong)Stopwatch.GetTimestamp();

				if (readingTimestamp - lastReadingTimestamp >= (ulong)Stopwatch.Frequency)
				{
					if (state.TemperatureSensor is not null) temperature = (short)(_tccActivationTemperature - (ReadMsr(_pawnIo!, Ia32ThermStatus) >> 16) & 0x7F);
					if (state.PowerSensor is not null) powerReading = (uint)ReadMsr(_pawnIo!, MsrPp0EnergyStatus);

					if (state.TemperatureSensor is not null) state.TemperatureSensor._value = temperature;
					// Generate a NaN reading if the previous data point was too old.
					if (state.PowerSensor is not null)
					{
						state.PowerSensor._value = readingTimestamp - lastReadingTimestamp >= 60 * (ulong)Stopwatch.Frequency ?
							double.NaN :
							(ulong)Stopwatch.Frequency * (powerReading - lastPowerReading) * _energyFactor / (readingTimestamp - lastReadingTimestamp);
					}

					lastPowerReading = powerReading;
					lastReadingTimestamp = readingTimestamp;
				}

				if (Interlocked.Decrement(ref _pendingGroupQueryThreadCount) == 0) _groupedQueryValueTaskSource!.SetResult();

				if (Volatile.Read(ref _isDisposed)) return;

				if (!(state.TemperatureSensor?._isQueried == true || state.PowerSensor?._isQueried == true)) break;
			}

			state.Event.Reset();
		}
	}

	private void SetAffinityForCore(int coreIndex)
	{
		var core = _packageInformation.Cores[coreIndex];
		ProcessorAffinity.SetForCurrentThread((nuint)core.GroupAffinity.Mask, core.GroupAffinity.Group);
	}

	private abstract class Sensor
	{
		private readonly IntelCpuDriver _driver;
		internal bool _isQueried;

		protected Sensor(IntelCpuDriver driver)
		{
			_driver = driver;
		}

		public GroupedQueryMode GroupedQueryMode => _isQueried ? GroupedQueryMode.Enabled : GroupedQueryMode.Supported;

		protected IntelCpuDriver Driver => _driver;

		internal abstract MonitoringThreadState GetThreadState();
	}

	private abstract class Sensor<T> : Sensor
		where T : unmanaged
	{
		internal T _value;

		protected Sensor(IntelCpuDriver driver) : base(driver)
		{
		}

		public ValueTask<T> GetValueAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

		public bool TryGetLastValue(out T lastValue)
		{
			lastValue = _value;
			return true;
		}
	}

	private abstract class PackageSensor<T> : Sensor<T>
		where T : unmanaged
	{
		protected PackageSensor(IntelCpuDriver driver) : base(driver) { }

		internal sealed override MonitoringThreadState GetThreadState() => Driver._monitoringThreads[0];
	}

	private abstract class CoreSensor<T> : Sensor<T>
		where T : unmanaged
	{
		public int CoreIndex { get; }

		protected CoreSensor(IntelCpuDriver driver, int coreIndex) : base(driver)
		{
			if (coreIndex > ProcessorCoreTemperatureSensorIds.Count) throw new PlatformNotSupportedException();
			CoreIndex = coreIndex;
		}

		internal sealed override MonitoringThreadState GetThreadState() => Driver._monitoringThreads[CoreIndex + 1];
	}

	private sealed class PackageTemperatureSensor : PackageSensor<short>, IPolledSensor<short>
	{
		public PackageTemperatureSensor(IntelCpuDriver driver) : base(driver) { }

		short? ISensor<short>.ScaleMinimumValue => (short)(Driver._tccActivationTemperature - 0x7F);
		short? ISensor<short>.ScaleMaximumValue => (short)Driver._tccActivationTemperature;
		Guid ISensor.SensorId => ProcessorPackageTemperatureSensorId;
		SensorUnit ISensor.Unit => SensorUnit.Celsius;
	}

	private sealed class PackagePowerSensor : PackageSensor<double>, IPolledSensor<double>
	{
		public PackagePowerSensor(IntelCpuDriver driver) : base(driver) { }

		double? ISensor<double>.ScaleMinimumValue => 0;
		double? ISensor<double>.ScaleMaximumValue => 500;
		Guid ISensor.SensorId => ProcessorPackagePowerSensorId;
		SensorUnit ISensor.Unit => SensorUnit.Watts;
	}

	private sealed class CoreTemperatureSensor : CoreSensor<short>, IPolledSensor<short>
	{
		public CoreTemperatureSensor(IntelCpuDriver driver, int coreIndex) : base(driver, coreIndex) { }

		short? ISensor<short>.ScaleMinimumValue => (short)(Driver._tccActivationTemperature - 0x7F);
		short? ISensor<short>.ScaleMaximumValue => (short)Driver._tccActivationTemperature;
		Guid ISensor.SensorId => ProcessorCoreTemperatureSensorIds[CoreIndex];
		SensorUnit ISensor.Unit => SensorUnit.Celsius;
	}

	private sealed class CorePowerSensor : CoreSensor<double>, IPolledSensor<double>
	{
		public CorePowerSensor(IntelCpuDriver driver, int coreIndex) : base(driver, coreIndex) { }

		double? ISensor<double>.ScaleMinimumValue => 0;
		double? ISensor<double>.ScaleMaximumValue => 500;
		Guid ISensor.SensorId => ProcessorCorePowerSensorIds[CoreIndex];
		SensorUnit ISensor.Unit => SensorUnit.Watts;
	}

	private sealed class GroupedQueryValueTaskSource : IValueTaskSource
	{
		private ManualResetValueTaskSourceCore<byte> _core = new() { RunContinuationsAsynchronously = true };

		public void SetResult() => _core.SetResult(0);
		public void SetDisposed() => _core.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(IntelCpuDriver).FullName)));

		public void GetResult(short token)
		{
			_ = _core.GetResult(token);
			_core.Reset();
		}

		public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

		public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
			=> _core.OnCompleted(continuation, state, token, flags);

		public ValueTask AsValueTask() => new(this, _core.Version);
	}

	private sealed class MonitoringThreadState(ManualResetEvent @event, Thread thread, Sensor<short>? temperatureSensor, Sensor<double>? powerSensor)
	{
		public readonly ManualResetEvent Event = @event;
		public readonly Thread Thread = thread;
		public readonly Sensor<short>? TemperatureSensor = temperatureSensor;
		public readonly Sensor<double>? PowerSensor = powerSensor;
		public bool IsEnabled;
	}

	[Flags]
	private enum ProcessorCapabilities : byte
	{
		None = 0,
		TemperatureSensor = 1,
		EnergySensor = 2,
		EnergyUnitPositivePowerOfTwo = 4,
	}
}
