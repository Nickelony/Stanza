using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

/// <summary>Bridges the markup engine to the analyzers.</summary>
internal static class MarkupLiteralAnalysis
{
	/// <summary>
	/// Parses a literal once for the markup rules, so this repository's tests take 35 ms, not 267 ms.
	/// </summary>
	private static readonly WeakCache<string, ParsedLiteral> s_parses = new();

	private static readonly ImmutableArray<DiagnosticDescriptor> s_tightRules =
	[
		DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether,
		DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether,
	];

	private static MarkupFindings Measure(string text, MarkupOptions markup, CancellationToken cancellationToken)
	{
		ParsedLiteral parsed = s_parses.GetOrCompute(
			text,
			state: cancellationToken,

			static (value, token) =>
			{
				var sourceText = MarkupSourceText.Build(value);
				return new ParsedLiteral(sourceText, MarkupAnalysis.Parse(sourceText, MarkupLimits.DefaultMaxDocumentCharacters, token));
			}
		);

		return MarkupAnalysis.Measure(sourceText: parsed.SourceText, parse: parsed.Parse, options: markup, skipsMembers: false, cancellationToken);
	}

	private static Location BuildLineLocation(SourceText sourceText, SyntaxTree syntaxTree, int lineIndex)
	{
		TextLine textLine = sourceText.Lines[lineIndex < 0 ? 0 : Math.Min(lineIndex, sourceText.Lines.Count - 1)];
		return Location.Create(syntaxTree, textLine.Span);
	}

	public static void ReportSeparations(SyntaxNodeAnalysisContext context, StanzaRule rule)
	{
		if (MarkupLiteral.Read(context.Node) is not { } literal)
			return;

		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);
		ImmutableArray<MarkupGap> gaps = ReadGaps(literal.Value, options.Markup, context.CancellationToken);

