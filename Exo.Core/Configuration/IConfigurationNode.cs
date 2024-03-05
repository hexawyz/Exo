namespace Exo.Configuration;

/// <summary>Defines a configuration node.</summary>
/// <remarks>
/// Configuration nodes can contain configuration containers, some of which are also themselves configuration nodes.
/// </remarks>
public interface IConfigurationNode
{
	/// <summary>Gets a configuration container to store values and child containers.</summary>
	/// <param name="containerName"></param>
	/// <returns></returns>
	IConfigurationContainer GetContainer(string containerName);

	/// <summary>Gets a configuration container used to store keyed configuration.</summary>
	/// <remarks>If needed, specific keys can be opened as their own containers and define custom child containers.</remarks>
	/// <typeparam name="TKey">The type of configuration key.</typeparam>
	/// <param name="containerName"></param>
	/// <param name="nameSerializer"></param>
	/// <returns></returns>
	IConfigurationContainer<TKey> GetContainer<TKey>(string containerName, INameSerializer<TKey> nameSerializer);
}
