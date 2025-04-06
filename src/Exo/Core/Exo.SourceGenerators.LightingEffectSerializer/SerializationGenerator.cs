using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Exo.Core.SourceGenerators;

[Generator]
public class SerializationGenerator : IIncrementalGenerator
{
	private static readonly DiagnosticDescriptor[] Diagnostics =
	[
		new("ESG0001", "Conflicting serialization attributes", "The member {1} of type {0} has conflicting serialization attributes.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
		new("ESG0002", "Non-public member marked for serialization", "The non-public member {1} of type {0} is marked for serialization.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
		new("ESG0003", "Invalid data type", "The member {1} of type {0} has a type that is not supported for serialization.", "EffectSerializationGenerator", DiagnosticSeverity.Error, true),
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
				.Append("FutureEffectSerializer.RegisterEffect<")
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

		sb.AppendLine("using Exo.Contracts;")
			.AppendLine("using Exo.Lighting.Effects;")
			.AppendLine()
			.Append("namespace ").AppendLine(effect.Namespace).AppendLine("{")
			.Append("\tpartial struct ").Append(effect.TypeName).Append(" : ILightingEffect<").Append(effect.TypeName).AppendLine(">")
			.AppendLine("\t{")
			.Append("\t\tstatic ReadOnlySpan<byte> TypeIdGuidBytes => [").Append(string.Join(", ", Array.ConvertAll(effect.TypeId.ToByteArray(), b => "0x" + b.ToString("X2")))).AppendLine("];")
			.AppendLine()
			.Append("\t\tstatic LightingEffectInformation ILightingEffect<").Append(effect.TypeName).AppendLine(">.GetEffectMetadata()")
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
				.AppendLine("\t\t\t\t\t\tIndex = null,")
				.Append("\t\t\t\t\t\tName = ").Append(ToStringLiteral(member.Name)).AppendLine(",")
				.Append("\t\t\t\t\t\tDisplayName = ").Append(ToStringLiteral(member.DisplayName)).AppendLine(",")
				.Append("\t\t\t\t\t\tDataType = DataType.").Append(member.DataTypeName).AppendLine(",")
				.AppendLine("\t\t\t\t\t\tDescription = null,")
				.AppendLine("\t\t\t\t\t\tEnumerationValues =")
				.AppendLine("\t\t\t\t\t\t[");
			foreach (var enumValue in member.EnumValues)
			{
				sb.Append("\t\t\t\t\t\t\tnew() { Value = ").Append(enumValue.Value.ToString(CultureInfo.InvariantCulture)).Append(", DisplayName = ").Append(ToStringLiteral(enumValue.DisplayName ?? enumValue.Name)).AppendLine(", Description = null, },");
			}
			sb.AppendLine("\t\t\t\t\t\t],");
			if (member.FixedArrayLength is not null)
			{
				sb.Append("\t\t\t\t\t\tArrayLength = ").Append(member.FixedArrayLength.GetValueOrDefault().ToString(CultureInfo.InvariantCulture)).AppendLine(",");
			}
			sb.AppendLine("\t\t\t\t\t},");
		}
		sb.AppendLine("\t\t\t\t],")
			.AppendLine("\t\t\t};");

		sb.AppendLine("\t\t}")
			.AppendLine("\t}")
			.AppendLine("}").AppendLine();

		context.AddSource(effect.FullName + ".Serializer.Generated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	private string ToStringLiteral(string? text)
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
			int? fixedArrayLength = null;
			string? displayName = null;
			string? dataType = null;
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
			if (memberType is not null && memberType.TypeKind is TypeKind.Enum)
			{
				enumValues = GetEnumValues(memberType);
				memberType = memberType.EnumUnderlyingType;
			}

			if (memberType is null || memberType.TypeKind is not TypeKind.Structure)
			{
				problems.Add(new(ProblemKind.UnsupportedDataType, member.Name));
			}
			else
			{
				if (memberType.IsGenericType)
				{
					if (memberType.TypeArguments.Length == 1 && memberType.ContainingNamespace.ToDisplayString() == "Exo" && memberType.Name.StartsWith("FixedArray"))
					{
						fixedArrayLength = int.Parse(memberType.Name.Substring("FixedArray".Length), CultureInfo.InvariantCulture);
						if (fixedArrayLength.GetValueOrDefault() > 0)
						{
							if (memberType.TypeArguments[0] is INamedTypeSymbol elementType)
							{
								dataType = GetDataType(elementType);
							}
							if (dataType is not null) dataType = "ArrayOf" + dataType;
						}
					}
				}
				else
				{
					dataType = GetDataType(memberType);
				}
				if (dataType is null)
				{
					problems.Add(new(ProblemKind.UnsupportedDataType, member.Name));
				}
			}

			members.Add(new(member.Name, displayName ?? member.Name, dataType ?? "UNSUPPORTED", fixedArrayLength, defaultValue, minimumValue, maximumValue, enumValues));
		}

		// IIRC, empty structs would still have a minimum size of 1, so we'd better resort to implementing an empty serializer for them in all cases.
		if (members.Count == 0)
		{
			isEligibleForSerializationBypass = false;
		}

		return new(typeId, effectType.ContainingNamespace.ToDisplayString(), effectType.Name, effectType.ToDisplayString(), !isEligibleForSerializationBypass, new([.. members]), new([.. problems]));
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

	private static string? GetDataType(INamedTypeSymbol memberType)
	{
		if (memberType.ContainingNamespace.ToDisplayString() == "System")
		{
			return memberType.MetadataName switch
			{
				"Byte" => "Int8",
				"SByte" => "UInt8",
				"Int16" => "Int16",
				"UInt16" => "UInt16",
				"Int32" => "Int32",
				"UInt32" => "UInt32",
				"Int64" => "Int64",
				"UInt64" => "UInt64",
				"Int128" => "Int128",
				"UInt128" => "UInt128",
				"Half" => "Float16",
				"Single" => "Float32",
				"Double" => "Float64",
				"Boolean" => "Boolean",
				"Guid" => "Guid",
				"TimeSpan" => "TimeSpan",
				"String" => "String",
				_ => null,
			};
		}
		else if (memberType.ContainingNamespace.ToDisplayString() == "Exo.ColorFormats")
		{
			return memberType.MetadataName switch
			{
				"RgbColor" => "ColorRgb24",
				"RgbwColor" => "ColorRgbw32",
				"ArgbColor" => "ColorArgb32",
				_ => null,
			};
		}
		return null;
	}

	private enum ProblemKind
	{
		None = 0,
		ConflictingSerializationAttributes = 1,
		NonPublicMemberMarkedAsSerialized = 2,
		UnsupportedDataType = 3,
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
		public SerializedMemberInfo(string name, string displayName, string dataTypeName, int? fixedArrayLength, object? defaultValue, object? minimumValue, object? maximumValue, EquatableReadOnlyList<EnumValueInfo> enumValues)
		{
			Name = name;
			DisplayName = displayName;
			DataTypeName = dataTypeName;
			FixedArrayLength = fixedArrayLength;
			DefaultValue = defaultValue;
			MinimumValue = minimumValue;
			MaximumValue = maximumValue;
			EnumValues = enumValues;
		}

		public string Name { get; }
		public string DisplayName { get; }
		public string DataTypeName { get; }
		public int? FixedArrayLength { get; }
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
