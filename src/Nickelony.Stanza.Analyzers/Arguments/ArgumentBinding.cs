using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Nickelony.Stanza.Analyzers.Arguments;

/// <summary>Reads a call's argument list and binding, and pairs its arguments with their parameters.</summary>
internal static class ArgumentBinding
{
	/// <summary>Writes a parameter named after a keyword with <c>@</c>, so the name reads as an identifier.</summary>
	public static string EscapeName(string name) => SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : $"@{name}";

	/// <summary>Whether a named argument may precede a positional one, which C# 7.2 first allows.</summary>
	public static bool AllowsNamesBeforePositions(SyntaxTree syntaxTree) => StanzaSyntax.SupportsLanguage(syntaxTree, LanguageVersion.CSharp7_2);

	/// <summary>
	/// Pairs by span containment, not position, as the bindings also hold entries the call writes no argument for.
	/// </summary>
	public static IArgumentOperation?[] PairArgumentsWithBindings(BaseArgumentListSyntax argumentList, ImmutableArray<IArgumentOperation> bound)
	{
		SeparatedSyntaxList<ArgumentSyntax> arguments = argumentList.Arguments;
		var pairing = new IArgumentOperation?[arguments.Count];

		// An omitted optional parameter or an empty params array has a binding but no written argument.
		foreach (IArgumentOperation candidate in bound)
		{
			if (candidate.Syntax is not { } syntax)
				continue;

			// Arguments are disjoint and ascending:
			// the carrier is the last one starting at or before the syntax.
			int low = 0;
			int high = arguments.Count - 1;
			int containing = -1;

			while (low <= high)
			{
				int middle = low + ((high - low) / 2);

				if (arguments[middle].SpanStart <= syntax.SpanStart)
				{
					containing = middle;
					low = middle + 1;
				}
				else
				{
					high = middle - 1;
				}
			}

			if (containing >= 0 && syntax.Span.End <= arguments[containing].Span.End && pairing[containing] is null)
				pairing[containing] = candidate;
		}

		return pairing;
	}

	public static int CountLeadingImplicitArguments(ImmutableArray<IArgumentOperation> bound, BaseArgumentListSyntax argumentList)
	{
		int count = 0;

		foreach (IArgumentOperation candidate in bound)
		{
			if (candidate.Syntax is { } syntax && IsWritten(argumentList, syntax))
				break;

			count++;
		}

		return count;
	}

	private static bool IsWritten(BaseArgumentListSyntax argumentList, SyntaxNode syntax)
	{
		foreach (ArgumentSyntax argument in argumentList.Arguments)
		{
			if (IsInside(argument, syntax))
				return true;
		}

		return false;
	}

	private static bool IsInside(ArgumentSyntax argument, SyntaxNode syntax) =>
		syntax.Span.Start >= argument.SpanStart && syntax.Span.End <= argument.Span.End;

	/// <summary>Whether a conditional directive stands among the arguments or between the tokens of one.</summary>
	public static bool HasConditionalDirectiveAmongArguments(BaseArgumentListSyntax argumentList)
	{
		if (!argumentList.ContainsDirectives)
			return false;

		// A directive starts its line, so it sits in the leading trivia of the token after it.
		// The tokens that can follow one among the arguments are the brackets and the commas.
		foreach (SyntaxToken token in argumentList.ChildTokens())
		{
			if (StanzaSyntax.HasConditionalDirective(token.LeadingTrivia))
				return true;
		}

		foreach (SyntaxToken separator in argumentList.Arguments.GetSeparators())
		{
			if (StanzaSyntax.HasConditionalDirective(separator.LeadingTrivia))
				return true;
		}

		foreach (ArgumentSyntax argument in argumentList.Arguments)
		{
			if (argument.ContainsDirectives && HasConditionalDirectiveBesideTheArgumentsOwnTokens(argument))
				return true;
		}

		return false;
	}

	/// <summary>Whether a conditional directive stands in an argument outside its brackets and braces.</summary>
	/// <remarks>
	/// A directive inside a bracket or a brace, as in a lambda body, leaves the arguments of the list as they are.
	/// </remarks>
	private static bool HasConditionalDirectiveBesideTheArgumentsOwnTokens(ArgumentSyntax argument)
	{
		int depth = 0;

		foreach (SyntaxToken token in argument.DescendantTokens())
		{
			if (depth == 0 && StanzaSyntax.HasConditionalDirective(token.LeadingTrivia))
				return true;

			if (token.Kind() is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken or SyntaxKind.OpenBraceToken)
				depth++;
			else if (token.Kind() is SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken or SyntaxKind.CloseBraceToken)
				depth--;
		}

		return false;
	}

	public static BaseArgumentListSyntax? FindArgumentList(SyntaxNode node)
	{
		return node switch
		{
			BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
			ConstructorInitializerSyntax initializer => initializer.ArgumentList,
			ElementAccessExpressionSyntax elementAccess => elementAccess.ArgumentList,
			InvocationExpressionSyntax invocation => invocation.ArgumentList,
			PrimaryConstructorBaseTypeSyntax primary => primary.ArgumentList,
			_ => null,
		};
	}

	/// <summary>Finds the member a call binds to and the arguments it binds, or nothing for a call with none.</summary>
	public static BoundCall? FindBoundCall(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken)
	{
		BoundCall? call = semanticModel.GetOperation(node, cancellationToken) switch
		{
			IInvocationOperation invocation when HasAuthoredParameters(invocation.TargetMethod) => new BoundCall(
				invocation.TargetMethod,
				invocation.Arguments
			),

			IObjectCreationOperation { Constructor: { } constructor } creation => new BoundCall(constructor, creation.Arguments),
			IPropertyReferenceOperation property when property.Property.IsIndexer => new BoundCall(property.Property, property.Arguments),
			_ => null,
		};

		return call is { Arguments.IsDefaultOrEmpty: false } ? call : null;
	}

	private static bool HasAuthoredParameters(IMethodSymbol method) => method.MethodKind != MethodKind.DelegateInvoke;

	/// <summary>The method, constructor or indexer a call binds to, and the arguments it binds.</summary>
	internal sealed record BoundCall(ISymbol Member, ImmutableArray<IArgumentOperation> Arguments);
}