		if (!gaps.IsEmpty)
			ReportGaps(context, rule, literal, options, gaps: gaps.Select(static gap => (gap, (FixOptions?)null)));
	}

	public static void ReportRemovals(SyntaxNodeAnalysisContext context, StanzaRule rule)
	{
		if (MarkupLiteral.Read(context.Node) is not { } literal)
			return;

		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);

		// A tight rule the file enables owns the whole gap, so the layout hands it the gaps above the cap too.
		MarkupOptions markup = options.Markup with
		{
			Rules = GetEnabledTightRules(context, options, rule, position: literal.Token.SpanStart),
		};

		ImmutableArray<MarkupRemoval> removals = ReadRemovals(
			text: literal.Value,
			markup,
			id: rule.Descriptor.Id,
			cancellationToken: context.CancellationToken
		);

		if (!removals.IsEmpty)
		{
			ReportGaps(
				context,
				rule,
				literal,
				options,
				gaps: removals.Select(static removal => (removal.Gap, (FixOptions?)new FixOptions(KeptBlankLines: removal.KeptBlankLines)))
			);
		}
	}

	/// <summary>Reports each comment marker with no space before its text, at the marker alone.</summary>
	public static void ReportCommentMarkers(SyntaxNodeAnalysisContext context, DiagnosticDescriptor descriptor)
	{
		if (MarkupLiteral.Read(context.Node) is not { } literal)
			return;

		SyntaxToken token = literal.Token;
		SyntaxTree syntaxTree = context.Node.SyntaxTree;
		SourceText sourceText = syntaxTree.GetText(context.CancellationToken);

		// The closing quotes sit on the line after the value.
		TextLine lastLine = sourceText.Lines[literal.LastValueLine + 1];

		// The compiler strips the indentation before the closing quotes from every value line,
		// so an offset of the value sits that many characters into the file line.
		// A UTF-8 literal's suffix follows the closing quotes,
		// so the quote run and the suffix both come off the token end.
		int indentationLength = token.Span.End - GetRawStringDelimiterWidth(sourceText, token) - GetRawStringSuffixWidth(token) - lastLine.Start;

		var options = StanzaOptions.From(context.Options, syntaxTree);

		ImmutableArray<MarkupCommentMarker> markers = ReadCommentMarkers(
			text: literal.Value,
			markup: options.Markup,
			cancellationToken: context.CancellationToken
		);

		MarkupValueLines lines = new(literal.Value);

		foreach (MarkupCommentMarker marker in markers)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			(int fileLineIndex, int fileLineOffset) = lines.GetFileLineAndOffset(
				lineIndex: marker.LineIndex,
				offsetInLine: marker.MarkerOffset,
				indentationLength
			);

			int lineIndex = literal.FirstValueLine + fileLineIndex;

			if (lineIndex >= sourceText.Lines.Count)
				continue;

			TextLine textLine = sourceText.Lines[lineIndex];

			var span = new TextSpan(textLine.Start + fileLineOffset, marker.Length);

			if (span.End > textLine.End)
				continue;

			// The report concerns one marker and carries no partner location, unlike the gap reports of the other markup rules.
			context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.Create(syntaxTree, span)));
		}
	}

	/// <summary>Reports each gap on the line after it, with the line before it as the location its fix reads.</summary>
	private static void ReportGaps(
		SyntaxNodeAnalysisContext context,
		StanzaRule rule,
		MarkupLiteral literal,
		StanzaOptions options,
		IEnumerable<(MarkupGap Gap, FixOptions? FixOptions)> gaps
	)
	{
		// A pair shares a gap, so a file that enables both halves leaves it to one of them:
		// this rule reports only the gaps it owns. A rule that shares no gap owns all it is asked about.
		if (!StanzaRuleState.OwnsTheSharedGap(scope: context, options, rule, position: literal.Token.SpanStart))
			return;

		SyntaxTree syntaxTree = context.Node.SyntaxTree;
		SourceText sourceText = syntaxTree.GetText(context.CancellationToken);
		MarkupValueLines lines = new(literal.Value);

		foreach ((MarkupGap gap, FixOptions? fixOptions) in gaps)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			int nextLine = literal.FirstValueLine + lines.GetFileLineIndex(gap.NextStartLineIndex);
			int previousLine = literal.FirstValueLine + lines.GetFileLineIndex(gap.PreviousEndLineIndex);

			if (nextLine > literal.LastValueLine)
				continue;

			context.ReportDiagnostic(
				Diagnostic.Create(
					descriptor: rule.Descriptor,
					location: BuildLineLocation(sourceText, syntaxTree, nextLine),
					additionalLocations: [BuildLineLocation(sourceText, syntaxTree, previousLine)],
					properties: fixOptions?.ToProperties()
				)
			);
		}
	}

	/// <summary>
	/// Leaves out a tight rule that is off over the literal, which owns nothing there, so the cap keeps the gaps.
	/// </summary>
	private static MarkupRuleSet GetEnabledTightRules(SyntaxNodeAnalysisContext context, StanzaOptions options, StanzaRule rule, int position)
	{
		// Each rule reads the other tight rule through its own entry, and the cap reads both through its own.
		StanzaRule[] tightRules = [.. s_tightRules.Select(tight => tight == rule.Descriptor ? rule : rule.GetDeferral(tight))];

		return MarkupRuleSet.From(
			tightRules
				.Where(tight =>
					StanzaRuleState.IsEnabledByConfig(scope: context, tight)
					&& !StanzaRuleState.IsOffAt(scope: context, options, rule: tight, position)
				)
				.Select(static tight => tight.Descriptor.Id)
		);
	}

	public static ImmutableArray<MarkupGap> ReadGaps(string text, MarkupOptions markup, CancellationToken cancellationToken) =>
		[.. Measure(text, markup, cancellationToken).Gaps];

	public static ImmutableArray<MarkupRemoval> ReadRemovals(string text, MarkupOptions markup, string id, CancellationToken cancellationToken) =>
		[.. Measure(text, markup, cancellationToken).Removals.Where(removal => string.Equals(removal.Id, id, StringComparison.Ordinal))];

	public static ImmutableArray<MarkupCommentMarker> ReadCommentMarkers(string text, MarkupOptions markup, CancellationToken cancellationToken) =>
		[.. Measure(text, markup, cancellationToken).CommentMarkers];

	private sealed record ParsedLiteral(MarkupSourceText SourceText, MarkupParse Parse);

	/// <summary>Measures the quote run back from the token end, past the <c>u8</c> suffix.</summary>
	public static int GetRawStringDelimiterWidth(SourceText sourceText, SyntaxToken token)
	{
		int end = token.Span.End - GetRawStringSuffixWidth(token);
		int width = 0;

		for (int position = end - 1; position >= token.SpanStart && sourceText[position] == '"'; position--)
			width++;

		return width;
	}

	public static int GetRawStringSuffixWidth(SyntaxToken token) => token.IsKind(SyntaxKind.Utf8MultiLineRawStringLiteralToken) ? 2 : 0;
}
