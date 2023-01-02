namespace DeviceTools.Core.Tests;

public class PropertiesTest
{
	public static IEnumerable<(string Name, Property Property)> AllProperties =>
		typeof(Property)
			.Assembly
			.GetTypes()
			.Where(t => t.Namespace == typeof(Properties.System).Namespace && t.IsAbstract && t.IsSealed)
			.SelectMany
			(
				t => t.GetFields().Where(f => typeof(Property).IsAssignableFrom(f.FieldType)),
				(t, f) =>
				(
					Name: t.FullName!.Substring(t.Namespace!.Length + 1).Replace("+", ".") + "." + f.Name,
					Property: (Property)f.GetValue(null)!
				)
			);

	public static IEnumerable<object[]> AllPropertiesData =>
		AllProperties
			.Select(t => new object[] { t.Name, t.Property })
			.ToArray();

	[Theory]
	[MemberData(nameof(AllPropertiesData))]
	public void CanonicalNameShouldMatchTypeName(string name, Property property)
	{
		Assert.Equal(property.Key.GetName(), name);
	}

	// Not really a test, but useful to generate an export of all properties.
#if false
	[Fact]
	public void ExportAllProperties()
	{
		using var file = new StreamWriter("properties.csv");

		file.WriteLine("Name,CategoryId,PropertyIndex,Type,IsCanonical");

		foreach (var (name, property) in AllProperties)
		{
			file.Write(name);
			file.Write(",");
			file.Write(property.Key.CategoryId.ToString("B"));
			file.Write(",");
			file.Write(property.Key.PropertyId.ToString());
			file.Write(",");
			file.Write(property.GetType().Name.AsSpan()[0..^8]);
			file.Write(",");
			file.Write(property.Key.GetName() == name);
			file.WriteLine();
		}
	}
#endif
}
