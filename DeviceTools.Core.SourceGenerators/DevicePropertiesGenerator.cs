using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DeviceTools.Core.SourceGenerators;

[Generator]
public class DevicePropertiesGenerator : ISourceGenerator
{
	public void Initialize(GeneratorInitializationContext context) { }

	public void Execute(GeneratorExecutionContext context)
	{
		if (!(context.AdditionalFiles.Where(at => Path.GetFileName(at.Path) == "properties.csv").SingleOrDefault()?.GetText() is SourceText text))
		{
			return;
		}

		var data = new Dictionary<Guid, List<(string Name, int PropertyIndex, string Type, bool IsCanonical)>>();

		var vendors = new List<(string Name, Guid categoryId, int PropertyIndex, string Type, bool IsCanonical)>();

		if (text.Lines.Count == 0 || text.Lines[0].ToString() != "Name,CategoryId,PropertyIndex,Type,IsCanonical")
		{
			context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("PG0001", "Invalid CSV file.", "Invalid CSV file.", "PropertyGenerator", DiagnosticSeverity.Error, true), null));
			return;
		}

		int propertyCount = 0;
		for (int i = 1; i < text.Lines.Count; i++)
		{
			var line = text.Lines[i];

			if (line.Span.Length == 0 && i + 1 == text.Lines.Count) break;

			var columns = line.ToString().Split(',');

			if (columns.Length != 5)
			{
				context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("PG0002", "Invalid line in CSV file.", "Invalid line in CSV file: '{0}'.", "PropertyGenerator", DiagnosticSeverity.Error, true), null, line));
				return;
			}

			string name = columns[0];
			var categoryId = Guid.Parse(columns[1]);
			int propertyIndex = int.Parse(columns[2]);
			string type = columns[3];
			bool isCanonical = bool.Parse(columns[4]);

			if (!data.TryGetValue(categoryId, out var list))
			{
				data.Add(categoryId, list = new());
			}

			list.Add((name, propertyIndex, type, isCanonical));
			propertyCount++;
		}

		var rootNamespace = new NamespaceInfo("System");
		var sb = new StringBuilder();

		sb.AppendLine("using System;");
		sb.AppendLine("using System.Runtime.CompilerServices;");
		sb.AppendLine("using DeviceTools.FilterExpressions;");
		sb.AppendLine();
		sb.AppendLine("namespace DeviceTools;");
		sb.AppendLine();
		sb.AppendLine("public static class Properties");
		sb.AppendLine("{");
		sb.AppendLine("\tprivate static readonly Property[] AllProperties = InitAllProperties();");
		sb.AppendLine();
		sb.AppendLine("\tprivate static Property[] InitAllProperties()");
		sb.AppendLine("\t{");
		sb.AppendLine($"\t\tvar properties = new Property[{propertyCount.ToString(CultureInfo.InvariantCulture)}];");
		sb.AppendLine("\t\tGuid guid;");

		int index = 0;
		foreach (var kvp in data)
		{
			var guid = kvp.Key;
			sb.AppendLine($"\t\tguid = new({guid.ToString("X").Replace("{", "").Replace("}", "")});");

			kvp.Value.Sort((x, y) => Comparer<int>.Default.Compare(x.PropertyIndex, y.PropertyIndex));

			foreach (var property in kvp.Value)
			{
				sb.AppendLine($"\t\tproperties[{index.ToString(CultureInfo.InvariantCulture)}] = new {property.Type}Property(guid, {property.PropertyIndex.ToString(CultureInfo.InvariantCulture)});");

				var parts = property.Name.Split('.');

				if (parts is null || parts[0] != "System")
				{
					context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("PG0003", "Property namespace not starting with System.", "Property namespace not starting with System : '{0}'.", "PropertyGenerator", DiagnosticSeverity.Error, true), null, property.Name));
					return;
				}

				var ns = rootNamespace;
				int count = parts.Length - 1;

				for (int i = 1; i < count; i++)
				{
					var name = parts[i];
					var ns2 = ns.Namespaces.Find(n => n.Name == name);
					if (ns2 is null)
					{
						ns.Namespaces.Add(ns2 = new(name));
					}
					ns = ns2;
				}

				ns.Properties.Add(new(parts[parts.Length - 1], property.Type, index, property.IsCanonical));

				index++;
			}
		}

		sb.AppendLine("\t\treturn properties;");
		sb.AppendLine("\t}");
		sb.AppendLine();

		static void EmitNamespace(StringBuilder sb, string indent, NamespaceInfo ns)
		{
			sb.AppendLine($"{indent}public static class {ns.Name}");
			sb.AppendLine($"{indent}{{");
			foreach (var ns2 in ns.Namespaces)
			{
				EmitNamespace(sb, indent + "\t", ns2);
			}
			foreach (var p in ns.Properties)
			{
				sb.AppendLine($"{indent}\tpublic static readonly {p.Type}Property {p.Name} = Unsafe.As<{p.Type}Property>(AllProperties[{p.ArrayIndex}]);");
			}
			sb.AppendLine($"{indent}}}");
		}

		EmitNamespace(sb, "\t", rootNamespace);

		sb.AppendLine("}");

		context.AddSource("Properties.Generated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	private sealed class NamespaceInfo
	{
		public NamespaceInfo(string name) => Name = name;

		public string Name { get; }
		public List<NamespaceInfo> Namespaces { get; } = new();
		public List<PropertyInfo> Properties { get; } = new();
	}

	private sealed class PropertyInfo
	{
		public string Name { get; }
		public string Type { get; }
		public int ArrayIndex { get; }
		public bool IsCanonical { get; }

		public PropertyInfo(string name, string type, int arrayIndex, bool isCanonical)
		{
			Name = name;
			Type = type;
			ArrayIndex = arrayIndex;
			IsCanonical = isCanonical;
		}
	}
}
