using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Sensors;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Intel.Cpu;

public class IntelCpuDriver : Driver, IDeviceDriver<ISensorDeviceFeature>, ISensorsFeature
{
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

			t.Item1.TrySetResult
			(
				new(t.Item3, new IntelCpuDriver(t.Item2, ReadBrandString(), t.Item4))
			);
		}
		catch (Exception ex)
		{
			t.Item1.TrySetException(ex);
		}
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

	private readonly ImmutableArray<ISensor> _sensors;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;

	public IntelCpuDriver
	(
		ILogger<IntelCpuDriver> logger,
		string brandString,
		int processorIndex
	) : base(brandString, new("IntelX86", string.Create(CultureInfo.InvariantCulture, $"{processorIndex}:{brandString}"), brandString, null))
	{
		_sensors = [];
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, IntelCpuDriver, ISensorsFeature>(this);
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Processor;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => _sensors;
}

public enum ProcessorModel
{
}

public enum ProcessorFamily
{
}
