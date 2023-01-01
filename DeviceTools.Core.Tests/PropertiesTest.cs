using System.Reflection;

namespace DeviceTools.Core.Tests;

public class PropertiesTest
{
	public static IEnumerable<object[]> AllPropertiesData =>
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
			)
			.Select(t => new object[] { t.Name, t.Property })
			.ToArray();

	[Theory]
	[MemberData(nameof(AllPropertiesData))]
	public void CanonicalNameShouldMatchTypeName(string name, Property property)
	{
		Assert.Equal(property.Key.GetName(), name);
	}
}
