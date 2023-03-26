using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Exo;

internal static class Optional
{
	internal static readonly object DisposedSentinel = new object();
}

/// <summary>Represents a disposable object that can optionally be used when creating an object.</summary>
/// <remarks>
/// <para>
/// The recommended usage is for constructors or factory methods that have optional dependencies.
/// Such constructors or factory methods can use <see cref="Optional{T}"/> to communicate their use with the caller.
/// If not used during object construction or later during object lifetime, it is strongly advised to dispose the instance of <see cref="Optional{T}"/> directly within the constructor.
/// Otherwise, the caller may need to keep the <see cref="Optional{T}"/> instance for the whole lifetime of the constructed object.
/// </para>
/// <para>
/// Object creation may require custom data. In order to provide an optional service,
/// one must create a derived class and implement the abstract <see cref="Optional{T}.CreateValue"/> method with the appropriate logic.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of disposable object created by this instance.</typeparam>
public abstract class Optional<T> : IDisposable
	where T : class, IDisposable
{
	// null if unallocated, DisposedSentinel if the object is disposed, or a valid instance of T otherwise.
	private object? _value;

	public bool IsValueAllocated => Volatile.Read(ref _value) is not null;

	protected Optional() { }

	public bool IsDisposed => ReferenceEquals(Volatile.Read(ref _value), Optional.DisposedSentinel);

	public void Dispose()
	{
		var value = Volatile.Read(ref _value);

		// If the value is unallocated, we need to acquire the lock because there could be a race between GetOrCreateValue() and Dispose().
		if (value is null)
		{
			lock (this)
			{
				value = _value;

				Volatile.Write(ref _value, Optional.DisposedSentinel);

				if (value is null || ReferenceEquals(value, Optional.DisposedSentinel)) return;
			}
		}
		else if (ReferenceEquals(value, Optional.DisposedSentinel))
		{
			return;
		}
		else
		{
			// If the value is already allocated, the reference can only ever be changed to the disposed status, so we don't need to acquire the lock.
			Volatile.Write(ref _value, Optional.DisposedSentinel);
		}

		// Once the instance has been marked as disposed, dispose the value.
		Unsafe.As<T>(value).Dispose();
	}

	public virtual T GetOrCreateValue()
	{
		if (Volatile.Read(ref _value) is not { } value)
		{
			lock (this)
			{
				if ((value = _value) is null)
				{
					Volatile.Write(ref _value, value = CreateValue());
				}
			}
		}

		return GetValue(value);
	}

	public T? GetValueOrDefault() => GetValue(Volatile.Read(ref _value));

	[return: NotNullIfNotNull(nameof(value))]
	private static T? GetValue(object? value)
		=> !ReferenceEquals(value, Optional.DisposedSentinel) ? Unsafe.As<T>(value) : throw new ObjectDisposedException(nameof(Optional));

	protected abstract T CreateValue();
}
