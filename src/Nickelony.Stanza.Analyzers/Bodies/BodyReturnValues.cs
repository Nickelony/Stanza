using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.Bodies;

internal static class BodyReturnValues
{
	/// <summary>
	/// Whether the member returns a value; an <see langword="async"/> member does only with type arguments.
	/// </summary>
	public static bool ReturnsValue(SemanticModel semanticModel, SyntaxNode owner)
	{
		return owner switch
		{
			ConstructorDeclarationSyntax or DestructorDeclarationSyntax => false,
			AccessorDeclarationSyntax accessor => accessor.Keyword.IsKind(SyntaxKind.GetKeyword),
			PropertyDeclarationSyntax or IndexerDeclarationSyntax => true,
			MethodDeclarationSyntax method => ReturnsValue(semanticModel, method.ReturnType, method.Modifiers),
			LocalFunctionStatementSyntax localFunction => ReturnsValue(semanticModel, localFunction.ReturnType, localFunction.Modifiers),
			// A C# 14 compound assignment or increment operator can return void.
			OperatorDeclarationSyntax @operator => ReturnsValue(semanticModel, @operator.ReturnType, @operator.Modifiers),
			// The callers pass only conversion operators to this arm, and a conversion always hands a value back.
			_ => true,
		};
	}

	private static bool ReturnsValue(SemanticModel semanticModel, TypeSyntax returnType, SyntaxTokenList modifiers)
	{
		if (returnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
			return false;

		// An async member returns a value only when its task type has type arguments, such as Task<T>.
		// The type is bound, so an alias resolves to its target, and a type the model cannot bind keeps its written arity.
		return !StanzaSyntax.HasModifier(modifiers, SyntaxKind.AsyncKeyword)
			|| semanticModel.GetTypeInfo(returnType).Type is INamedTypeSymbol { TypeArguments.Length: > 0 };
	}
}
