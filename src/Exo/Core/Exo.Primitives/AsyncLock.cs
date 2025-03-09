using System.Runtime.CompilerServices;

namespace Exo;

/// <summary>Provides the asynchronous equivalent to a lock.</summary>
/// <remarks>
/// <para>This class does not provide reentrancy. It allows releasing the acquired lock on any thread or execution flow.</para>
/// <para>
/// While this class allows cancellation of the lock acquisition, users of this class are advised to not base their cancellation on short periods of real world time.
/// Doing so could lead to severe performance degradations, up to producing the effect of a deadlock for a somewhat long period of time.
/// </para>
/// </remarks>
// NB: It would be possible to make this into a non-readonly struct for internal class usage, but in addition to being unsafe, it would come with restrictions. (No dispose semantic)
public sealed class AsyncLock
{
	public readonly struct Registration : IDisposable
	{
		private readonly AsyncLock _owner;
		// NB: It could be possible to split this into and provide useful metadata here such as: Was there contention at the time the lock was acquired ?
		// But it is uncertain whether there is value in doing this, so let's not for now.
		// As to why the version is native-sized: The data will be aligned anyway so we may as well use that until we have other uses for the space.
		// The more version bits we have, the less the likelihood of an invalid successful release is.
		// It would already be quite low with only 16-bits, so we could reduce to that if needed.
		private readonly nuint _version;

		internal Registration(AsyncLock owner, nuint version)
		{
			_owner = owner;
			_version = version;
		}

		public readonly void Dispose()
		{
			_owner.Release(_version);
		}
	}

	// TODO: Investigate using IValueTaskSource<>.
	private class QueueTaskCompletionSource : TaskCompletionSource<Registration>
	{
		public QueueTaskCompletionSource(CancellationToken cancellationToken)
		{
			if (cancellationToken.CanBeCanceled)
			{
				_cancellationTokenRegistration = cancellationToken.UnsafeRegister(this);
			}
		}

		public QueueTaskCompletionSource? Next;
		private readonly CancellationTokenRegistration _cancellationTokenRegistration;

		public void UnregisterCancellationRegistration() => _cancellationTokenRegistration.Unregister();
	}

	private static readonly object LockSentinel = new();

	// This state can be one of:
	// - null: If the lock is not taken
	// - LockSentinel: If the lock is taken and no one is waiting
	// - QueueTaskCompletionSource: If the lock is taken and there is at least one waiter
	// We will be queuing items at the front because if makes the algorithm simpler for now, but it puts the cost of browsing the waiting list to the lock owner.
	// Doing this in the other direction would also be possible by putting a sentinel value at the end of a consumed queue, but it would make the code less trivial.
	// It could still have some advantages, but in the end the CPU cost has to be paid somewhere. Let's study this later.
	private object? _state;
	// The version is used to prevent double free. In that sense it is not strictly necessary, but since this class is made public, it is better to actively fight against bad uses.
	// Apart from the extra fields here and in the registration struct, it is not very expensive to implement, as it will only be accessed from within the assumed lock.
	// i.e. The only time where we would access it outside the real lock is when a double-release in done. In that case, we would read an updated value and just throw.
	private nuint _version;

	public ValueTask<Registration> WaitAsync(CancellationToken cancellationToken)
	{
		var oldState = Interlocked.CompareExchange(ref _state, LockSentinel, null);

		return oldState is null ? new(new Registration(this, _version)) : WaitSlowAsync(oldState, cancellationToken);
	}

	private ValueTask<Registration> WaitSlowAsync(object? oldState, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled<Registration>(cancellationToken);

		var tcs = new QueueTaskCompletionSource(cancellationToken);
		object? newState;

		while (true)
		{
			if (oldState is null)
			{
				newState = LockSentinel;
			}
			else
			{
				tcs.Next = Unsafe.As<QueueTaskCompletionSource?>(!ReferenceEquals(oldState, LockSentinel) ? oldState : null);
				newState = tcs;
			}
			if (oldState == (oldState = Interlocked.CompareExchange(ref _state, newState, oldState)))
			{
				if (ReferenceEquals(newState, LockSentinel)) return new(new Registration(this, _version));
				else return new(tcs.Task);
			}
		}
	}

