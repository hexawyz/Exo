namespace Exo.Configuration;

public interface IConfigurationContainer : IConfigurationNode
{
	ValueTask<ConfigurationResult<TValue>> ReadValueAsync<TValue>(CancellationToken cancellationToken);
	ValueTask WriteValueAsync<TValue>(TValue value, CancellationToken cancellationToken) where TValue : notnull;
	ValueTask DeleteValueAsync<TValue>();
	ValueTask DeleteAllValuesAsync();
}

public interface IConfigurationContainer<TKey>
{
	ValueTask<TKey[]> GetKeysAsync(CancellationToken cancellationToken);
	ValueTask<ConfigurationResult<TValue>> ReadValueAsync<TValue>(TKey key, CancellationToken cancellationToken);
	ValueTask WriteValueAsync<TValue>(TKey key, TValue value, CancellationToken cancellationToken) where TValue : notnull;
	ValueTask DeleteValueAsync<TValue>(TKey key);
	ValueTask DeleteValuesAsync(TKey key);

	/// <summary>Gets a key-scoped configuration container for the specified key.</summary>
	/// <remarks>
	/// <para>This allows returning a container that can be used both to fetch values .</para>
	/// <para>For best results, callers should keep the reference returned by this method instead of calling it multiple times for the same key.</para>
	/// </remarks>
	/// <param name="key"></param>
	/// <returns></returns>
	IConfigurationContainer GetContainer(TKey key);
	ValueTask DeleteAllContainersAsync();
}
