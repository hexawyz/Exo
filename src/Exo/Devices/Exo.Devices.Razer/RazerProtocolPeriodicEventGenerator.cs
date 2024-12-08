using System.Runtime.CompilerServices;

namespace Exo.Devices.Razer;

// This manages a single timer to be shared for multiple devices connected to a dongle.
// NB: This could probably be shared across all devices if we are sure that the period can be the same.
// NB2: Could probably nest this class inside RazerDeviceDriver and get rid of IRazerPeriodicEventHandler.
internal sealed class RazerProtocolPeriodicEventGenerator : IDisposable
{
	// Holds a shared instance delegate for timers.
	private sealed class Callback
	{
		public static readonly TimerCallback Instance = new(new Callback().Handle);

		private Callback() { }

		private void Handle(object? state)
			=> Unsafe.As<RazerProtocolPeriodicEventGenerator>(state!).OnTimerTick();
	}

	private readonly Timer _timer;
	private readonly int _period;
	private readonly Lock _lock;

	// This can hold different values:
	//  - The value null if there are no handlers. In that case, the timer will be disabled.
	//  - An instance of IRazerPeriodicEventHandler if there is a single handler.
	//  - An array of IRazerPeriodicEventHandler if there is more than one handler.
	private object? _handlers;

	public RazerProtocolPeriodicEventGenerator(int period)
	{
		_period = period;
		_timer = new Timer(Callback.Instance, this, Timeout.Infinite, Timeout.Infinite);
		_lock = new();
	}

	public void Register(IRazerPeriodicEventHandler handler)
	{
		ArgumentNullException.ThrowIfNull(handler);

		lock (_lock)
		{
			var handlers = _handlers;

			if (handlers is null)
			{
				_handlers = handler;
				_timer.Change(_period, _period);
				return;
			}
			else if (ReferenceEquals(handlers, handler))
			{
				return;
			}
			else if (handlers is IRazerPeriodicEventHandler[] array)
			{
				if (Array.IndexOf(array, handler) >= 0) return;

				Array.Resize(ref array, array.Length + 1);

				array[^1] = handler;

				_handlers = array;
			}
			else
			{
				_handlers = new[] { Unsafe.As<IRazerPeriodicEventHandler>(handlers), handler };
			}
		}
	}

	// If this was a public API, we would need to make sure that events are always unregistered by returning a finalizable/disposable object in Register.
	// We'll trust that the calling code is correctly releasing its registrations for now, but keep that option open.
	public void Unregister(IRazerPeriodicEventHandler handler)
	{
		ArgumentNullException.ThrowIfNull(handler);

		lock (_lock)
		{
			var handlers = _handlers;

			if (handlers is null)
			{
				return;
			}
			else if (ReferenceEquals(handlers, handler))
			{
				_handlers = null;
				_timer.Change(Timeout.Infinite, Timeout.Infinite);
				return;
			}
			else if (handlers is IRazerPeriodicEventHandler[] array)
			{
				int index = Array.IndexOf(array, handler);

				if (index < 0) return;

				if (array.Length == 2)
				{
					_handlers = array[index ^ 1];
				}
				else
				{
					var newArray = new IRazerPeriodicEventHandler[array.Length - 1];

					Array.Copy(array, 0, newArray, 0, index);
					Array.Copy(array, index + 1, newArray, index, newArray.Length - index);

					_handlers = newArray;
				}
			}
		}
	}

	private void OnTimerTick()
	{
		// Executing all events in the lock could be risky, but it makes things simpler.
		// Because we control the code of the handlers, we should be able to guarantee that they are not executing for too long.
		lock (_lock)
		{
			var handlers = _handlers;

			if (handlers is null) return;

			// The type-check for array should be almost free compared to the interface type.
			// That's why we do this check and not the other one.
			// If the object is not an array, we know it can only be an instance of IRazerPeriodicEventHandler, and as such we can avoid the type-check with Unsafe.As.
			if (handlers is IRazerPeriodicEventHandler[] array)
			{
				foreach (var handler in array)
				{
					try
					{
						handler.HandlePeriodicEvent();
					}
					catch (Exception ex)
					{
						// TODO: Log
					}
				}
			}
			else
			{
				try
				{
					Unsafe.As<IRazerPeriodicEventHandler>(handlers).HandlePeriodicEvent();
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
	}

	public void Dispose()
	{
		_timer.Dispose();
	}
}
