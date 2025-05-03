using System.Collections;
using System.Collections.Immutable;
using System.Data;
using System.Globalization;
using System.Text;
using Exo.Lighting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Exo.Core.SourceGenerators;

[Generator]
public class EffectSerializationGenerator : IIncrementalGenerator
{
	private static readonly DiagnosticDescriptor[] Diagnostics =
	[
		new("ESG0001", "Conflicting serialization attributes", "The member {1} of type {0} has conflicting serialization attributes.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
		new("ESG0002", "Non-public member marked for serialization", "The non-public member {1} of type {0} is marked for serialization.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
		new("ESG0003", "Invalid data type", "The member {1} of type {0} has a type that is not supported for serialization.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
		new("ESG0004", "Invalid array limits", "The member {1} of type {0} is a variable array with incorrect limits.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
		new("ESG0005", "Missing variable array limits", "The member {1} of type {0} is a variable array but limits were not specified.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
		new("ESG0006", "Variable array limits not allowed", "The member {1} of type {0} specified variable array limits while not variable array.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
		new("ESG0007", "Invalid default value", "The member {1} of type {0} has an invalid value specified for its default value.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
		new("ESG0008", "Invalid array default value", "The member {1} of type {0} has an invalid number of default values.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
		new("ESG0009", "Invalid minimum value", "The member {1} of type {0} has an invalid value specified for its minimum value.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
		new("ESG0010", "Invalid maximum value", "The member {1} of type {0} has an invalid value specified for its maximum value.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
	];

	private const string EffectInterfaceTypeName = "Exo.Lighting.Effects.ILightingEffect";

	private static bool IsTypeId(SimpleNameSyntax nameSyntax) => nameSyntax.Identifier.Text == "TypeId" || nameSyntax.Identifier.Text == "TypeIdAttribute";

	private static bool CouldBeTypeId(NameSyntax nameSyntax)
	{
		switch (nameSyntax)
		{
		case IdentifierNameSyntax ins: return IsTypeId(ins);
		case QualifiedNameSyntax qns: return IsTypeId(qns.Right);
		case AliasQualifiedNameSyntax aqns: return IsTypeId(aqns.Name);
		}
		return false;
	}

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var effectTypes = context.SyntaxProvider.CreateSyntaxProvider
		(
			(node, cancellationToken) => node is StructDeclarationSyntax structDeclarationSyntax &&
				structDeclarationSyntax.BaseList is { } baseList &&
				baseList.Types.Count > 0 &&
				structDeclarationSyntax.AttributeLists.Any(a => a.Attributes.Any(a => a.ArgumentList?.Arguments.Count == 11 && CouldBeTypeId(a.Name))),
			(context, cancellationToken) =>
			{
				var structDeclarationSyntax = (StructDeclarationSyntax)context.Node;
				bool hasEffectInterface = structDeclarationSyntax.BaseList!.Types.Any
				(
					baseTypeSyntax =>
					{
						var typeInfo = context.SemanticModel.GetTypeInfo(baseTypeSyntax.Type, cancellationToken);
						if (typeInfo.Type is null || typeInfo.Type.TypeKind != TypeKind.Interface) return false;
						if (typeInfo.Type.ToDisplayString() == EffectInterfaceTypeName ||
							typeInfo.Type.AllInterfaces.Any(i => i.ToDisplayString() == EffectInterfaceTypeName))
						{
							return true;
						}
						return false;
					}
				);

				if (!hasEffectInterface) return null;

				if (context.SemanticModel.GetDeclaredSymbol(structDeclarationSyntax) is ITypeSymbol type)
				{
					return AnalyzeEffectType(type, cancellationToken);
				}

				return null;
			}
		).Where(effect => effect is not null).Select((effect, cancellationToken) => effect.GetValueOrDefault());

		// Generate one serializer for each type, and a global registration wrapper for each of those.
		context.RegisterSourceOutput(effectTypes, ExecuteSerializerGeneration);
		context.RegisterSourceOutput(effectTypes.Collect(), ExecuteRegistrationGeneration);
	}

	private void ExecuteRegistrationGeneration(SourceProductionContext context, ImmutableArray<EffectInfo> effects)
	{
		if (effects.IsDefaultOrEmpty) return;

		var sb = new StringBuilder();

		sb.AppendLine("using Exo.Lighting;")
			.AppendLine("using System.ComponentModel;")
			.AppendLine("using System.Runtime.CompilerServices;")
			.AppendLine()
			.AppendLine("namespace Exo.Lighting.Effects.Internal;")
			.AppendLine()
			.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]")
			.AppendLine("internal static class EffectRegistration")
			.AppendLine("{")
			.AppendLine("\t[ModuleInitializer]")
			.AppendLine("\tpublic static void RegisterEffects()")
			.AppendLine("\t{");

		foreach (var effect in effects)
		{
			sb.Append("\t\t")
				.Append("EffectSerializer.RegisterEffect<")
				.Append(effect.FullName)
				.AppendLine(">();");
		}

		sb.AppendLine("\t}")
			.AppendLine("}");

		context.AddSource("EffectRegistration.Generated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	private void ExecuteSerializerGeneration(SourceProductionContext context, EffectInfo effect)
	{
		if (effect.Problems.Count > 0)
		{
			foreach (var problem in effect.Problems)
			{
				context.ReportDiagnostic(Diagnostic.Create(Diagnostics[(int)problem.Kind - 1], null, effect.FullName, problem.Data));
			}
			return;
		}

		var sb = new StringBuilder();

		sb.AppendLine("using Exo.Lighting;")
			.AppendLine("using Exo.Lighting.Effects;")
			.AppendLine()
			.Append("namespace ").AppendLine(effect.Namespace).AppendLine("{")
			.Append("\tpartial struct ").Append(effect.TypeName).Append(" : ILightingEffect<").Append(effect.TypeName).AppendLine(">")
			.AppendLine("\t{")
			.Append("\t\tstatic ReadOnlySpan<byte> TypeIdGuidBytes => [").Append(string.Join(", ", Array.ConvertAll(effect.TypeId.ToByteArray(), b => "0x" + b.ToString("X2")))).AppendLine("];")
			.AppendLine()
			.AppendLine("\t\tstatic Guid ILightingEffect.EffectId => new Guid(TypeIdGuidBytes);")
			.AppendLine()
			.AppendLine("\t\tstatic LightingEffectInformation ILightingEffect.GetEffectMetadata()")
			.AppendLine("\t\t{");

		sb.AppendLine("\t\t\treturn new()")
			.AppendLine("\t\t\t{")
			.AppendLine("\t\t\t\tEffectId = new Guid(TypeIdGuidBytes),")
			.AppendLine("\t\t\t\tProperties =")
			.AppendLine("\t\t\t\t[");
		foreach (var member in effect.Members)
		{
			sb.AppendLine("\t\t\t\t\tnew()")
				.AppendLine("\t\t\t\t\t{")
				.Append("\t\t\t\t\t\tName = ").Append(ToStringLiteral(member.Name)).AppendLine(",")
				.Append("\t\t\t\t\t\tDisplayName = ").Append(ToStringLiteral(member.DisplayName)).AppendLine(",")
				.Append("\t\t\t\t\t\tDataType = LightingDataType.").Append(member.DataType).AppendLine(",");

			if (member.DefaultValue is not null)
			{
				sb.Append("\t\t\t\t\t\tDefaultValue = ");
				AppendValue(sb, member.DataType, member.MinimumElementCount, member.MaximumElementCount, member.DefaultValue);
				sb.AppendLine(",");
			}

			if (member.MinimumValue is not null)
			{
				sb.Append("\t\t\t\t\t\tMinimumValue = ");
				AppendValue(sb, member.DataType, 1, 1, member.MinimumValue);
				sb.AppendLine(",");
			}

			if (member.MaximumValue is not null)
			{
				sb.Append("\t\t\t\t\t\tMaximumValue = ");
				AppendValue(sb, member.DataType, 1, 1, member.MaximumValue);
				sb.AppendLine(",");
			}

			sb.AppendLine("\t\t\t\t\t\tEnumerationValues =")
				.AppendLine("\t\t\t\t\t\t[");
			foreach (var enumValue in member.EnumValues)
			{
				sb.Append("\t\t\t\t\t\t\tnew() { Value = ").Append(enumValue.Value.ToString(CultureInfo.InvariantCulture)).Append(", DisplayName = ").Append(ToStringLiteral(enumValue.DisplayName ?? enumValue.Name)).AppendLine(", Description = null, },");
			}
			sb.AppendLine("\t\t\t\t\t\t],");
			// NB: Should migrate all of this to using constructors or factory methods, so that parameters are checked.
			//if (member.MinimumElementCount != 1 || member.MinimumElementCount != member.MaximumElementCount)
			//{
			sb.Append("\t\t\t\t\t\tMinimumElementCount = ").Append(member.MinimumElementCount.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
				.Append("\t\t\t\t\t\tMaximumElementCount = ").Append(member.MaximumElementCount.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
			//}
			sb.AppendLine("\t\t\t\t\t},");
		}
		sb.AppendLine("\t\t\t\t],")
			.AppendLine("\t\t\t};")
			.AppendLine("\t\t}");

		if (effect.RequiresExplicitSerialization)
		{
			bool hasVariableArray = false;
			sb.AppendLine()
				.Append("\t\tstatic bool ISerializer<").Append(effect.TypeName).Append(">.TryGetSize(in ").Append(effect.TypeName).AppendLine(" value, out uint size)")
				.AppendLine("\t\t{");
			if (effect.Members.Count == 0)
			{
				sb.AppendLine("\t\t\tsize = 0;")
					.AppendLine("\t\t\treturn true;");
			}
			else
			{
				uint fixedLength = 0;
				var variableMembers = new List<(uint, SerializedMemberInfo)>();
				bool hasString = false;
				foreach (var member in effect.Members)
				{
					// Strings would be slightly more contrived, as we need to measure the UTF-8 length, so we will fallback to amore complex generation scenario in this case.
					if (member.DataType == LightingDataType.String)
					{
						variableMembers.Add((0, member));
						hasString = true;
					}
					else
					{
						uint length = GetElementSize(member.DataType);
						if (member.MinimumElementCount != member.MaximumElementCount)
						{
							variableMembers.Add((length, member));
							hasVariableArray = true;
						}
						else if (member.MinimumElementCount > 1)
						{
							fixedLength += (uint)member.MinimumElementCount * length;
						}
						else
						{
							fixedLength += length;
						}
					}
				}
				if (variableMembers.Count == 0)
				{
					sb.Append("\t\t\tsize = ").Append(fixedLength.ToString()).AppendLine(";")
						.AppendLine("\t\t\treturn true;");
				}
				else if (!hasString)
				{
					sb.Append("\t\t\tsize = ").Append(fixedLength.ToString());
					foreach (var (elementSize, member) in variableMembers)
					{
						sb.AppendLine(" +")
							.Append("\t\t\t\t(value.").Append(member.Name).Append(".IsDefault ? 0 : global::Exo.BufferWriter.GetVariableLength((uint)value.").Append(member.Name).Append(".Length) + (uint)value.").Append(member.Name).Append(".Length)");
					}
					sb.AppendLine(";")
						.AppendLine("\t\t\treturn true;");
				}
				else
				{
					// We currently don't have a single need for strings in effect types, so implementing this is not critical.
					throw new Exception("TODO: Implement serialization for strings.");
				}
			}
			sb.AppendLine("\t\t}")
				.AppendLine()
				.Append("\t\tstatic void ISerializer<").Append(effect.TypeName).Append(">.Serialize(ref BufferWriter writer, in ").Append(effect.TypeName).AppendLine(" value)")
				.AppendLine("\t\t{");
			foreach (var member in effect.Members)
			{
				string indent = "\t\t\t";
				string valueName;
				bool isVariableArray = member.MinimumElementCount != member.MaximumElementCount;
				if (isVariableArray)
				{
					sb.Append(indent).Append("if (value.").Append(member.Name).AppendLine(".IsDefaultOrEmpty)")
						.Append(indent).AppendLine("{")
						.Append(indent).AppendLine("\twriter.Write((byte)0);")
						.Append(indent).AppendLine("}")
						.Append(indent).AppendLine("else")
						.Append(indent).AppendLine("{")
						.Append(indent).Append("\twriter.WriteVariable((uint)value.").Append(member.Name).AppendLine(".Length);")
						.Append(indent).Append("\tforeach (var item in value.").Append(member.Name).AppendLine(")")
						.Append(indent).AppendLine("\t{");

					indent += "\t\t";
					valueName = "item";
				}
				else
				{
					valueName = "value." + member.Name;
				}
				string typeCast = "";
				if (member.EnumValues.Count > 0)
				{
					typeCast = member.DataType switch
					{
						LightingDataType.UInt8 => "(byte)",
						LightingDataType.SInt8 => "(sbyte)",
						LightingDataType.UInt16 => "(ushort)",
						LightingDataType.SInt16 => "(short)",
						LightingDataType.UInt32 => "(uint)",
						LightingDataType.SInt32 => "(int)",
						_ => throw new Exception("Unsupported enum type."),
					};
				}
				else if (member.DataType == LightingDataType.EffectDirection1D)
				{
					typeCast = "(byte)";
				}
				if (!isVariableArray && member.MinimumElementCount > 1)
				{
					if (member.DataType == LightingDataType.String) throw new InvalidOperationException("Fixed length arrays of string are not supported. (yet)");
					sb.Append(indent).Append("writer.Write(").Append(valueName).AppendLine(");");
				}
				else
				{
					switch (member.DataType)
					{
					case LightingDataType.UInt8:
					case LightingDataType.SInt8:
					case LightingDataType.UInt16:
					case LightingDataType.SInt16:
					case LightingDataType.UInt32:
					case LightingDataType.SInt32:
					case LightingDataType.UInt64:
					case LightingDataType.SInt64:
					case LightingDataType.UInt128:
					case LightingDataType.SInt128:
					case LightingDataType.Float16:
					case LightingDataType.Float32:
					case LightingDataType.Float64:
					case LightingDataType.Boolean:
					case LightingDataType.DateTime:
					case LightingDataType.TimeSpan:
					case LightingDataType.Guid:
					case LightingDataType.EffectDirection1D:
					case LightingDataType.ColorGrayscale8:
					case LightingDataType.ColorGrayscale16:
						sb.Append(indent).Append("writer.Write(").Append(typeCast).Append(valueName).AppendLine(");");
						break;
					case LightingDataType.String:
						sb.Append(indent).Append("writer.WriteVariableString(").Append(valueName).AppendLine(");");
						break;
					case LightingDataType.ColorRgb24:
						sb.Append(indent).Append("writer.Write(").Append(valueName).AppendLine(".R);");
						sb.Append(indent).Append("writer.Write(").Append(valueName).AppendLine(".G);");
						sb.Append(indent).Append("writer.Write(").Append(valueName).AppendLine(".B);");
						break;
					default:
						throw new InvalidOperationException("Unsupported data type.");
					}
				}
				if (isVariableArray)
				{
					indent = indent.Substring(0, indent.Length - 2);
					sb.Append(indent).AppendLine("\t}")
						.Append(indent).AppendLine("}");
				}
			}
			sb.AppendLine("\t\t}")
				.AppendLine()
				.Append("\t\tstatic void ISerializer<").Append(effect.TypeName).Append(">.Deserialize(ref BufferReader reader, out ").Append(effect.TypeName).AppendLine(" value)")
				.AppendLine("\t\t{");
			if (effect.Members.Count == 0)
			{
				sb.AppendLine("\t\t\tvalue = new();");
			}
			else
			{
				if (hasVariableArray)
				{
					// The same variable (name) will be reused for all variable arrays, so it is declared early.
					sb.AppendLine("\t\t\tuint count;");
				}
				foreach (var member in effect.Members)
				{
					string indent = "\t\t\t";
					bool isVariableArray = member.MinimumElementCount != member.MaximumElementCount;
					string memberType = member.EnumDataTypeName is not null ?
						member.EnumDataTypeName :
						member.DataType switch
						{
							LightingDataType.UInt8 => "byte",
							LightingDataType.SInt8 => "sbyte",
							LightingDataType.UInt16 => "ushort",
							LightingDataType.SInt16 => "short",
							LightingDataType.UInt32 => "uint",
							LightingDataType.SInt32 => "int",
							LightingDataType.UInt64 => "ulong",
							LightingDataType.SInt64 => "long",
							LightingDataType.UInt128 => "global::System.UInt128",
							LightingDataType.SInt128 => "global::System.Int128",
							LightingDataType.Float16 => "global::System.Half",
							LightingDataType.Float32 => "float",
							LightingDataType.Float64 => "double",
							LightingDataType.Boolean => "bool",
							LightingDataType.Guid => "global::System.Guid",
							LightingDataType.TimeSpan => "global::System.TimeSpan",
							LightingDataType.DateTime => "global::System.DateTime",
							LightingDataType.String => "global::System.String",
							LightingDataType.EffectDirection1D => "global::Exo.Lighting.Effects.EffectDirection1D",
							LightingDataType.ColorRgb24 => "global::Exo.ColorFormats.RgbColor",
							LightingDataType.ColorRgbw32 => "global::Exo.ColorFormats.RgbwColor",
							LightingDataType.ColorArgb32 => "global::Exo.ColorFormats.ArgbColor",
							_ => throw new NotImplementedException($"Data type not implemented: {member.DataType}."),
						};
					if (isVariableArray)
					{
						sb.Append(indent).AppendLine("count = reader.ReadVariableUInt32();")
							.Append(indent).Append(memberType).Append("[] ").Append(member.SerializationVariableName).AppendLine(";")
							.Append(indent).AppendLine("if (count == 0)")
							.Append(indent).AppendLine("{")
							.Append(indent).Append("\t").Append(member.SerializationVariableName).AppendLine(" = [];")
							.Append(indent).AppendLine("}")
							.Append(indent).AppendLine("else")
							.Append(indent).AppendLine("{")
							.Append(indent).Append("\t").Append(member.SerializationVariableName).Append(" = new ").Append(memberType).AppendLine("[count];")
							.Append(indent).AppendLine("\tfor (uint i = 0; i < count; i++)")
							.Append(indent).AppendLine("\t{")
							.Append(indent).Append("\t\t").Append(member.SerializationVariableName).Append("[i] = ");

						indent += "\t\t";
					}
					else
					{
						if (member.MinimumElementCount > 1)
						{
							if (member.DataType == LightingDataType.String) throw new InvalidOperationException("Fixed length arrays of string are not supported. (yet)");
							memberType = $"FixedLengthArray{member.MinimumElementCount}<{memberType}>";
						}
						sb.Append(indent)
							.Append(memberType).Append(" ").Append(member.SerializationVariableName).Append(" = ");
					}
					string typeCast = "";
					if (member.EnumDataTypeName is not null)
					{
						typeCast = "(" + member.EnumDataTypeName + ")";
					}
					switch (member.DataType)
					{
					case LightingDataType.UInt8:
						sb.Append(typeCast).AppendLine("reader.ReadByte();");
						break;
					case LightingDataType.SInt8:
						sb.Append(typeCast).AppendLine("(sbyte)reader.ReadByte()");
						break;
					case LightingDataType.UInt16:
						sb.Append(typeCast).AppendLine("reader.Read<ushort>();");
						break;
					case LightingDataType.SInt16:
						sb.Append(typeCast).AppendLine("(short)reader.Read<ushort>();");
						break;
					case LightingDataType.UInt32:
						sb.Append(typeCast).AppendLine("reader.Read<uint>();");
						break;
					case LightingDataType.SInt32:
						sb.Append(typeCast).AppendLine("(int)reader.Read<uint>();");
						break;
					case LightingDataType.UInt64:
						sb.Append(typeCast).AppendLine("reader.Read<ulong>();");
						break;
					case LightingDataType.SInt64:
						sb.Append(typeCast).AppendLine("(long)reader.Read<ulong>();");
						break;
					case LightingDataType.UInt128:
						sb.AppendLine("reader.Read<global::System.UInt128>();");
						break;
					case LightingDataType.SInt128:
						sb.AppendLine("(global::System.Int128)reader.Read<global::System.UInt128>();");
						break;
					case LightingDataType.Float16:
						sb.AppendLine("reader.Read<global::System.Half>();");
						break;
					case LightingDataType.Float32:
						sb.AppendLine("reader.Read<float>();");
						break;
					case LightingDataType.Float64:
						sb.AppendLine("reader.Read<double>();");
						break;
					case LightingDataType.Boolean:
						sb.AppendLine("reader.ReadBoolean();");
						break;
					case LightingDataType.DateTime:
					case LightingDataType.TimeSpan:
						sb.Append(typeCast).AppendLine("new((long)reader.Read<ulong>());");
						break;
					case LightingDataType.Guid:
						sb.AppendLine("reader.ReadGuid();");
						break;
					case LightingDataType.EffectDirection1D:
						sb.AppendLine("(global::Exo.Lighting.Effects.EffectDirection1D)reader.ReadByte();");
						break;
					case LightingDataType.ColorGrayscale8:
						sb.Append(typeCast).AppendLine("reader.ReadByte();");
						break;
					case LightingDataType.ColorGrayscale16:
						sb.Append(typeCast).AppendLine("reader.Read<ushort>();");
						break;
					case LightingDataType.String:
						sb.AppendLine("reader.ReadVariableString();");
						break;
					case LightingDataType.ColorRgb24:
						sb.AppendLine("new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());");
						break;
					default:
						throw new InvalidOperationException("Unsupported data type.");
					}
					if (isVariableArray)
					{
						indent = indent.Substring(0, indent.Length - 2);
						sb.Append(indent).AppendLine("\t}")
							.Append(indent).AppendLine("}");
					}
				}
				sb.Append("\t\t\tvalue = new(");
				if (effect.Members.Count > 0)
				{
					var members = effect.Members;
					OutputMemberVariable(sb, members[0]);
					for (int i = 1; i < members.Count; i++)
					{
						sb.Append(", ");
						OutputMemberVariable(sb, members[i]);
					}
				}
				sb.AppendLine(");");
			}
			sb.AppendLine("\t\t}"); ;
		}

		sb.AppendLine("\t}")
			.AppendLine("}").AppendLine();

		context.AddSource(effect.FullName + ".Serializer.Generated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	private static void OutputMemberVariable(StringBuilder sb, SerializedMemberInfo member)
	{
		if (member.MinimumElementCount != member.MaximumElementCount)
		{
			sb.Append("global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(").Append(member.SerializationVariableName).Append(")");
		}
		else
		{
			sb.Append(member.SerializationVariableName);
		}
	}

	private static uint GetElementSize(LightingDataType dataType)
		=> dataType switch
		{
			LightingDataType.UInt8 or LightingDataType.SInt8 or LightingDataType.ColorGrayscale8 or LightingDataType.EffectDirection1D or LightingDataType.Boolean => 1,
			LightingDataType.UInt16 or LightingDataType.SInt16 or LightingDataType.Float16 or LightingDataType.ColorGrayscale16 => 2,
			LightingDataType.UInt32 or LightingDataType.SInt32 or LightingDataType.Float32 or LightingDataType.ColorRgbw32 or LightingDataType.ColorArgb32 => 4,
			LightingDataType.UInt64 or LightingDataType.SInt64 or LightingDataType.Float64 or LightingDataType.TimeSpan or LightingDataType.DateTime => 8,
			LightingDataType.UInt128 or LightingDataType.SInt128 or LightingDataType.Guid => 16,
			LightingDataType.ColorRgb24 => 3,
			LightingDataType.String => 0,
			_ => throw new Exception("Unsupported data type."),
		};

	private static void AppendValue(StringBuilder sb, LightingDataType dataType, int minimumElementCount, int maximumElementCount, object defaultValue)
	{
		try
		{
			if (defaultValue is Array defaultValues)
			{
				if (defaultValues.Length < minimumElementCount || defaultValues.Length > maximumElementCount) throw new InvalidOperationException("Invalid number of values for the default value.");
				switch (dataType)
				{
				case LightingDataType.ColorRgb24:
					var colors = (uint[])defaultValues;
					sb.Append("new global::Exo.ColorFormats.RgbColor[").Append(colors.Length).Append("]");
					if (colors.Length > 0)
					{
						sb.Append(" { new");
						AppendRgbParameters(sb, colors[0]);
						for (int i = 1; i < colors.Length; i++)
						{
							sb.Append(", new");
							AppendRgbParameters(sb, colors[i]);
						}
						sb.Append(" }");
					}
					break;
				default:
					throw new Exception("TODO: Implement array default values for other data types.");
				}
			}
			else
			{
				switch (dataType)
				{
				case LightingDataType.UInt8: sb.Append("(byte)").Append(((byte)defaultValue).ToString(CultureInfo.InvariantCulture)); break;
				case LightingDataType.SInt8: sb.Append("(sbyte)").Append(((sbyte)defaultValue).ToString(CultureInfo.InvariantCulture)); break;
				case LightingDataType.UInt16: sb.Append("(ushort)").Append(((ushort)defaultValue).ToString(CultureInfo.InvariantCulture)); break;
				case LightingDataType.SInt16: sb.Append("(short)").Append(((short)defaultValue).ToString(CultureInfo.InvariantCulture)); break;
				case LightingDataType.UInt32: sb.Append(((uint)defaultValue).ToString(CultureInfo.InvariantCulture)).Append("l"); break;
				case LightingDataType.SInt32: sb.Append(((int)defaultValue).ToString(CultureInfo.InvariantCulture)); break;
				case LightingDataType.UInt64: sb.Append(((ulong)defaultValue).ToString(CultureInfo.InvariantCulture)).Append("ul"); break;
				case LightingDataType.SInt64: sb.Append(((long)defaultValue).ToString(CultureInfo.InvariantCulture)).Append("l"); break;
				case LightingDataType.Float16: sb.Append("(Half)").Append(((float)defaultValue).ToString("R", CultureInfo.InvariantCulture)).Append("f"); break;
				case LightingDataType.Float32: sb.Append(((float)defaultValue).ToString("R", CultureInfo.InvariantCulture)).Append("f"); break;
				case LightingDataType.Float64: sb.Append(((double)defaultValue).ToString("R", CultureInfo.InvariantCulture)).Append("d"); break;
				case LightingDataType.Guid: throw new NotImplementedException("TODO: GUID default/min/max value serialization.");
				case LightingDataType.TimeSpan: throw new NotImplementedException("TODO: GUID default/min/max value serialization.");
				case LightingDataType.DateTime: throw new NotImplementedException("TODO: GUID default/min/max value serialization.");
				case LightingDataType.Boolean: sb.Append(((bool)defaultValue) ? "true" : "false"); break;
				case LightingDataType.String: sb.Append(ToStringLiteral((string)defaultValue)); break;
				case LightingDataType.EffectDirection1D:
					sb.Append
					(
						(byte)defaultValue switch
						{
							0 => "EffectDirection1D.Forward",
							1 => "EffectDirection1D.Backward",
							_ => throw new Exception("Invalid default value for EffectDirection1D.")
						}
					);
					break;
				case LightingDataType.ColorRgb24:
					if (defaultValue is not null)
					{
						uint color = (uint)defaultValue;
						sb.Append("new global::Exo.ColorFormats.RgbColor");
						AppendRgbParameters(sb, color);
					}
					else
					{
						sb.Append("null");
					}
					break;
				default: throw new NotImplementedException($"TODO: Serialization of default/min/max values of type {dataType}.");
				}
			}
		}
		catch (InvalidCastException ex)
		{
			throw new Exception($"{dataType}: {defaultValue.GetType()}");
		}
		static void AppendRgbParameters(StringBuilder sb, uint color)
		{
			sb.Append("(").Append((byte)(color >> 16))
				.Append(", ").Append((byte)(color >> 8))
				.Append(", ").Append((byte)color).Append(")");
		}
	}

	private static string ToStringLiteral(string? text)
	{
		if (text is null) return "null";

		return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(text)).ToFullString();
	}

	private EffectInfo? AnalyzeEffectType(ITypeSymbol effectType, CancellationToken cancellationToken)
	{
		Guid typeId = default;

		foreach (var attribute in effectType.GetAttributes())
		{
			if (attribute.AttributeClass?.ToDisplayString() == "Exo.TypeIdAttribute" && attribute.ConstructorArguments.Length == 11)
			{
				var args = attribute.ConstructorArguments;
				typeId = new Guid
				(
					Convert.ToUInt32(args[0].Value),
					Convert.ToUInt16(args[1].Value),
					Convert.ToUInt16(args[2].Value),
					Convert.ToByte(args[3].Value),
					Convert.ToByte(args[4].Value),
					Convert.ToByte(args[5].Value),
					Convert.ToByte(args[6].Value),
					Convert.ToByte(args[7].Value),
					Convert.ToByte(args[8].Value),
					Convert.ToByte(args[9].Value),
					Convert.ToByte(args[10].Value)
				);
			}
		}

		if (typeId == default) return null;

		// We'll try our best to determine if a type is only using plain auto-properties and/or public fields.
		// If that's the case and we can determine that the type only uses fixed-length types, we will rely on the default implementation which is a straight copy of the datatype.
		bool isEligibleForSerializationBypass = true;
		var members = new List<SerializedMemberInfo>();
		var problems = new List<ProblemInfo>();

		foreach (var member in effectType.GetMembers())
		{
			// Skip all static members, members that are not fields or properties.
			// We do need to examine all non-public fields and properties to determine if the type is blittable and eligible for raw serialization.
			if (member.IsStatic || member.Kind is not (SymbolKind.Field or SymbolKind.Property)) continue;

			// Collect metadata from attributes specified on the property.
			bool isIgnored = false;
			bool isExplicitlyIncluded = false;
			int minimumElementCount = 1;
			int maximumElementCount = 1;
			bool arrayLimitsSpecified = false;
			string? displayName = null;
			LightingDataType? dataType = null;
			string? enumDataTypeName = null;
			object? defaultValue = null;
			object? minimumValue = null;
			object? maximumValue = null;
			INamedTypeSymbol? memberType = null;
			EquatableReadOnlyList<EnumValueInfo> enumValues = new([]);

			var attributes = member.GetAttributes();

			foreach (var attribute in attributes)
			{
				if (attribute.AttributeClass is null) continue;
				switch (attribute.AttributeClass.ToDisplayString())
				{
				case "System.Runtime.Serialization.DataMemberAttribute":
					isExplicitlyIncluded = true;
					break;
				case "System.Runtime.Serialization.IgnoreDataMemberAttribute":
					isIgnored = true;
					break;
				case "System.ComponentModel.DataAnnotations.DisplayAttribute":
					displayName = (string?)attribute.NamedArguments.FirstOrDefault(na => na.Key == "Name").Value.Value;
					break;
				case "System.ComponentModel.DefaultValueAttribute":
					defaultValue = attribute.ConstructorArguments[0].Value;
					break;
				case "System.ComponentModel.DataAnnotations.RangeAttribute":
					// Ignore Range attributes that specify something else than an integer.
					if (attribute.AttributeConstructor!.Parameters[0].Type.ToDisplayString() != "System.Type")
					{
						minimumValue = attribute.ConstructorArguments[0].Value;
						maximumValue = attribute.ConstructorArguments[1].Value;
					}
					break;
				case "Exo.Lighting.ArrayAttribute":
					minimumElementCount = (int)attribute.ConstructorArguments[0].Value!;
					maximumElementCount = (int)attribute.ConstructorArguments[1].Value!;
					// Obviously, we want variable arrays to actually be variable, so the maximum number of elements should always be more than the minimum.
					if (minimumElementCount < 0 || maximumElementCount < 0 || minimumElementCount >= maximumElementCount)
					{
						problems.Add(new(ProblemKind.InvalidArrayLimits, member.Name));
					}
					arrayLimitsSpecified = true;
					break;
				}
			}

			if (isIgnored && isExplicitlyIncluded)
			{
				problems.Add(new(ProblemKind.ConflictingSerializationAttributes, member.Name));
			}

			if (member is IPropertySymbol property)
			{
				bool hasBody = false;
				// The idea for properties is to consider that any property having a body will automatically switch us to the manual serialization path.
				foreach (var syntaxReference in property.DeclaringSyntaxReferences)
				{
					var propertySyntax = (PropertyDeclarationSyntax)syntaxReference.GetSyntax(cancellationToken);
					if (propertySyntax.ExpressionBody is not null)
					{
						hasBody = true;
						break;
					}
					else if (propertySyntax.AccessorList is not null)
					{
						foreach (var accessor in propertySyntax.AccessorList.Accessors)
						{
							if (accessor.ExpressionBody is not null || accessor.Body is not null)
							{
								hasBody = true;
								break;
							}
						}
					}
				}

				// We never want to serialize non-public properties.
				// The tests below are a matter of determining if those properties are problematic.
				if (property.DeclaredAccessibility != Accessibility.Public)
				{
					// Exactly as for fields, it will be strictly forbidden to mark non-public properties as serialized.
					// Otherwise, a non-public auto property will opt us out of raw serialization.
					if (isExplicitlyIncluded)
					{
						problems.Add(new(ProblemKind.NonPublicMemberMarkedAsSerialized, property.Name));
					}
					else if (!hasBody)
					{
						isEligibleForSerializationBypass = false;
					}
					continue;
				}

				// If the property is not an auto property, we can't guarantee that the type is blittable.
				// If we need to exclude an auto property, the type may be blittable but we MUST NOT serialize it that way because we have to ignore the property.
				if (hasBody || isIgnored)
				{
					isEligibleForSerializationBypass = false;

					if (isExplicitlyIncluded && property.DeclaredAccessibility != Accessibility.Public)
					{
						continue;
					}
				}

				memberType = property.Type as INamedTypeSymbol;
			}
			else if (member is IFieldSymbol field)
			{
				// Investigate non-public fields to determine if they disqualify us from raw serialization.
				// The only private fields that we allow are ones that implement a public auto property. In that case, we will process the property instead of the field.
				// All other non-public fields make the type non blittable for our purposes.
				if (field.DeclaredAccessibility != Accessibility.Public)
				{
					if (!field.IsImplicitlyDeclared || field.AssociatedSymbol is not IPropertySymbol { DeclaredAccessibility: Accessibility.Public })
					{
						isEligibleForSerializationBypass = false;

						if (isExplicitlyIncluded)
						{
							problems.Add(new(ProblemKind.NonPublicMemberMarkedAsSerialized, field.Name));
						}
					}
					continue;
				}

				memberType = field.Type as INamedTypeSymbol;
			}
			else
			{
				throw new InvalidOperationException("The member is of an unexpected type");
			}

			// Determine the data type used for the member.
			if (memberType is not null && memberType.TypeKind is TypeKind.Enum && !IsBuiltInEnumType(memberType))
			{
				enumValues = GetEnumValues(memberType);
				enumDataTypeName = "global::" + memberType.ToDisplayString();
				memberType = memberType.EnumUnderlyingType;
			}

			if (memberType is null || memberType.TypeKind is not (TypeKind.Enum or TypeKind.Structure))
			{
				problems.Add(new(ProblemKind.UnsupportedDataType, member.Name));
			}
			else
			{
				if (memberType.IsGenericType)
				{
					if (memberType.TypeArguments.Length == 1)
					{
						if (memberType.ContainingNamespace.ToDisplayString() == "Exo" && memberType.Name.StartsWith("FixedArray"))
						{
							if (arrayLimitsSpecified)
							{
								problems.Add(new(ProblemKind.ForbiddenArrayLimits, member.Name));
							}
							maximumElementCount = minimumElementCount = int.Parse(memberType.Name.Substring("FixedArray".Length), CultureInfo.InvariantCulture);
							if (minimumElementCount > 0)
							{
								if (memberType.TypeArguments[0] is INamedTypeSymbol elementType)
								{
									dataType = GetDataType(elementType);
								}
							}
						}
						else if (memberType.ContainingNamespace.ToDisplayString() == "System.Collections.Immutable" && memberType.Name.StartsWith("ImmutableArray"))
						{
							if (!arrayLimitsSpecified)
							{
								problems.Add(new(ProblemKind.MissingArrayLimits, member.Name));
							}
							if (minimumElementCount > 0)
							{
								if (memberType.TypeArguments[0] is INamedTypeSymbol elementType)
								{
									dataType = GetDataType(elementType);
								}
							}
							isEligibleForSerializationBypass = false;
						}
					}
				}
				else
				{
					dataType = GetDataType(memberType);
					if (arrayLimitsSpecified)
					{
						problems.Add(new(ProblemKind.ForbiddenArrayLimits, member.Name));
					}
				}
				if (dataType is null)
				{
					problems.Add(new(ProblemKind.UnsupportedDataType, member.Name));
				}
			}

			// NB: For now, we don't support arrays for min/max values, so we pass [1, 1] as array constraints.
			// It would be possible to allow arrays there too, but let's wait until this is needed.
			if (defaultValue is not null) defaultValue = ParseValue(member, dataType, minimumElementCount, maximumElementCount, defaultValue, problems, ValueKind.Default);
			if (minimumValue is not null) minimumValue = ParseValue(member, dataType, 1, 1, minimumValue, problems, ValueKind.Minimum);
			if (maximumValue is not null) maximumValue = ParseValue(member, dataType, 1, 1, maximumValue, problems, ValueKind.Maximum);

			members.Add
			(
				new
				(
					member.Name,
					displayName ?? member.Name,
					dataType ?? LightingDataType.Other,
					enumDataTypeName,
					minimumElementCount,
					maximumElementCount,
					defaultValue,
					minimumValue,
					maximumValue,
					enumValues
				)
			);
		}

		// Empty structs would still have a minimum size of 1, so we'd better resort to implementing an empty serializer for them in all cases.
		if (members.Count == 0)
		{
			isEligibleForSerializationBypass = false;
		}

		return new(typeId, effectType.ContainingNamespace.ToDisplayString(), effectType.Name, effectType.ToDisplayString(), !isEligibleForSerializationBypass, new([.. members]), new([.. problems]));
	}

	private static object? ParseValue(ISymbol member, LightingDataType? dataType, int minimumElementCount, int maximumElementCount, object value, List<ProblemInfo> problems, ValueKind kind)
	{
		if (value is string s && (minimumElementCount != 1 || minimumElementCount != maximumElementCount) && s.IndexOf(',') >= 0)
		{
			// Allow terminating the value list with a comma so that it is possible to differentiate between unique value and array of length 1.
			if (s[s.Length - 1] == ',') s = s.Substring(0, s.Length - 1);

			string[] defaultValues = s.Split([','], StringSplitOptions.None);
			if (defaultValues.Length < minimumElementCount || defaultValues.Length > maximumElementCount)
			{
				problems.Add(new(ProblemKind.InvalidDefaultValue, member.Name));
			}
			else
			{
				try
				{
					value = dataType switch
					{
						LightingDataType.UInt8 => Array.ConvertAll(defaultValues, s => byte.Parse(s, CultureInfo.InvariantCulture)),
						LightingDataType.SInt8 => Array.ConvertAll(defaultValues, s => sbyte.Parse(s, CultureInfo.InvariantCulture)),
						LightingDataType.UInt16 => Array.ConvertAll(defaultValues, s => ushort.Parse(s, CultureInfo.InvariantCulture)),
						LightingDataType.SInt16 => Array.ConvertAll(defaultValues, s => short.Parse(s, CultureInfo.InvariantCulture)),
						LightingDataType.UInt32 => Array.ConvertAll(defaultValues, s => uint.Parse(s, CultureInfo.InvariantCulture)),
						LightingDataType.SInt32 => Array.ConvertAll(defaultValues, s => int.Parse(s, CultureInfo.InvariantCulture)),
						LightingDataType.UInt64 => Array.ConvertAll(defaultValues, s => ulong.Parse(s, CultureInfo.InvariantCulture)),
						LightingDataType.SInt64 => Array.ConvertAll(defaultValues, s => ulong.Parse(s, CultureInfo.InvariantCulture)),
						LightingDataType.Float16 or LightingDataType.Float32 => Array.ConvertAll(defaultValues, s => float.Parse(s, CultureInfo.InvariantCulture)),
						LightingDataType.Float64 => Array.ConvertAll(defaultValues, s => double.Parse(s, CultureInfo.InvariantCulture)),
						LightingDataType.ColorRgb24 => Array.ConvertAll(defaultValues, s => ParseRgbColor(s)),
						_ => throw new InvalidDataException(),
					};
				}
				catch
				{
					problems.Add(new(ProblemKind.InvalidDefaultValue, member.Name));
				}
			}
		}
		else
		{
			try
			{
				value = dataType switch
				{
					LightingDataType.UInt8 or LightingDataType.EffectDirection1D => (object)Convert.ToByte(value),
					LightingDataType.SInt8 => Convert.ToSByte(value),
					LightingDataType.UInt16 => Convert.ToUInt16(value),
					LightingDataType.SInt16 => Convert.ToInt16(value),
					LightingDataType.UInt32 => Convert.ToUInt32(value),
					LightingDataType.SInt32 => Convert.ToInt32(value),
					LightingDataType.UInt64 => Convert.ToUInt64(value),
					LightingDataType.SInt64 => Convert.ToInt64(value),
					LightingDataType.Float16 or LightingDataType.Float32 => Convert.ToSingle(value),
					LightingDataType.Float64 => Convert.ToDouble(value),
					LightingDataType.Boolean => Convert.ToBoolean(value),
					LightingDataType.Guid => throw new Exception("TODO"),
					LightingDataType.TimeSpan => throw new Exception("TODO"),
					LightingDataType.DateTime => throw new Exception("TODO"),
					LightingDataType.String => Convert.ToString(value),
					LightingDataType.ColorRgb24 => ParseRgbColor(value.ToString()),
					_ => throw new InvalidDataException(),
				};
			}
			catch
			{
				problems.Add
				(
					new
					(
						kind switch
						{
							ValueKind.Default => ProblemKind.InvalidDefaultValue,
							ValueKind.Minimum => ProblemKind.InvalidMinimumValue,
							ValueKind.Maximum => ProblemKind.InvalidMaximumValue,
							_ => throw new InvalidOperationException("Wrong value kind."),
						},
						member.Name
					)
				);
			}
		}

		return value;
	}

	private static uint ParseRgbColor(string? s)
	{
		if (s is null || s.Length != 7 || s[0] != '#') throw new ArgumentException(null, nameof(s));
		return uint.Parse(s.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
	}

	private static EquatableReadOnlyList<EnumValueInfo> GetEnumValues(INamedTypeSymbol enumType)
		=> enumType.EnumUnderlyingType!.Name switch
		{
			"Byte" => GetEnumValues<byte>(enumType.GetMembers()),
			"SByte" => GetEnumValues<sbyte>(enumType.GetMembers()),
			"Int16" => GetEnumValues<short>(enumType.GetMembers()),
			"UInt16" => GetEnumValues<ushort>(enumType.GetMembers()),
			"Int32" => GetEnumValues<int>(enumType.GetMembers()),
			"UInt32" => GetEnumValues<uint>(enumType.GetMembers()),
			"Int64" => GetEnumValues<long>(enumType.GetMembers()),
			"UInt64" => GetEnumValues<ulong>(enumType.GetMembers()),
			"Char" => GetEnumValues<char>(enumType.GetMembers()),
			_ => throw new InvalidOperationException("Invalid enum type: " + enumType.Name + "."),
		};

	private static EquatableReadOnlyList<EnumValueInfo> GetEnumValues<TValue>(ImmutableArray<ISymbol> enumMembers)
		where TValue : unmanaged
	{
		var values = new List<EnumValueInfo>();
		foreach (var enumMember in enumMembers)
		{
			if (enumMember.Kind != SymbolKind.Field) break;
			string? displayName = null;
			foreach (var attribute in enumMember.GetAttributes())
			{
				if (attribute.AttributeClass!.MetadataName == "DisplayAttribute" && attribute.AttributeClass.ContainingNamespace.ToDisplayString() == "System.ComponentModel.DataAnnotations")
				{
					foreach (var argument in attribute.NamedArguments)
					{
						if (argument.Key == "Name")
						{
							displayName = (string)argument.Value.Value!;
						}
					}
				}
			}
			var value = ((IFieldSymbol)enumMember).ConstantValue;
			ulong binaryValue;
			if (typeof(TValue) == typeof(byte)) binaryValue = (byte)value!;
			else if (typeof(TValue) == typeof(sbyte)) binaryValue = (byte)(sbyte)value!;
			else if (typeof(TValue) == typeof(short)) binaryValue = (ushort)(short)value!;
			else if (typeof(TValue) == typeof(ushort)) binaryValue = (ushort)value!;
			else if (typeof(TValue) == typeof(int)) binaryValue = (uint)(int)value!;
			else if (typeof(TValue) == typeof(uint)) binaryValue = (uint)value!;
			else if (typeof(TValue) == typeof(long)) binaryValue = (ulong)(long)value!;
			else if (typeof(TValue) == typeof(ulong)) binaryValue = (ulong)value!;
			else if (typeof(TValue) == typeof(char)) binaryValue = (ushort)(char)value!;
			else throw new InvalidOperationException();
			values.Add(new(enumMember.Name, displayName, binaryValue));
		}

		return new([.. values]);
	}

	private bool IsBuiltInEnumType(INamedTypeSymbol memberType)
	{
		if (memberType.ContainingNamespace.ToDisplayString() == "Exo.Lighting.Effects")
		{
			if (memberType.MetadataName == "EffectDirection1D") return true;
		}
		return false;
	}

	private static LightingDataType GetDataType(INamedTypeSymbol memberType)
	{
		if (memberType.ContainingNamespace.ToDisplayString() == "System")
		{
			return memberType.MetadataName switch
			{
				"SByte" => LightingDataType.SInt8,
				"Byte" => LightingDataType.UInt8,
				"Int16" => LightingDataType.SInt16,
				"UInt16" => LightingDataType.UInt16,
				"Int32" => LightingDataType.SInt32,
				"UInt32" => LightingDataType.UInt32,
				"Int64" => LightingDataType.SInt64,
				"UInt64" => LightingDataType.UInt64,
				"Int128" => LightingDataType.SInt128,
				"UInt128" => LightingDataType.UInt128,
				"Half" => LightingDataType.Float16,
				"Single" => LightingDataType.Float32,
				"Double" => LightingDataType.Float64,
				"Boolean" => LightingDataType.Boolean,
				"Guid" => LightingDataType.Guid,
				"TimeSpan" => LightingDataType.TimeSpan,
				"String" => LightingDataType.String,
				_ => LightingDataType.Other,
			};
		}
		else if (memberType.ContainingNamespace.ToDisplayString() == "Exo.ColorFormats")
		{
			return memberType.MetadataName switch
			{
				"RgbColor" => LightingDataType.ColorRgb24,
				"RgbwColor" => LightingDataType.ColorRgbw32,
				"ArgbColor" => LightingDataType.ColorArgb32,
				_ => LightingDataType.Other,
			};
		}
		else if (memberType.ContainingNamespace.ToDisplayString() == "Exo.Lighting.Effects")
		{
			return memberType.MetadataName switch
			{
				"EffectDirection1D" => LightingDataType.EffectDirection1D,
				_ => LightingDataType.Other,
			};
		}
		return LightingDataType.Other;
	}

	private enum ProblemKind
	{
		None = 0,
		ConflictingSerializationAttributes = 1,
		NonPublicMemberMarkedAsSerialized = 2,
		UnsupportedDataType = 3,
		InvalidArrayLimits = 4,
		MissingArrayLimits = 5,
		ForbiddenArrayLimits = 6,
		InvalidDefaultValue = 7,
		InvalidArrayDefaultValue = 8,
		InvalidMinimumValue = 9,
		InvalidMaximumValue = 10,
	}

	private enum ValueKind
	{
		Default = 0,
		Minimum = 1,
		Maximum = 2,
	}

	private record struct ProblemInfo
	{
		public ProblemInfo(ProblemKind kind, string data)
		{
			Kind = kind;
			Data = data;
		}

		public ProblemKind Kind { get; }
		public string Data { get; }
	}

	private record struct EffectInfo
	{
		public EffectInfo(Guid typeId, string @namespace, string typeName, string fullName, bool requiresExplicitSerialization, EquatableReadOnlyList<SerializedMemberInfo> members, EquatableReadOnlyList<ProblemInfo> problems)
		{
			TypeId = typeId;
			Namespace = @namespace;
			TypeName = typeName;
			FullName = fullName;
			RequiresExplicitSerialization = requiresExplicitSerialization;
			Members = members;
			Problems = problems;
		}

		public Guid TypeId { get; }
		public string Namespace { get; }
		public string TypeName { get; }
		public string FullName { get; }
		public bool RequiresExplicitSerialization { get; }
		public EquatableReadOnlyList<SerializedMemberInfo> Members { get; }
		public EquatableReadOnlyList<ProblemInfo> Problems { get; }
	}

	private record struct SerializedMemberInfo
	{
		public SerializedMemberInfo(string name, string displayName, LightingDataType dataType, string? enumDataTypeName, int minimumElementCount, int maximumElementCount, object? defaultValue, object? minimumValue, object? maximumValue, EquatableReadOnlyList<EnumValueInfo> enumValues)
		{
			if (name is null) throw new ArgumentNullException(nameof(name));
			if (name.Length == 0) throw new ArgumentException(null, nameof(name));
			Name = name;
			// Use a suffix so that these variable names don't clash with other variables.
			SerializationVariableName = $"{char.ToLowerInvariant(Name[0])}{name.Substring(1)}__";
			DisplayName = displayName;
			DataType = dataType;
			EnumDataTypeName = enumDataTypeName;
			MinimumElementCount = minimumElementCount;
			MaximumElementCount = maximumElementCount;
			DefaultValue = defaultValue;
			MinimumValue = minimumValue;
			MaximumValue = maximumValue;
			EnumValues = enumValues;
		}

		public string Name { get; }
		public string SerializationVariableName { get; }
		public string DisplayName { get; }
		public LightingDataType DataType { get; }
		public string? EnumDataTypeName { get; }
		public int MinimumElementCount { get; }
		public int MaximumElementCount { get; }
		public object? DefaultValue { get; }
		public object? MinimumValue { get; }
		public object? MaximumValue { get; }
		public EquatableReadOnlyList<EnumValueInfo> EnumValues { get; }
	}

	private record struct EnumValueInfo
	{
		public EnumValueInfo(string name, string? displayName, ulong value)
		{
			Name = name;
			DisplayName = displayName;
			Value = value;
		}

		public string Name { get; }
		public string? DisplayName { get; }
		public ulong Value { get; }
	}

	private readonly struct EquatableReadOnlyList<T> : IReadOnlyList<T>, IEquatable<EquatableReadOnlyList<T>> where T : IEquatable<T>
	{
		public struct Enumerator : IEnumerator<T>
		{
			private readonly T[] _items;
			private int _index;

			internal Enumerator(T[] items)
			{
				_items = items;
				_index = -1;
			}

			void IDisposable.Dispose() { }

			public T Current => _items[_index];
			object IEnumerator.Current => Current;

			public bool MoveNext() => ++_index < _items.Length;
			void IEnumerator.Reset() => _index = -1;
		}

		private readonly T[] _items;

		public EquatableReadOnlyList(T[] items)
		{
			_items = items;
		}

		public int Count => _items.Length;
		public T this[int index] => _items[index];

		public Enumerator GetEnumerator() => new(_items);
		IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public override bool Equals(object? obj) => obj is EquatableReadOnlyList<T> list && Equals(list);

		public bool Equals(EquatableReadOnlyList<T> other)
		{
			if (_items == other._items) return true;
			if (_items is null || other._items is null || _items.Length != other._items.Length) return false;

			for (int i = 0; i < _items.Length; i++)
			{
				if (!_items[i].Equals(other._items[i])) return false;
			}
			return true;
		}

		public override int GetHashCode()
		{
			int hashCode = -1071024834;
			if (_items is not null)
			{
				hashCode = hashCode * -1521134295 + _items.Length;
				for (int i = 0; i < _items.Length; i++)
				{
					hashCode = hashCode * -1521134295 + _items[i].GetHashCode();
				}
			}
			return hashCode;
		}

		public static bool operator ==(EquatableReadOnlyList<T> left, EquatableReadOnlyList<T> right) => left.Equals(right);
		public static bool operator !=(EquatableReadOnlyList<T> left, EquatableReadOnlyList<T> right) => !(left == right);
	}
}
