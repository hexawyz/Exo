using System.Collections.Immutable;
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

public partial class IntelCpuDriver : Driver, IDeviceDriver<ISensorDeviceFeature>, ISensorsFeature
{
	private const uint Ia32ThermStatus = 0x19C;
	private const uint MsrTemperatureTarget = 0x1A2;
	private const uint Ia32PackageThermStatus = 0x1B1;

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

			// Read CPU thermal information.
			(eax, ebx, ecx, edx) = X86Base.CpuId(6, 0);

			bool hasTemperatureSensor = (eax & 1) != 0;
			byte tccActivationTemperature = 0;

			PawnIo? pawnIo = null;
			if (hasTemperatureSensor)
			{
				// TODO: Make this centralized somewhere so that we don't double-load anything.
				// For now, this is mostly a POC and there will only be a single CPU, so it is not yet a problem.
				pawnIo = new PawnIo();

				pawnIo.LoadModuleFromResource(typeof(IntelCpuDriver).Assembly, "intel_msr.bin");

				ulong temperatureTarget = ReadMsr(pawnIo, MsrTemperatureTarget);

				tccActivationTemperature = (byte)(temperatureTarget >> 16);
			}

			t.Item1.TrySetResult
			(
				new(t.Item3, new IntelCpuDriver(t.Item2, pawnIo, t.Item5, tccActivationTemperature, ReadBrandString(), t.Item4))
			);
		}
		catch (Exception ex)
		{
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
	private readonly uint _tccActivationTemperature;
	private readonly ImmutableArray<ISensor> _sensors;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private bool _isDisposed;

	private IntelCpuDriver
	(
		ILogger<IntelCpuDriver> logger,
		PawnIo? pawnIo,
		ProcessorPackageInformation packageInformation,
		byte tccActivationTemperature,
		string brandString,
		int processorIndex
	) : base(brandString, new("IntelX86", string.Create(CultureInfo.InvariantCulture, $"{processorIndex}:{brandString}"), brandString, null))
	{
		_pawnIo = pawnIo;
		_packageInformation = packageInformation;
		_tccActivationTemperature = tccActivationTemperature;
		Sensor[] sensors;
		// For user-friendliness and consistence in the naming of threads.
		int processorNumber = processorIndex + 1;
		if (_pawnIo is not null && tccActivationTemperature > 0)
		{
			// This will hopefully be enough to process the small amount of operations of each thread.
			const int ThreadStackSize = 4096;

			// We likely shouldn't try to expose per-core temperature sensors if there is only one CPU core?
			// Also, this sensor code might be expensive for high core count, as we spawn a thread for each core.
			// While this is the easiest way to handle everything, it might be worth it to use the same thread for N cores at the cost of rescheduling.
			sensors = new Sensor[packageInformation.Cores.Length > 1 ? 1 + packageInformation.Cores.Length : 1];
			sensors[0] = new PackageTemperatureSensor
			(
				this,
				new Thread
				(
					new ParameterizedThreadStart(ReadPackageSensors),
					ThreadStackSize
				)
				{
					IsBackground = true,
					Name = string.Create(CultureInfo.InvariantCulture, $"Intel CPU #{processorNumber} - Metrics")
				}
			);
			if (sensors.Length > 1)
			{
				var readCoreSensors = new ParameterizedThreadStart(ReadCoreSensors);
				for (int i = 1; i < sensors.Length; i++)
				{
					sensors[i] = new CoreTemperatureSensor
					(
						this,
						new Thread
						(
							readCoreSensors,
							ThreadStackSize
						)
						{
							IsBackground = true,
							Name = string.Create(CultureInfo.InvariantCulture, $"Intel CPU #{processorNumber} Core #{i} - Metrics")
						},
						i - 1
					);
				}
			}
		}
		else
		{
			sensors = [];
		}
		_sensors = ImmutableCollectionsMarshal.AsImmutableArray(Unsafe.As<ISensor[]>(sensors));
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, IntelCpuDriver, ISensorsFeature>(this);
		foreach (var sensor in sensors)
		{
			sensor.Start();
		}
	}

	public override ValueTask DisposeAsync()
	{
		if (!Interlocked.Exchange(ref _isDisposed, true))
		{
			foreach (var sensor in _sensors)
			{
				Unsafe.As<Sensor>(sensor).SetEvent();
			}
			foreach (var sensor in _sensors)
			{
				Unsafe.As<Sensor>(sensor).Join();
			}
			_pawnIo?.Dispose();
		}
		return ValueTask.CompletedTask;
	}

	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref _isDisposed)) throw new ObjectDisposedException(GetType().FullName);
	}

	private void ReadPackageSensors(object? state)
		=> ReadPackageSensors(Unsafe.As<PackageTemperatureSensor>(state!));

	private void ReadPackageSensors(PackageTemperatureSensor sensor)
	{
		ProcessorAffinity.SetForCurrentThread(_packageInformation.GroupAffinities);
		while (true)
		{
			sensor!.WaitEvent();
			if (Volatile.Read(ref _isDisposed))
			{
				sensor.MarkDisposed();
				return;
			}
			sensor._readValueTaskSource!.SetResult((short)(_tccActivationTemperature - (ReadMsr(_pawnIo!, Ia32PackageThermStatus) >> 16) & 0x7F));
			sensor.ResetEvent();
		}
	}

	private void ReadCoreSensors(object? state)
		=> ReadCoreSensors(Unsafe.As<CoreTemperatureSensor>(state!));

	private void ReadCoreSensors(CoreTemperatureSensor sensor)
	{
		SetAffinityForCore(sensor.CoreIndex);
		while (true)
		{
			sensor!.WaitEvent();
			if (Volatile.Read(ref _isDisposed))
			{
				sensor.MarkDisposed();
				return;
			}
			sensor._readValueTaskSource!.SetResult((short)(_tccActivationTemperature - (ReadMsr(_pawnIo!, Ia32ThermStatus) >> 16) & 0x7F));
			sensor.ResetEvent();
		}
	}

	private void SetAffinityForCore(int coreIndex)
	{
		var core = _packageInformation.Cores[coreIndex];
		ProcessorAffinity.SetForCurrentThread((nuint)core.GroupAffinity.Mask, core.GroupAffinity.Group);
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Processor;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => _sensors;

	private abstract class Sensor
	{
		private readonly IntelCpuDriver _driver;
		private readonly ManualResetEvent _manualResetEvent;
		internal readonly Thread _thread;

		protected Sensor(IntelCpuDriver driver, Thread thread)
		{
			_driver = driver;
			_manualResetEvent = new(false);
			_thread = thread;
		}

		protected IntelCpuDriver Driver => _driver;

		internal virtual void MarkDisposed() => _manualResetEvent.Dispose();
		internal void Start() => _thread.Start(this);
		internal void Join() => _thread.Join();
		internal void WaitEvent() => _manualResetEvent.WaitOne();
		internal void SetEvent() => _manualResetEvent.Set();
		internal void ResetEvent() => _manualResetEvent.Reset();
	}

	private abstract class Sensor<T> : Sensor
		where T : unmanaged
	{
		internal SensorReadValueTaskSource<T>? _readValueTaskSource;

		protected Sensor(IntelCpuDriver driver, Thread thread) : base(driver, thread)
		{
			_readValueTaskSource = new();
		}

		internal sealed override void MarkDisposed()
		{
			Interlocked.Exchange(ref _readValueTaskSource, null)?.SetDisposed();
			base.MarkDisposed();
		}

		// NB: This will break if the method is called more than once at a time.
		protected ValueTask<T> GetValueAsync(CancellationToken cancellationToken)
		{
			Driver.ThrowIfDisposed();
			SetEvent();
			if (_readValueTaskSource is not { } vts) throw new ObjectDisposedException(GetType().FullName);
			return vts.AsValueTask();
		}
	}

	private sealed class PackageTemperatureSensor : Sensor<short>, IPolledSensor<short>
	{
		public PackageTemperatureSensor(IntelCpuDriver driver, Thread thread) : base(driver, thread)
		{
		}

		short? ISensor<short>.ScaleMinimumValue => (short)(Driver._tccActivationTemperature - 0x7F);
		short? ISensor<short>.ScaleMaximumValue => (short)Driver._tccActivationTemperature;
		Guid ISensor.SensorId => ProcessorPackageTemperatureSensorId;
		SensorUnit ISensor.Unit => SensorUnit.Celsius;

		ValueTask<short> IPolledSensor<short>.GetValueAsync(CancellationToken cancellationToken) => GetValueAsync(cancellationToken);
	}

	private sealed class CoreTemperatureSensor : Sensor<short>, IPolledSensor<short>
	{
		public int CoreIndex { get; }

		public CoreTemperatureSensor(IntelCpuDriver driver, Thread thread, int coreIndex) : base(driver, thread)
		{
			if (coreIndex > ProcessorCoreTemperatureSensorIds.Count) throw new PlatformNotSupportedException();
			CoreIndex = coreIndex;
		}

		short? ISensor<short>.ScaleMinimumValue => (short)(Driver._tccActivationTemperature - 0x7F);
		short? ISensor<short>.ScaleMaximumValue => (short)Driver._tccActivationTemperature;
		Guid ISensor.SensorId => ProcessorCoreTemperatureSensorIds[CoreIndex];
		SensorUnit ISensor.Unit => SensorUnit.Celsius;

		ValueTask<short> IPolledSensor<short>.GetValueAsync(CancellationToken cancellationToken) => GetValueAsync(cancellationToken);
	}

	private sealed class SensorReadValueTaskSource<T> : IValueTaskSource<T>
		where T : unmanaged
	{
		private ManualResetValueTaskSourceCore<T> _core = new() { RunContinuationsAsynchronously = true };

		public void SetResult(T result) => _core.SetResult(result);
		public void SetDisposed() => _core.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(IntelCpuDriver).FullName)));

		public T GetResult(short token)
		{
			var result = _core.GetResult(token);
			_core.Reset();
			return result;
		}

		public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

		public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
			=> _core.OnCompleted(continuation, state, token, flags);

		public ValueTask<T> AsValueTask() => new(this, _core.Version);
	}
}
