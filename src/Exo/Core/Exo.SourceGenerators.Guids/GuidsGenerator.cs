using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Exo.Core.SourceGenerators;

[Generator]
public class GuidsGenerator : IIncrementalGenerator
{
	public static JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.General)
	{
		ReadCommentHandling = JsonCommentHandling.Skip,
	};

	public void Initialize(IncrementalGeneratorInitializationContext context)
		=> context.RegisterSourceOutput
		(
			context.AdditionalTextsProvider
				.Where(at => Path.GetFileName(at.Path) == "Guids.json")
				.Select((at, c) => at.GetText()!)
				.Where(t => t is not null),
			Execute
		);

	private void Execute(SourceProductionContext context, SourceText text)
	{
		JsonObject? root = null;

		try
		{
			root = JsonSerializer.Deserialize<JsonObject>(text.ToString(), JsonSerializerOptions);
		}
		catch (JsonException ex)
		{
		}

		if (root is null)
		{
			context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("GG0001", "Invalid JSON file.", "Invalid JSON file.", "GuidGenerator", DiagnosticSeverity.Error, true), null));
			return;
		}

		// For now, we only do very basic GUID deduplication, because doing things otherwise would be too complex.
		// If a GUID is referenced in an array, it will always be copied in the sequence of that array.
		// The "easy" deduplication we could do is to make sure that unique references to that guid point into the array if the array is defined after the initial occurrence.
		// But… even that would make things too complex for now. And anyway, that can be mostly worked around in the code if desired, and if it ever was a real problem.
		var guidIndices = new Dictionary<Guid, int>();
		// We will try to nest namespaces in the generated source, although the namespaces in the json file will be flat.
		Dictionary<string, NamespaceInfo> namespaces = new();
		List<Guid> guids = [];
		bool hasArray = false;

		try
		{
			foreach (var kvp in root)
			{
				if (kvp.Value is not JsonObject o)
				{
					ReportExpectedObjectForPropertyError(context, kvp.Key);
					return;
				}

				ProcessNamespace(kvp.Key, o);
			}
		}
		catch (ProcessingCanceledException)
		{
			return;
		}

		void ProcessNamespace(string namespaceName, JsonObject ns)
		{
			Dictionary<string, NamespaceInfo> currentNamespaces = namespaces;
			NamespaceInfo? info = null;

			foreach (var section in namespaceName.Split('.'))
			{
				if (!currentNamespaces.TryGetValue(section, out info))
				{
					currentNamespaces.Add(section, info = new NamespaceInfo(section));
				}
				currentNamespaces = info.Namespaces;
			}

			foreach (var kvp in ns)
			{
				if (kvp.Value is not JsonObject o)
				{
					ReportExpectedObjectForPropertyError(context, kvp.Key);
					throw new ProcessingCanceledException();
				}

				info!.Classes.Add(ProcessClass(namespaceName, kvp.Key, o));
			}
		}

		ClassInfo ProcessClass(string namespaceName, string className, JsonObject c)
		{
			var info = new ClassInfo(className);
			foreach (var kvp in c)
			{
				var valueKind = kvp.Value?.GetValueKind() ?? JsonValueKind.Undefined;
				switch (valueKind)
				{
				case JsonValueKind.Object:
					info.Classes.Add(ProcessClass(namespaceName, kvp.Key, (JsonObject)kvp.Value!));
					break;
				case JsonValueKind.Array:
					info.Guids.Add(ProcessGuidArray(kvp.Key, (JsonArray)kvp.Value!));
					break;
				case JsonValueKind.String:
					info.Guids.Add(ProcessGuid(kvp.Key, kvp.Value!.GetValue<string>()));
					break;
				default:
					ReportUnexpectedValueKindInClassDefinitionError(context, namespaceName, className, valueKind);
					throw new ProcessingCanceledException();
				}
			}
			return info;
		}

		GuidInfo ProcessGuid(string name, string value)
		{
			if (value.Length != 36 || !Guid.TryParse(value, out var guid))
			{
				ReportInvalidGuidValueError(context, value);
				throw new ProcessingCanceledException();
			}

			if (!guidIndices.TryGetValue(guid, out int index))
			{
				guidIndices.Add(guid, index = guids.Count);
				guids.Add(guid);
			}
			return new(name, index);
		}

		GuidInfo ProcessGuidArray(string name, JsonArray array)
		{
			int index = guids.Count;
			foreach (var value in array)
			{
				var valueKind = value?.GetValueKind() ?? JsonValueKind.Undefined;
				if (valueKind != JsonValueKind.String)
				{
					ReportUnexpectedValueKindInGuidArrayError(context, valueKind);
					throw new ProcessingCanceledException();
				}
				var text = value!.GetValue<string>()!;
				if (text.Length != 36 || !Guid.TryParse(text, out var guid))
				{
					ReportInvalidGuidValueError(context, text);
					throw new ProcessingCanceledException();
				}
				if (!guidIndices.ContainsKey(guid)) guidIndices.Add(guid, guids.Count);
				guids.Add(guid);
			}
			hasArray = true;
			return new(name, index, array.Count);
		}

		var sb = new StringBuilder(1024);

		const string RootNamespace = "Exo";

		sb.AppendLine("using System;");
		sb.AppendLine("using System.Collections;");
		sb.AppendLine("using System.Collections.Generic;");
		sb.AppendLine("using System.ComponentModel;");
		sb.AppendLine("using System.Runtime.CompilerServices;");
		sb.AppendLine();
		sb.Append("namespace ").AppendLine(RootNamespace);
		sb.AppendLine("{");
		sb.AppendLine("\tnamespace Internal");
		sb.AppendLine("\t{");
		sb.AppendLine("\t\t[EditorBrowsable(EditorBrowsableState.Never)]");
		sb.AppendLine("\t\tinternal static class ꅔ");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\tpublic static ReadOnlySpan<byte> ꁘ => [");
		foreach (var guid in guids)
		{
			sb.Append("\t\t\t\t");
			foreach (var b in guid.ToByteArray())
			{
				sb.Append("0x").Append(b.ToString("X2", CultureInfo.InvariantCulture)).Append(", ");
			}
			sb.AppendLine();
		}
		sb.AppendLine("\t\t\t];");
		sb.AppendLine("\t\t}");
		sb.AppendLine("\t}");
		if (hasArray)
		{
			sb.AppendLine();
			sb.AppendLine("\tinternal readonly struct ImmutableGuidArray : IReadOnlyList<Guid>");
			sb.AppendLine("\t{");
			sb.AppendLine("\t\tprivate readonly int _index;");
			sb.AppendLine("\t\tprivate readonly int _count;");
			sb.AppendLine("\t\t");
			sb.AppendLine("\t\tinternal ImmutableGuidArray(int index, int count) => (_index, _count) = (index, count);");
			sb.AppendLine("\t\t");
			sb.AppendLine("\t\tpublic Guid this[int index] => (uint)index < (uint)_count ? new(Internal.ꅔ.ꁘ.Slice((_index + index) * 16, 16)) : throw new ArgumentException(nameof(index));");
			sb.AppendLine("\t\tpublic int Count => _count;");
			sb.AppendLine("\t\t");
			sb.AppendLine("\t\tpublic Enumerator GetEnumerator() => new(this);");
			sb.AppendLine("\t\tIEnumerator IEnumerable.GetEnumerator() => GetEnumerator();");
			sb.AppendLine("\t\tIEnumerator<Guid> IEnumerable<Guid>.GetEnumerator() => GetEnumerator();");
			sb.AppendLine("\t\t");
			sb.AppendLine("\t\tpublic static implicit operator ReadOnlySpan<Guid>(ImmutableGuidArray array) => System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Guid>(Internal.ꅔ.ꁘ.Slice(array._index * 16, array._count * 16));");
			sb.AppendLine("\t\t");
			sb.AppendLine("\t\tpublic struct Enumerator : IEnumerator<Guid>");
			sb.AppendLine("\t\t{");
			sb.AppendLine("\t\t\tprivate int _index;");
			sb.AppendLine("\t\t\tprivate readonly int _count;");
			sb.AppendLine("\t\t\tprivate readonly int _startIndex;");
			sb.AppendLine("\t\t\t");
			sb.AppendLine("\t\t\tinternal Enumerator(ImmutableGuidArray ꉩ) : this(ꉩ._index, ꉩ._count) { }");
			sb.AppendLine("\t\t\t");
			sb.AppendLine("\t\t\tprivate Enumerator(int index, int count)");
			sb.AppendLine("\t\t\t{");
			sb.AppendLine("\t\t\t\t_index = -1;");
			sb.AppendLine("\t\t\t\t_count = count;");
			sb.AppendLine("\t\t\t\t_startIndex = index;");
			sb.AppendLine("\t\t\t}");
			sb.AppendLine("\t\t\t");
			sb.AppendLine("\t\t\tpublic readonly void Dispose() { }");
			sb.AppendLine("\t\t\t");
			sb.AppendLine("\t\t\tpublic readonly Guid Current => new(Internal.ꅔ.ꁘ.Slice((_startIndex + _index) * 16, 16));");
			sb.AppendLine("\t\t\treadonly object IEnumerator.Current => Current;");
			sb.AppendLine("\t\t\t");
			sb.AppendLine("\t\t\tpublic bool MoveNext() => (uint)++_index < (uint)_count;");
			sb.AppendLine("\t\t\tpublic void Reset() => _index = -1;");
			sb.AppendLine("\t\t}");
			sb.AppendLine("\t}");
		}
		sb.AppendLine("}");
		sb.AppendLine();
		foreach (var ns in namespaces.Values)
		{
			EmitNamespace(sb, "", ns);
		}

		static void EmitNamespace(StringBuilder sb, string indent, NamespaceInfo ns)
		{
			sb.Append(indent).Append("namespace ").AppendLine(ns.Name);
			sb.Append(indent).AppendLine("{");
			string indent2 = indent + "\t";
			foreach (var c in ns.Classes)
			{
				EmitClass(sb, indent2, c);
			}
			foreach (var ns2 in ns.Namespaces.Values)
			{
				EmitNamespace(sb, indent2, ns2);
			}
			sb.Append(indent).AppendLine("}");
		}

		static void EmitClass(StringBuilder sb, string indent, ClassInfo c)
		{
			bool isStruct = c.Name.StartsWith("struct:", StringComparison.Ordinal);
			sb.Append(indent).Append(isStruct ? "partial struct " : "partial class ").AppendLine(isStruct ? c.Name.Substring(7) : c.Name);
			sb.Append(indent).AppendLine("{");
			string indent2 = indent + "\t";
			foreach (var g in c.Guids)
			{
				string name = g.Name;
				string accessibility = "private";
				if (name.StartsWith("public:"))
				{
					accessibility = "public";
					name = name.Substring(7);
				}
				else if (name.StartsWith("internal:"))
				{
					accessibility = "internal";
					name = name.Substring(9);
				}
				else if (name.StartsWith("protected:"))
				{
					accessibility = "protected";
					name = name.Substring(10);
				}
				else if (name.StartsWith("private protected:"))
				{
					accessibility = "private protected";
					name = name.Substring(18);
				}
				if (g.GuidCount == 0)
				{
					sb.Append(indent2)
						.Append(accessibility)
						.Append(" static Guid ")
						.Append(name)
						.Append(" => new(global::").Append(RootNamespace).Append(".Internal.ꅔ.ꁘ.Slice(").Append(g.GuidIndex * 16).AppendLine(", 16));");
				}
				else
				{
					sb.Append(indent2)
						.Append(accessibility)
						.Append(" static global::").Append(RootNamespace).Append(".ImmutableGuidArray ")
						.Append(name)
						.Append(" => new(").Append(g.GuidIndex).Append(", ").Append(g.GuidCount).AppendLine(");");
				}
			}
			foreach (var c2 in c.Classes)
			{
				EmitClass(sb, indent2, c);
			}
			sb.Append(indent).AppendLine("}");
		}

		context.AddSource("Guids.Generated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	private static void ReportExpectedObjectForPropertyError(SourceProductionContext context, string propertyName)
		=> context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("GG0002", "Invalid JSON data.", $"Expected an object for property {propertyName}.", "GuidGenerator", DiagnosticSeverity.Error, true), null));

	private static void ReportUnexpectedValueKindInClassDefinitionError(SourceProductionContext context, string namespaceName, string className, JsonValueKind valueKind)
		=> context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("GG0003", "Invalid JSON node.", $"Unexpected {valueKind} encountered in class definition {namespaceName}.{className}.", "GuidGenerator", DiagnosticSeverity.Error, true), null));

	private static void ReportInvalidGuidValueError(SourceProductionContext context, string value)
		=> context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("GG0004", "Invalid GUID.", $"The value {value} is not a valid GUID.", "GuidGenerator", DiagnosticSeverity.Error, true), null));

	private static void ReportUnexpectedValueKindInGuidArrayError(SourceProductionContext context, JsonValueKind valueKind)
		=> context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("GG0005", "Invalid JSON array value.", $"Values of type {valueKind} are not supported in GUID arrays.", "GuidGenerator", DiagnosticSeverity.Error, true), null));

	private sealed class NamespaceInfo(string name)
	{
		public string Name { get; } = name;
		public Dictionary<string, NamespaceInfo> Namespaces { get; } = new();
		public List<ClassInfo> Classes { get; } = new();
	}

	private sealed class ClassInfo(string name)
	{
		public string Name { get; } = name;
		public List<ClassInfo> Classes { get; } = new();
		public List<GuidInfo> Guids { get; } = new();
	}

	private sealed class GuidInfo(string name, int guidIndex, int guidCount = 0)
	{
		public string Name { get; } = name;
		public int GuidIndex { get; } = guidIndex;
		public int GuidCount { get; } = guidCount;
	}

	private sealed class ProcessingCanceledException : Exception
	{
	}
}
