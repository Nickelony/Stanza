using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateJumpStatementAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateJumpStatement)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(context.Node);

		// A stanza needs a sibling above the jump, and a container without a jump has none to judge.
		if (siblings.Length < 2 || BlankLineJumps.FindJumps(siblings).IsEmpty)
			return;

		var container = new ContainerContext(context, StanzaOptions.From(context.Options, context.Node.SyntaxTree), siblings);
		ImmutableArray<int> jumps = BlankLineJumps.FindJudgedJumps(container, jumpRule: Rule);

		if (jumps.IsEmpty)
			return;

		GapOwners gapOwners = BlankLineOwnership.ReadGapOwners(container, Rule);

		// The runs of declarations and calls own the boundaries they ask for, and end the stanza there.
		ImmutableHashSet<int> owned =
		[
			.. BlankLineOwnership
				.ReadGatheredGroupingBorders(container, rule: Rule, gapOwners, gathered: null)
				.Select(static border => border.Boundary),
		];

		// The guard rule, where it is on, sets the statement below the guards apart, so the stanza ends there too.
		// It leaves the gap above a jump to this rule, which reports it when the stanza above it spans two lines.
		int guardBoundary = BlankLineGuards.FindAskedGuardBoundary(container, askingRule: Rule);
		ImmutableHashSet<int> standing = guardBoundary < 0 ? owned : owned.Add(guardBoundary);

		RequiredBlankLineAnalysis.ReportGroupingBoundaries(
			container,
			boundaries: BlankLineJumps.ReadAskedJumpBoundaries(siblings, jumps, gapOwners, standing, cancellationToken: context.CancellationToken),
			rule: Rule,
			excludedBoundaries: owned
		);
	}
}
