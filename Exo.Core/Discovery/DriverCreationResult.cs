using System.Collections.Immutable;

namespace Exo.Discovery;

public sealed class DriverCreationResult<TKey> : ComponentCreationResult<TKey, Driver>
	where TKey : notnull, IEquatable<TKey>
{
	public DriverCreationResult(ImmutableArray<TKey> registrationKeys, Driver driver, IAsyncDisposable? disposableResult)
		: base(registrationKeys, driver, disposableResult)
	{
	}
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
