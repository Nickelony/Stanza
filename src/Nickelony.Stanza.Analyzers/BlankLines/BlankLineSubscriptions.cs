using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the runs of event subscriptions, which bind a semantic model while ZA0007 is on.</summary>
/// <remarks>
/// Only the symbol tells <c>Click += OnClick;</c> from <c>count += step;</c>.
/// </remarks>
internal static class BlankLineSubscriptions
{
	private static readonly WeakCache<SemanticModel, StatementReads<bool>> s_reads = new();

	/// <summary>Reads the run borders of the subscriptions among the siblings.</summary>
	public static ImmutableArray<BlankLineGrouping.RunBorder> ReadBorders(
		ContainerContext container,
		GapOwners gapOwners,
		BlankLineGrouping.GatheredStretch? gathered
	)
	{
		if (CountCandidates(container.Siblings, gathered) < 2)
			return [];

		return BlankLineGrouping.ReadSubscriptionRunBorders(
			siblings: container.Siblings,
			options: container.Options,
			isSubscription: GetReader(container),
			gapOwners,
			gathered,
			cancellationToken: container.CancellationToken
		);
	}

	/// <summary>Gets the reader that tells a subscription from any other statement.</summary>
	public static Func<SyntaxNode, bool> GetReader(ContainerContext container) =>
		GetReads(container.Context.SemanticModel).GetReader(container.CancellationToken);

	/// <summary>Gets the answers of a model, so a statement is bound once however often it is read.</summary>
	public static StatementReads<bool> GetReads(SemanticModel semanticModel) =>
		s_reads.GetOrCompute(semanticModel, static model => new StatementReads<bool>(model, TryReadSubscription));

	/// <summary>Counts the statements that look like a subscription before any symbol is read.</summary>
	/// <remarks>
	/// A read of a stretch counts the stretch alone, so the rounds that gather a long block stay linear.
	/// </remarks>
	private static int CountCandidates(ImmutableArray<SyntaxNode> siblings, BlankLineGrouping.GatheredStretch? gathered)
	{
		int count = 0;

		// A stretch that continues above counts the sibling above it too, since the runs that cross its first gap hold that one.
		int first = (gathered?.Start ?? 0) - (gathered is { ContinuesAbove: true } ? 1 : 0);

		for (int index = first; index < (gathered?.End ?? siblings.Length); index++)
		{
			if (FindTarget(siblings[index]) is not null)
				count++;
		}

		return count;
	}

	/// <summary>Finds the name a <c>+=</c> or <c>-=</c> statement assigns to, if it could name an event.</summary>
	private static ExpressionSyntax? FindTarget(SyntaxNode node)
	{
		if (node is not ExpressionStatementSyntax { Expression: { } expression })
			return null;

		// A `?.` that guards the assignment holds it, as in `button?.Click += OnClick;`, so it is read off what the access guards.
		expression = BlankLineExpressions.UnwrapConditionalAccess(expression);

		if (
			expression
				is AssignmentExpressionSyntax
				{
					RawKind: (int)SyntaxKind.AddAssignmentExpression or (int)SyntaxKind.SubtractAssignmentExpression,
				} assignment
			&& assignment.Left is IdentifierNameSyntax or MemberAccessExpressionSyntax or MemberBindingExpressionSyntax
		)
		{
			return assignment.Left;
		}

		return null;
	}

	/// <summary>Whether the statement subscribes to an event, or declines one that names none.</summary>
	private static bool TryReadSubscription(
		SemanticModel semanticModel,
		SyntaxNode statement,
		CancellationToken cancellationToken,
		out bool isSubscription
	)
	{
		isSubscription = false;

		if (FindTarget(statement) is not { } target)
			return false;

		isSubscription = semanticModel.GetSymbolInfo(target, cancellationToken).Symbol is IEventSymbol;
		return true;
	}
}
