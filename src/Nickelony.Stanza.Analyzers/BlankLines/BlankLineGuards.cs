using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the guard clauses that open a block.</summary>
/// <remarks>
/// A guard is an <c>if</c> with no <c>else</c> whose body is a lone exit, or a call of a throw helper.
/// </remarks>
internal static class BlankLineGuards
{
	/// <summary>Finds the boundary below the leading guards, or -1 when there is none.</summary>
	public static int FindGuardBoundary(ImmutableArray<SyntaxNode> siblings, StanzaOptions options)
	{
		int leading = ReadLeadingGuardCount(siblings, options);
		return leading > 0 && leading < siblings.Length ? leading : -1;
	}

	/// <summary>Reads how many guards open the block, which is the index of the first statement below them.</summary>
	public static int ReadLeadingGuardCount(ImmutableArray<SyntaxNode> siblings, StanzaOptions options)
	{
		int leading = 0;

		while (leading < siblings.Length && IsGuard(siblings[leading], options))
			leading++;

		return leading;
	}

	/// <summary>Finds the boundary the guard rule asks for, or -1 when it is off or judges no such container.</summary>
	/// <remarks>
	/// A rule that defers to the guard rule reads its gap here, as the gap ends a stanza and stays blank.
	/// </remarks>
	public static int FindAskedGuardBoundary(ContainerContext container, StanzaRule askingRule)
	{
		if (
			StanzaRuleState.FindJudgingDeferral(askingRule, DiagnosticDescriptors.SeparateGuardClauses, containerKind: container.Kind)
			is not { } guardRule
		)
		{
			return -1;
		}

		int boundary = FindGuardBoundary(container.Siblings, container.Options);

		if (
			boundary < 0
			|| StanzaRuleState.IsOffAt(
				scope: container,
				options: container.Options,
				rule: guardRule,
				position: container.Siblings[boundary].SpanStart
			)
		)
		{
			return -1;
		}

		return boundary;
	}

	/// <summary>Whether the jump rule is on and asks for the blank line above the statement below the guards.</summary>
	public static bool IsAskedByJumpRule(
		ContainerContext container,
		int boundary,
		GapOwners gapOwners,
		ImmutableHashSet<int> standing,
		StanzaRule guardRule
	)
	{
		StanzaRule jumpRule = guardRule.GetDeferral(DiagnosticDescriptors.SeparateJumpStatement);

		if (!jumpRule.Kinds.Contains(container.Kind) || !BlankLineJumps.FindJudgedJumps(container, jumpRule).Contains(boundary))
			return false;

		return !BlankLineJumps
			.ReadAskedJumpBoundaries(
				siblings: container.Siblings,
				jumps: [boundary],
				gapOwners,
				standing,
				cancellationToken: container.CancellationToken
			)
			.IsEmpty;
	}

	private static bool IsGuard(SyntaxNode sibling, StanzaOptions options)
	{
		return sibling switch
		{
			IfStatementSyntax { Else: null } ifStatement => IsLoneExit(
				ifStatement.Statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : ifStatement.Statement,
				options
			),

			ExpressionStatementSyntax statement => IsThrowHelperCall(statement, options),
			_ => false,
		};
	}

	private static bool IsLoneExit(StatementSyntax statement, StanzaOptions options) =>
		BlankLineJumps.IsJump(statement) || (statement is ExpressionStatementSyntax call && IsThrowHelperCall(call, options));

	/// <summary>Whether the statement calls a throw helper, by its names or by a root the key lists.</summary>
	private static bool IsThrowHelperCall(ExpressionStatementSyntax statement, StanzaOptions options)
	{
		if (BlankLineExpressions.FindInvocation(statement.Expression) is not { } invocation)
			return false;

		ExpressionSyntax callee = BlankLineExpressions.GetRootInvocation(invocation).Expression;

		return IsFrameworkThrow(callee) || BlankLineExpressions.FindRoot(callee, options.GuardRoots) is not null;
	}

	private static bool IsFrameworkThrow(ExpressionSyntax callee)
	{
		return callee is MemberAccessExpressionSyntax access
			&& access.Name.Identifier.ValueText.StartsWith("Throw", StringComparison.Ordinal)
			&& ReadReceiverName(access.Expression)?.EndsWith("Exception", StringComparison.Ordinal) == true;
	}

	private static string? ReadReceiverName(ExpressionSyntax receiver)
	{
		return receiver switch
		{
			SimpleNameSyntax name => name.Identifier.ValueText,
			MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
			_ => null,
		};
	}
}
