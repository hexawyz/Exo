using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks.Sources;
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
		int processorIndex
	)
	{
		var tcs = new TaskCompletionSource<DriverCreationResult<SystemCpuDeviceKey>?>(TaskCreationOptions.RunContinuationsAsynchronously);
		var thread = new Thread(InitializeCpu) { IsBackground = true };
		thread.Start(Tuple.Create(tcs, logger, keys, processorIndex));
		return tcs.Task;
	}

	private static void InitializeCpu(object? state)
	{
		ArgumentNullException.ThrowIfNull(state);

		var t = (Tuple<TaskCompletionSource<DriverCreationResult<SystemCpuDeviceKey>?>, ILogger<IntelCpuDriver>, ImmutableArray<SystemCpuDeviceKey>, int>)state;

		try
		{
			// TODO: Switch to the correct CPU

			int eax, ebx, ecx, edx;

			(eax, ebx, ecx, edx) = X86Base.CpuId(unchecked((int)0x80000000U), 0);

			if ((eax & int.MinValue) == 0 || eax - unchecked((int)0x80000004U) < 4) throw new PlatformNotSupportedException("Brand String is not supported.");

			// TODO: Interpret processor information stuff if necessary. (For now, the brand name seems to be enough?)
			//(eax, ebx, ecx, edx) = X86Base.CpuId(1, 0);

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
				new(t.Item3, new IntelCpuDriver(t.Item2, pawnIo, tccActivationTemperature, ReadBrandString(), t.Item4))
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
	private readonly uint _tccActivationTemperature;
	private readonly ManualResetEvent? _packageReadRequestManualResetEvent;
	private SensorReadValueTaskSource<short>? _packageTemperatureReadValueTaskSource;
	private readonly ImmutableArray<ISensor> _sensors;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly Thread? _packageReadThread;
	private bool _isDisposed;

	private IntelCpuDriver
	(
		ILogger<IntelCpuDriver> logger,
		PawnIo? pawnIo,
		byte tccActivationTemperature,
		string brandString,
		int processorIndex
	) : base(brandString, new("IntelX86", string.Create(CultureInfo.InvariantCulture, $"{processorIndex}:{brandString}"), brandString, null))
	{
		_pawnIo = pawnIo;
		_tccActivationTemperature = tccActivationTemperature;
		if (_pawnIo is not null && tccActivationTemperature > 0)
		{
			_packageReadRequestManualResetEvent = new(false);
			_packageTemperatureReadValueTaskSource = new();
			_sensors = [new PackageTemperatureSensor(this)];
			_packageReadThread = new Thread(new ThreadStart(ReadPackageTemperatures)) { IsBackground = true };
		}
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, IntelCpuDriver, ISensorsFeature>(this);
		_packageReadThread?.Start();
	}

	public override ValueTask DisposeAsync()
	{
		if (!Interlocked.Exchange(ref _isDisposed, true))
		{
			_packageReadRequestManualResetEvent?.Set();
			_packageReadThread?.Join();
			_pawnIo?.Dispose();
		}
		return ValueTask.CompletedTask;
	}

	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref _isDisposed)) throw new ObjectDisposedException(GetType().FullName);
	}

	private void ReadPackageTemperatures()
	{
		while (true)
		{
			_packageReadRequestManualResetEvent!.WaitOne();
			if (Volatile.Read(ref _isDisposed))
			{
				Interlocked.Exchange(ref _packageTemperatureReadValueTaskSource, null)?.SetDisposed();
				return;
			}
			_packageTemperatureReadValueTaskSource!.SetResult((short)(_tccActivationTemperature - (ReadMsr(_pawnIo!, Ia32PackageThermStatus) >> 16) & 0x7F));
			_packageReadRequestManualResetEvent.Reset();
		}
	}

	// NB: This will break if the method is called more than once at a time.
	private ValueTask<short> ReadPackageTemperatureAsync()
	{
		ThrowIfDisposed();
		_packageReadRequestManualResetEvent!.Set();
		if (_packageTemperatureReadValueTaskSource is not { } vts) throw new ObjectDisposedException(GetType().FullName);
		return vts.AsValueTask();
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Processor;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => _sensors;

	private sealed class PackageTemperatureSensor : IPolledSensor<short>
	{
		private readonly IntelCpuDriver _driver;

		public PackageTemperatureSensor(IntelCpuDriver driver)
		{
			_driver = driver;
		}

		short? ISensor<short>.ScaleMinimumValue => (short)(_driver._tccActivationTemperature - 0x7F);
		short? ISensor<short>.ScaleMaximumValue => (short)_driver._tccActivationTemperature;
		Guid ISensor.SensorId => ProcessorPackageTemperatureSensorId;
		SensorUnit ISensor.Unit => SensorUnit.Celsius;

		ValueTask<short> IPolledSensor<short>.GetValueAsync(CancellationToken cancellationToken) => _driver.ReadPackageTemperatureAsync();
	}

	private sealed class SensorReadValueTaskSource<T> : IValueTaskSource<T>
		where T : unmanaged
	{
		private ManualResetValueTaskSourceCore<T> _core;

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
