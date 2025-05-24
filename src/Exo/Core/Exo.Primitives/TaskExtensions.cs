using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Exo;

public static class TaskExtensions
{
	public readonly struct CancellableTaskAwaitable
	{
		private readonly CancellableTaskAwaiter _awaiter;

		internal CancellableTaskAwaitable(Task task)
		{
			_awaiter = new(task);
		}

		public CancellableTaskAwaiter GetAwaiter() => _awaiter;

		public readonly struct CancellableTaskAwaiter : ICriticalNotifyCompletion
		{
			private readonly ConfiguredTaskAwaitable.ConfiguredTaskAwaiter _configuredTaskAwaiter;

			internal CancellableTaskAwaiter(Task task)
			{
				_configuredTaskAwaiter = task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing).GetAwaiter();
			}

			public bool IsCompleted => _configuredTaskAwaiter.IsCompleted;

			public void OnCompleted(Action continuation) => _configuredTaskAwaiter.OnCompleted(continuation);

			public void UnsafeOnCompleted(Action continuation) => _configuredTaskAwaiter.UnsafeOnCompleted(continuation);

			[StackTraceHidden]
			public bool GetResult()
			{
				_configuredTaskAwaiter.GetResult();
				var task = Unsafe.As<ConfiguredTaskAwaitable.ConfiguredTaskAwaiter, Task>(ref Unsafe.AsRef(in _configuredTaskAwaiter));
				var status = task.Status;
				if (status == TaskStatus.RanToCompletion) return true;
				else if (status == TaskStatus.Canceled) return false;
				else
				{
					task.Wait();
				}
				return true;
			}
		}
	}

	public readonly struct ConfiguredCancellableTaskAwaitable
	{
		private readonly ConfiguredCancellableTaskAwaiter _awaiter;

		internal ConfiguredCancellableTaskAwaitable(Task task, ConfigureAwaitOptions configureAwaitOptions)
		{
			_awaiter = new(task, configureAwaitOptions);
		}

		public ConfiguredCancellableTaskAwaiter GetAwaiter() => _awaiter;

		public readonly struct ConfiguredCancellableTaskAwaiter : ICriticalNotifyCompletion
		{
			private readonly ConfiguredTaskAwaitable.ConfiguredTaskAwaiter _configuredTaskAwaiter;
			private readonly bool _shouldSuppressThrowing;

			internal ConfiguredCancellableTaskAwaiter(Task task, ConfigureAwaitOptions configureAwaitOptions)
			{
				_configuredTaskAwaiter = task.ConfigureAwait(configureAwaitOptions | ConfigureAwaitOptions.SuppressThrowing).GetAwaiter();
				_shouldSuppressThrowing = (configureAwaitOptions & ConfigureAwaitOptions.SuppressThrowing) != 0;
			}

			public bool IsCompleted => _configuredTaskAwaiter.IsCompleted;

			public void OnCompleted(Action continuation) => _configuredTaskAwaiter.OnCompleted(continuation);

			public void UnsafeOnCompleted(Action continuation) => _configuredTaskAwaiter.UnsafeOnCompleted(continuation);

			[StackTraceHidden]
			public bool GetResult()
			{
				_configuredTaskAwaiter.GetResult();
				var task = Unsafe.As<ConfiguredTaskAwaitable.ConfiguredTaskAwaiter, Task>(ref Unsafe.AsRef(in _configuredTaskAwaiter));
				var status = task.Status;
				if (status == TaskStatus.RanToCompletion) return true;
				else if (status == TaskStatus.Canceled) return false;
				else if (!_shouldSuppressThrowing)
				{
					task.Wait();
				}
				return true;
			}
		}
	}

	public static CancellableTaskAwaitable ConfigureNicer(this Task task)
		=> new(task);

	public static ConfiguredCancellableTaskAwaitable ConfigureNicer(this Task task, ConfigureAwaitOptions configureAwaitOptions)
		=> new(task, configureAwaitOptions);
}
