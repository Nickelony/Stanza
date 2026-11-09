using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.Limits;

/// <summary>Finds the parameter lists that hold more parameters than a limit allows.</summary>
internal static class ParameterLists
{
	/// <summary>Collects the parameter list of each method, local function and delegate past the limit.</summary>
	/// <remarks>
	/// A constructor, an override, an explicit interface implementation and an extern method are left out.
	/// </remarks>
	public static ImmutableArray<ParameterListSyntax> CollectLongLists(SyntaxNode root, int limit, CancellationToken cancellationToken)
	{
		ImmutableArray<ParameterListSyntax>.Builder longLists = ImmutableArray.CreateBuilder<ParameterListSyntax>();

		foreach (SyntaxNode node in root.DescendantNodes())
		{
			cancellationToken.ThrowIfCancellationRequested();

			ParameterListSyntax? list = node switch
			{
				MethodDeclarationSyntax method when !IsOwnedElsewhere(method) => method.ParameterList,
				LocalFunctionStatementSyntax function => function.ParameterList,
				DelegateDeclarationSyntax declaration => declaration.ParameterList,
				_ => null,
			};

			if (list is not null && list.Parameters.Count > limit)
				longLists.Add(list);
		}

		return longLists.ToImmutable();
	}

	/// <summary>Whether the signature is fixed by an override, an interface or a native function.</summary>
	private static bool IsOwnedElsewhere(MethodDeclarationSyntax method)
	{
		return method.ExplicitInterfaceSpecifier is not null
			|| method.Modifiers.Any(SyntaxKind.OverrideKeyword)
			|| method.Modifiers.Any(SyntaxKind.ExternKeyword);
	}
}