	private void Release(nuint version)
	{
		// Simultaneously try to increment the version while checking that the version is actually correct.
		// We can't avoid the interlocked operation here, but at least it makes this code very safe at a reduced cost.
		// NB: Unless there is a severe bug/misuse in the caller (which *will* raise the exception), this operation will never be contended.
		// This code still be affected by false sharing, but there is nothing good we can do about it for now.
		// NB2: In an ideal/future world, we could probably try increasing the version simultaneously with releasing a non-waited lock, by relying on CMPXCHG16B.
		// It would make the code more efficient for the uncontended path. (Which should hopefully be the major code path)
		if (version != (version = Interlocked.CompareExchange(ref _version, version + 1, version))) throw new InvalidOperationException("The lock was already released.");

		// Try to release an uncontented lock.
		var oldState = Interlocked.CompareExchange(ref _state, null, LockSentinel);

		// Uncontended lock code path should be quick. We test for this first.
		if (ReferenceEquals(oldState, LockSentinel)) return;

		// Contended code path can be a bit slower. We delegate it to another method in order to keep this one relatively small.
		Release(oldState, version + 1);
	}

	private void Release(object? state, nuint version)
	{
		// This should never happen, but we need to be sure.
		if (state is null) throw new InvalidOperationException("The lock was in an invalid state.");

		QueueTaskCompletionSource head = Unsafe.As<QueueTaskCompletionSource>(state);

		while (true)
		{
			QueueTaskCompletionSource? current = head.Next;

			// Optimistically try to swap out the first item if it is alone.
			// This will be mostly helpful for the case of reduced contention, which is a least ideal use case than the uncontended one, but still one we want to favor over the contended case.
			if (current is null)
			{
				// Downgrade the stack from a waiting list to lock acquired, before we actually transfer the lock ownership to the waiter.
				// NB: The state can never be updated to something else than QueueTaskCompletionSource outside of this method.
				if (ReferenceEquals(head, head = Unsafe.As<QueueTaskCompletionSource>(Interlocked.CompareExchange(ref _state, LockSentinel, head)!)))
				{
					// If we were able to successfully complete the waiter, then we're done here. The waiter now has ownership of the lock.
					head.UnregisterCancellationRegistration();
					if (head.TrySetResult(new(this, version))) return;
					// Otherwise, the task has been cancelled.
					// Not a huge deal, we can still try to do a simple release of the lock…
					state = Interlocked.CompareExchange(ref _state, null, LockSentinel);
					if (ReferenceEquals(state, LockSentinel)) return;
					// If releasing the lock failed, then a new waiter has registered.
					head = Unsafe.As<QueueTaskCompletionSource>(state)!;
				}
				// Try all of this again with the new head waiter.
				continue;
			}

			// We're now in the case where there is at least two waiters in the waiting list.

			// We'll try to cleanup the cancelled waiters, but for performance reasons, we don't want to swap the _state itself here.
			// This allows cleanup up a consecutive sequence of cancelled waiters at once.
			// Because of this, we assume the (presumed) head of the waiting list is non cancelled, even if it is. (It will be handled by the previous code if necessary)
			// NB: We could face some severe contention if waiters are registered and cancelled faster than this code can do its job. Hopefully this will not happen.
			// That's one other reason to avoid basing cancellations on relatively short periods of real time.
			// NB2: The cancellation cleanup could likely be done concurrently, so we could investigate also doing this work in the contended WaitAsync case.
			QueueTaskCompletionSource previousNonCancelled = head;

			// The actual exit condition of this loop is when we reach the last waiter in the list. Which is guaranteed to happen at some point, as the list only grows from the head.
			while (true)
			{
				// The Next property can only be updated in this method, so we can be confident that null always will be null.
				if (current.Next is null)
				{
					// Whether the current waiter ends up being cancelled or not, we want to remove it from the waiting list.
					// NB: This also has the effect of removing all (known) previous cancelled waiters.
					previousNonCancelled.Next = null;
					// Trying to complete the last waiter (the first registered) allows us to simultaneously check for cancellation.
					if (current.TrySetResult(new(this, version))) return;
					// If the current waiter was cancelled, we have to try all of this again.
					// If head is the only remaining waiter that we know of, we will optimistically not update head and let it go through the (assumed) single-waiter case in the outer loop.
					if (ReferenceEquals(previousNonCancelled, head))
					{
						break;
					}
					else
					{
						// Once again, we should be guaranteed to get a QueueTaskCompletionSource if we reach this point.
						// Reaching this code means we were not able to successfully swap out a QueueTaskCompletionSource for something else.
						head = Unsafe.As<QueueTaskCompletionSource>(Volatile.Read(ref _state))!;
						// We already know that there is more than one item in the queue now, so we can skip the single-waiter case from the outer loop.
						previousNonCancelled = head;
						current = head.Next!;
						continue;
					}
				}

				// Skip cancelled waiters, and allow them to be cleaned once we reach the last in the waiting list.
				if (!current.Task.IsCanceled)
				{
					// Remove all (known) previous cancelled waiters from the list.
					if (current != previousNonCancelled.Next)
					{
						previousNonCancelled.Next = current;
					}
					previousNonCancelled = current;
				}

				current = current.Next;
			}
		}
	}
}
