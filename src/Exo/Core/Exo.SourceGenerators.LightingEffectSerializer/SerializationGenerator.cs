using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Exo.Core.SourceGenerators;

[Generator]
public class SerializationGenerator : IIncrementalGenerator
{
	private const string EffectInterfaceTypeName = "Exo.Lighting.Effects.ILightingEffect";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		IncrementalValuesProvider<ITypeSymbol> effectTypes = context.SyntaxProvider.CreateSyntaxProvider
		(
			(node, cancellationToken) => node is StructDeclarationSyntax structDeclarationSyntax && structDeclarationSyntax.BaseList is { } baseList && baseList.Types.Count > 0,
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

				return (ITypeSymbol?)context.SemanticModel.GetDeclaredSymbol(structDeclarationSyntax);
			}
		).Where(type => type is not null)!;

		context.RegisterSourceOutput
		(
			effectTypes,
			Execute
		);
	}

	private void Execute(SourceProductionContext context, ITypeSymbol effectType)
	{
		var sb = new StringBuilder();

		sb.Append("namespace ").AppendLine(effectType.ContainingNamespace.ToDisplayString()).AppendLine("{");

		sb.Append("\tpartial struct ").Append(effectType.Name).Append(" : ISerializer<").Append(effectType.Name).AppendLine(">").AppendLine("\t{");

		sb.AppendLine("\t}");
		sb.AppendLine("}").AppendLine();

		context.AddSource(effectType.ToDisplayString() + ".Serializer.Generated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}
}
