namespace Exo;

/// <summary>Defines an exclusion category for object factories.</summary>
/// <remarks>
/// <para>
/// Two different factories belonging to the same category can not be run in parallel, but a single factory can be executed in parallel with itself.
/// In addition to this, factory methods also cannot execute simultaneously with the disposing of an object produced within that category.
/// </para>
/// <para>
/// This is expected to be a relatively niche feature, but it is critical in guaranteeing the integrity of multi-factory components creation and disposal.
/// As the orchestrator is not knowledgeable about the inner workings of components and their factories, this provides a safe way to manage these more complex objects.
/// </para>
/// <para>
/// The exclusion category is represented as a type, as it is the simplest cheapest way to provide relatively unique categories with no risk of collision.
/// While the type used as a category is expected to very often be that of the component itself, the category can be absolutely any type.
/// The provided type is only used as a key and will bear no particular meaning in itself.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExclusionCategoryAttribute : Attribute
{
	/// <summary>The exclusion category.</summary>
	public Type Category { get; }

	/// <summary>Initializes a new instance of the class <see cref="ExclusionCategoryAttribute"/>.</summary>
	/// <param name="category">The type used as the exclusion category.</param>
	public ExclusionCategoryAttribute(Type category) => Category = category;
}
