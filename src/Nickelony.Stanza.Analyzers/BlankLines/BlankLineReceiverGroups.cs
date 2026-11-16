using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads consecutive selected operations by their written receiver or member owner.</summary>
internal static class BlankLineReceiverGroups
{
	public readonly record struct Groups(ImmutableHashSet<int> Inner, ImmutableArray<int> Outer, ImmutableHashSet<int> Sections);

	/// <summary>Caches one container scan shared by its gap claims and counterfactual run reads.</summary>
	public sealed class Reading
	{
		private Groups? _groups;
		public Groups Get(ContainerContext container) => _groups ??= ReadGroups(container);
	}

	public static Groups GetGroups(ContainerContext container) => container.ReceiverGroups?.Get(container) ?? new Groups([], [], []);
	public static ImmutableHashSet<int> GetSections(ContainerContext container) => container.ReceiverGroups?.Get(container).Sections ?? [];

	public static ImmutableHashSet<int> ReadAskedBoundaries(ContainerContext container, StanzaRule askingRule)
	{
		StanzaRule? owner =
			askingRule.Descriptor == DiagnosticDescriptors.SeparateReceiverGroups
				? askingRule
				: StanzaRuleState.FindJudgingDeferral(askingRule, DiagnosticDescriptors.SeparateReceiverGroups, container.Kind);

		if (owner is null || StanzaRuleState.IsRuleDisabled(container, container.Options, owner))
			return [];

		ImmutableArray<int> active =
		[
			.. GetGroups(container)
				.Outer.Where(boundary =>
					!StanzaRuleState.IsOffAt(
						scope: container,
						options: container.Options,
						rule: owner,
						position: container.Siblings[boundary].SpanStart
					)
				),
		];

		return
		[
			.. BlankLineKeptPairs
				.ReadOuterBoundaries(container, active)
				.Where(boundary =>
					!StanzaRuleState.IsOffAt(
						scope: container,
						options: container.Options,
						rule: owner,
						position: container.Siblings[boundary].SpanStart
					)
				),
		];
	}

	private static Groups ReadGroups(ContainerContext container)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		string?[] keys = new string?[siblings.Length];

		for (int index = 0; index < keys.Length; index++)
		{
			container.CancellationToken.ThrowIfCancellationRequested();
			keys[index] = FindKey(container, siblings[index]);
		}

		bool CanJoin(int boundary)
		{
			return boundary > 0
				&& boundary < keys.Length
				&& keys[boundary] is not null
				&& keys[boundary] == keys[boundary - 1]
				&& BlankLineKeptPairs.IsWhitespaceGap(siblings[boundary - 1], siblings[boundary], container.CancellationToken);
		}

		bool IsLoose(int boundary) => CanJoin(boundary) && BlankLineGaps.HasBlankLineBetween(siblings[boundary - 1], siblings[boundary]);

		ImmutableHashSet<int>.Builder inner = ImmutableHashSet.CreateBuilder<int>();
		ImmutableHashSet<int>.Builder sections = ImmutableHashSet.CreateBuilder<int>();

		StanzaRule keeper = StanzaRules.KeepReceiverGroupTogether;
		bool keepsSections = !StanzaRuleState.IsRuleDisabled(container, container.Options, keeper);

		for (int boundary = 1; boundary < keys.Length; boundary++)
		{
			if (!CanJoin(boundary))
				continue;

			if (IsLoose(boundary) && BlankLineSections.IsSectionBreak(container, boundary, isInside: CanJoin, isLoose: IsLoose))
			{
				if (
					keepsSections
					&& !StanzaRuleState.IsOffAt(scope: container, options: container.Options, rule: keeper, position: siblings[boundary].SpanStart)
				)
				{
					sections.Add(boundary);
				}
			}
			else
			{
				inner.Add(boundary);
			}
		}

		ImmutableHashSet<int> joined = inner.ToImmutable();
		ImmutableArray<int>.Builder outer = ImmutableArray.CreateBuilder<int>();

		void AddOuter(int boundary)
		{
			if (outer.Count == 0 || outer[outer.Count - 1] != boundary)
				outer.Add(boundary);
		}

		for (int start = 0; start < keys.Length; )
		{
			int end = start + 1;

			while (end < keys.Length && joined.Contains(end))
				end++;

			if (keys[start] is not null && end - start >= 2)
			{
				if (start > 0)
					AddOuter(start);

				if (end < keys.Length)
					AddOuter(end);
			}

			start = end;
		}

		return new Groups(joined, outer.ToImmutable(), sections.ToImmutable());
	}

	private static string? FindKey(ContainerContext container, SyntaxNode unit)
	{
		if (
			unit is not ExpressionStatementSyntax statement
			|| StanzaSyntax.IsMultiLine(unit)
			|| BlankLineReceiverCalls.IsJoinedStatement(unit)
			|| BlankLineGuards.IsGuard(unit, container.Options)
		)
		{
			return null;
		}

		if (BlankLineExpressions.FindInvocation(statement.Expression) is { } invocation)
		{
			ExpressionSyntax callee = BlankLineExpressions.GetRootInvocation(invocation).Expression;

			if (
				BlankLineExpressions.FindRoot(callee, container.Options.AssertionRoots) is not null
				|| BlankLineKeptPairs.ReadNarrowedNames(unit, container.Context.SemanticModel).Count > 0
			)
			{
				return null;
			}
		}

		return BlankLineReceiverCalls.FindConfiguredOwner(statement.Expression, container.Options);
	}
}
