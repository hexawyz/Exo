using System.Collections.Concurrent;
using Exo.Cooling;

namespace Exo.Service;

internal partial class CoolingService
{
	private abstract class CoolerChange
	{
		// The execute method will only be called once when the object is dequeued.
		// This means that the Execute method can include mechanisms that will pool the object for reuse if necessary.
		// That way, we will limit the impact of updates originating from software control curves, which is especially important, as those would occur often.
		public abstract void Execute();

		private static AutomaticCoolerChange? _pooledAutomaticChange = new();
		private static readonly ConcurrentStack<ManualCoolerChange> ManualChangePool = CreateManualPool();

		private static ConcurrentStack<ManualCoolerChange> CreateManualPool()
		{
			var pool = new ConcurrentStack<ManualCoolerChange>();
			pool.Push(new());
			pool.Push(new());
			return pool;
		}

		public static CoolerChange CreateAutomatic(IAutomaticCooler cooler)
		{
			if (Interlocked.Exchange(ref _pooledAutomaticChange, null) is not { } change) change = new();
			change.Prepare(cooler);
			return change;
		}

		public static CoolerChange CreateManual(IManualCooler cooler, byte power)
		{
			if (!ManualChangePool.TryPop(out var change)) change = new();
			change.Prepare(cooler, power);
			return change;
		}

		private sealed class AutomaticCoolerChange : CoolerChange
		{
			private IAutomaticCooler? _cooler;

			public void Prepare(IAutomaticCooler cooler) => _cooler = cooler;

			public override void Execute()
			{
				try
				{
					_cooler!.SwitchToAutomaticCooling();
				}
				finally
				{
					_cooler = null;
					Interlocked.CompareExchange(ref _pooledAutomaticChange, this, null);
				}
			}
		}

		private sealed class ManualCoolerChange : CoolerChange
		{
			private IManualCooler? _cooler;
			private byte _power;

			public void Prepare(IManualCooler cooler, byte power)
			{
				_cooler = cooler;
				_power = power;
			}

			public override void Execute()
			{
				try
				{
					_cooler!.SetPower(_power);
				}
				finally
				{
					_cooler = null;
					ManualChangePool.Push(this);
				}
			}
		}
	}
}
