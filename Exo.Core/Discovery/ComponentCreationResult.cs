using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exo.Discovery;

public abstract class ComponentCreationResult : IAsyncDisposable
{
	/// <summary>Gets the registration keys associated with this result.</summary>
	/// <remarks>
	/// <para>
	/// This property must only return keys that are directly associated with the creation operation.
	/// This is especially important for component that can be returned multiple times.
	/// </para>
	/// <para>
	/// While component factories might be able to resolve all the keys on their own, the keys are used to properly manage the lifetime of components.
	/// As such, it is much better to let the various discovery engine call the factories one by one.
	/// </para>
	/// <para>The keys would generally be already passed in to the factory. However, some factories might need to make some adjustments to the keys.</para>
	/// </remarks>
	// TODO: Must provide a reusable mechanism to allow components to store their current live instances in a cache instead of managing the caching themselves.
	// This does not need to be complicated, but it would be better if the cache (likely in the form of IDictionary<,>) is managed by the discovery orchestrator.
	// The cache to retrieve could then probably be matched using an attribute, so that the caches are properly isolated.
	// TODO: Similarly, we want to allow as much parallelism as possible for component creation and disposal, so a mechanism to lock operations must be provided.
	// This should be defined in the form of an attribute on the factories, which would provide a category (likely a Type object) to be used for locking operations.
	// This lock category should then be used to prevent disposing components while a corresponding factory to instantiate components is being called.
	// Similarly, it could be used to prevent parallel calls to the factories, if needed. (It is easier for factories to protect themselves against this on their own)
	protected Array RegistrationKeys { get; }
	/// <summary>Gets the component produced.</summary>
	/// <remarks>
	/// A same components instance can be returned multiple times if the component is accessible from multiple discovery engines.
	/// In that case, a unique <see cref="DisposableResult"/> must be provided in order for the component to be notified when the result is disposed.
	/// The discovery host will take care of counting valid references to the component and dispose it last.
	/// </remarks>
	public IAsyncDisposable Component { get; }
	/// <summary>Gets a disposable object that can be used to dispose the result.</summary>
	/// <remarks>
	/// This property can be <see langword="null"/>.
	/// If a value is provided, its <see cref="IAsyncDisposable.DisposeAsync"/> method will be called when the lifetime of the result ends.
	/// Once all results associated with a component have been disposed, the <see cref="Component"/> instance will itself be disposed.
	/// Proper reference counting is handled by the discovery orchestrator.
	/// </remarks>
	public IAsyncDisposable? DisposableResult { get; }

	protected ComponentCreationResult(Array? registrationKeys, IAsyncDisposable component, IAsyncDisposable? disposableResult)
	{
		ArgumentNullException.ThrowIfNull(registrationKeys);
		RegistrationKeys = registrationKeys;
		Component = component;
		DisposableResult = disposableResult;
	}

	public abstract ValueTask DisposeAsync();
}

public abstract class ComponentCreationResult<TKey, TComponent> : ComponentCreationResult
	where TKey : notnull, IEquatable<TKey>
	where TComponent : class, IAsyncDisposable
{
	public new ImmutableArray<TKey> RegistrationKeys => ImmutableCollectionsMarshal.AsImmutableArray(Unsafe.As<TKey[]>(base.RegistrationKeys));
	public new TComponent Component => Unsafe.As<TComponent>(base.Component);

	public ComponentCreationResult(ImmutableArray<TKey> registrationKeys, TComponent component, IAsyncDisposable? disposableResult)
		: base(ImmutableCollectionsMarshal.AsArray(registrationKeys)!, component, disposableResult)
	{
	}

	public override ValueTask DisposeAsync() => Component.DisposeAsync();
}

//public abstract class ComponentRegistrationKeys : IEnumerable
//{
//	internal ComponentRegistrationKeys() { }

//	IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
//}

//public class ComponentRegistrationKeys<TKey> : ComponentRegistrationKeys, IReadOnlyList<TKey>
//	where TKey : notnull, IComponentRegistrationKey, IEquatable<TKey>
//{
//	public struct Enumerator : IEnumerator<TKey>
//	{
//		private readonly TKey[] _keys;
//		private int _index;

//		internal Enumerator(TKey[] keys)
//		{
//			_keys = keys;
//			_index = -1;
//		}

//		public readonly TKey Current => _keys[_index];

//		public bool MoveNext() => (uint)++_index < (uint)_keys.Length;
//		public void Reset() => _index = -1;

//		readonly object? IEnumerator.Current => Current;

//		public readonly void Dispose() { }
//	}

//	private readonly TKey[] _keys;

//	public ComponentRegistrationKeys(TKey key) : this([key]) { }

//	public ComponentRegistrationKeys(ImmutableArray<TKey> keys)
//	{
//		if (keys.IsDefault) throw new ArgumentNullException(nameof(keys));
//		if (keys.IsEmpty) throw new ArgumentException("No key was provided.");
//		_keys = ImmutableCollectionsMarshal.AsArray(keys)!;
//	}

//	public TKey this[int index] => _keys[index];

//	public int Count => _keys.Length;

//	public Enumerator GetEnumerator() => new(_keys);
//	IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => GetEnumerator();
//	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
//}

//public interface IComponentRegistrationKey
//{
//}
