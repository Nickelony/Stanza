using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class MergeNestedIfStatementsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.MergeNestedIfStatements)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var outer = (IfStatementSyntax)context.Node;

		if (outer.Else is not null)
			return;

		var options = StanzaOptions.From(context.Options, outer.SyntaxTree);
		ImmutableArray<IfStatementSyntax> merged = FindBoolMerges(context, outer, options);

		if (merged.IsEmpty)
			return;

		// The fix of the if around this one merges this one too, so the outer one goes first.
		if (NestedIfs.FindMergingParent(outer) is { } parent && !FindBoolMerges(context, parent, options).IsEmpty)
			return;

		// A merge further out dedents this stack, so a Fix All would keep only one of the two changes.
		if (HasMergeAround(context, outer, options))
			return;

		// A chain keeps the shape the braces rule gives it until that rule has nothing to add, so the two never undo each other.
		SourceText sourceText = outer.SyntaxTree.GetText(context.CancellationToken);

		if (IsWaitingForBraces(context, rule: Rule, sourceText, outer, merged))
			return;

		var fixOptions = new FixOptions(
			IndentStepColumns: options.IndentStepColumns,
			MergedIfs: merged.Length,
			SeparatesNeighbors: BraceEdits.IsSeparatingNeighbors(context, Rule, outer)
		);

		context.ReportDiagnostic(Diagnostic.Create(Descriptor, outer.IfKeyword.GetLocation(), properties: fixOptions.ToProperties()));
	}

	/// <summary>Finds the ifs that merge into the one given, up to one that is no <see cref="bool"/>.</summary>
	/// <remarks>
	/// A type may define <c>true</c> and <c>false</c> but no <c>&amp;</c>, so it cannot join with <c>&amp;&amp;</c>.
	/// </remarks>
	private static ImmutableArray<IfStatementSyntax> FindBoolMerges(SyntaxNodeAnalysisContext context, IfStatementSyntax outer, StanzaOptions options)
	{
		ImmutableArray<IfStatementSyntax> merged = NestedIfs.FindMerges(outer, options.MaxLogicalOperators);

		if (merged.IsEmpty || IsOfAnotherType(context, outer.Condition))
			return [];

		int count = 0;

		while (count < merged.Length && !IsOfAnotherType(context, merged[count].Condition))
			count++;

		return [.. merged.Take(count)];
	}

	/// <summary>Whether an if that holds the statement merges, as this rule reports it there.</summary>
	/// <remarks>
	/// An if a pragma silences never merges, so the stack inside it does not wait for it.
	/// </remarks>
	private bool HasMergeAround(SyntaxNodeAnalysisContext context, IfStatementSyntax statement, StanzaOptions options)
	{
		foreach (IfStatementSyntax around in statement.Ancestors().OfType<IfStatementSyntax>())
		{
			if (around.Else is not null || StanzaRuleState.IsOffAt(scope: context, options, rule: Rule, position: around.IfKeyword.SpanStart))
				continue;

			if (!FindBoolMerges(context, outer: around, options).IsEmpty)
				return true;
		}

		return false;
	}

	private static bool IsOfAnotherType(SyntaxNodeAnalysisContext context, ExpressionSyntax condition)
	{
		return context.SemanticModel.GetTypeInfo(condition, context.CancellationToken).Type
			is { TypeKind: not TypeKind.Error, SpecialType: not SpecialType.System_Boolean };
	}

	private static bool IsWaitingForBraces(
		SyntaxNodeAnalysisContext context,
		StanzaRule rule,
		SourceText sourceText,
		IfStatementSyntax outer,
		ImmutableArray<IfStatementSyntax> merged
	)
	{
		if (BraceEdits.IsWaitingForBraces(context, rule, sourceText, ifStatement: outer))
			return true;

		foreach (IfStatementSyntax inner in merged)
		{
			if (BraceEdits.IsWaitingForBraces(context, rule, sourceText, ifStatement: inner))
				return true;
		}

		return false;
	}
}
