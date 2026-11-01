using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the base calls that open and close a member body.</summary>
/// <remarks>
/// A base call is a call statement whose chain starts at <see langword="base"/>, awaited, discarded or neither.
/// </remarks>
internal static class BlankLineBaseCalls
{
	/// <summary>Finds the boundary below the opening base call and the one above the closing base call.</summary>
	/// <remarks>
	/// The opening call is the first statement below the guards, and no rule's state moves either boundary.
	/// </remarks>
	public static ImmutableArray<int> FindBaseCallBoundaries(ContainerContext container)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;

		if (siblings.Length < 2 || !IsMemberBody(container.Node))
			return [];

		int opening = BlankLineGuards.ReadLeadingGuardCount(siblings, container.Options);
		int closing = siblings.Length - 1;

		bool opens = opening < closing && IsBaseCall(siblings[opening]);
		bool closes = closing >= opening && IsBaseCall(siblings[closing]);

		// Calls with a statement between them ask for two gaps, and adjacent calls share the gap between them.
		if (opens && closes && opening + 1 < closing)
			return [opening + 1, closing];

		if (opens)
			return [opening + 1];

		return closes ? [closing] : [];
	}

	/// <summary>Whether the base call rule is on and asks for the gap, for a rule that defers to it.</summary>
	public static bool IsAskedGap(ContainerContext container, int boundary, StanzaRule askingRule)
	{
		if (
			StanzaRuleState.FindJudgingDeferral(askingRule, DiagnosticDescriptors.SeparateBaseCall, containerKind: container.Kind)
			is not { } baseCallRule
		)
		{
			return false;
		}

		return !StanzaRuleState.IsOffAt(
				scope: container,
				options: container.Options,
				rule: baseCallRule,
				position: container.Siblings[boundary].SpanStart
			) && FindBaseCallBoundaries(container).Contains(boundary);
	}

	/// <summary>Reads the boundaries the base call rule asks for, for a rule that defers to it.</summary>
	/// <remarks>
	/// It reads none while the rule is off, and none that a pragma silences, so a run crosses such a gap.
	/// </remarks>
	public static ImmutableHashSet<int> ReadAskedBaseCallBoundaries(ContainerContext container, StanzaRule askingRule)
	{
		StanzaRule? baseCallRule = StanzaRuleState.FindJudgingDeferral(
			askingRule,
			DiagnosticDescriptors.SeparateBaseCall,
			containerKind: container.Kind
		);

		if (baseCallRule is null || !IsMemberBody(container.Node) || StanzaRuleState.IsRuleDisabled(container, container.Options, baseCallRule))
			return [];

		ImmutableHashSet<int>.Builder asked = ImmutableHashSet.CreateBuilder<int>();

		foreach (int boundary in FindBaseCallBoundaries(container))
		{
			if (
				!StanzaPragmas.IsSilencedByPragma(
					syntaxTree: container.Node.SyntaxTree,
					ruleId: baseCallRule.Descriptor.Id,
					position: container.Siblings[boundary].SpanStart,
					cancellationToken: container.CancellationToken
				)
			)
			{
				asked.Add(boundary);
			}
		}

		return asked.ToImmutable();
	}

	/// <summary>Whether the block is the body of a member, such as a method, a constructor or an accessor.</summary>
	/// <remarks>
	/// A local function and a lambda are left out, as a base call there is no call the member extends.
	/// </remarks>
	private static bool IsMemberBody(SyntaxNode container) =>
		container is BlockSyntax { Parent: BaseMethodDeclarationSyntax or AccessorDeclarationSyntax };

	private static bool IsBaseCall(SyntaxNode statement)
	{
		// A discard such as `_ = base.Load();` makes the call it assigns, and the chain starts from its receiver.
		return statement is ExpressionStatementSyntax { Expression: var expression }
			&& BlankLineExpressions.FindInvocation(expression) is not null
			&& BlankLineExpressions.GetChainStart(BlankLineExpressions.UnwrapDiscard(expression)) is BaseExpressionSyntax;
	}
}
