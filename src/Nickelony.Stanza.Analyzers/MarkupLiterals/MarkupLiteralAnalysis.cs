using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

/// <summary>Bridges the markup engine to the analyzers.</summary>
internal static class MarkupLiteralAnalysis
{
	/// <summary>Parses a literal once for the markup rules, however many of them read its text.</summary>
	private static readonly WeakCache<string, ParsedLiteral> s_parses = new();

	private static readonly ImmutableArray<DiagnosticDescriptor> s_removalOwners =
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

	/// <summary>Reads the literal and its file's options, or <see langword="null"/> when the rules skip it.</summary>
	private static (MarkupLiteral Literal, StanzaOptions Options)? ReadLiteral(SyntaxNodeAnalysisContext context)
	{
		// Most string literals are no raw string, so the kind is checked before the options are read.
		if (!StanzaKinds.IsMarkupLiteralToken(((LiteralExpressionSyntax)context.Node).Token))
			return null;

		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);
		return MarkupLiteral.Read(context, options.MarkupLiterals) is { } literal ? (literal, options) : null;
	}

	private static Location BuildLineLocation(SourceText sourceText, SyntaxTree syntaxTree, int lineIndex)
	{
		TextLine textLine = sourceText.Lines[lineIndex < 0 ? 0 : Math.Min(lineIndex, sourceText.Lines.Count - 1)];
		return Location.Create(syntaxTree, textLine.Span);
	}

	public static void ReportSeparations(SyntaxNodeAnalysisContext context, StanzaRule rule) => ReportSeparations(context, rule, ReadGaps);

	public static void ReportCommentedSeparations(SyntaxNodeAnalysisContext context, StanzaRule rule) =>
		ReportSeparations(context, rule, ReadCommentedGaps);

	private static void ReportSeparations(
		SyntaxNodeAnalysisContext context,
		StanzaRule rule,
		Func<string, MarkupOptions, CancellationToken, ImmutableArray<MarkupGap>> read
	)
	{
		if (ReadLiteral(context) is not (MarkupLiteral literal, StanzaOptions options))
			return;

		// A rule that defers a gap to a tight rule is told which of them stand, so the layout leaves that rule the gap.
		MarkupOptions markup = rule.DefersTo.IsDefault
			? options.Markup
			: options.Markup with
			{
				Rules = GetEnabledRemovalOwners(context, options, rule, position: literal.Token.SpanStart),
			};

		ImmutableArray<MarkupGap> gaps = read(literal.Value, markup, context.CancellationToken);

		if (!gaps.IsEmpty)
			ReportGaps(context, rule, literal, options, gaps: gaps.Select(static gap => (gap, (FixOptions?)null)));
	}

	public static void ReportRemovals(SyntaxNodeAnalysisContext context, StanzaRule rule)
	{
		// A tight rule the file enables owns the whole gap, so the layout hands it the gaps above the cap too.
		ReportRemovalGaps(
			context,
			rule,
			rulesFor: (literal, options) => GetEnabledRemovalOwners(context, options, rule, position: literal.Token.SpanStart)
		);
	}

	/// <summary>Reports each comment marker with no space before its text, at the marker alone.</summary>
	public static void ReportCommentMarkers(SyntaxNodeAnalysisContext context, DiagnosticDescriptor descriptor)
	{
		if (ReadLiteral(context) is not (MarkupLiteral literal, StanzaOptions options))
			return;

		ImmutableArray<MarkupCommentMarker> markers = ReadCommentMarkers(
			text: literal.Value,
			markup: options.Markup,
			cancellationToken: context.CancellationToken
		);

		ReportAtLineOffsets(
			context,
			descriptor,
			literal,
			spans: markers.Select(static marker => (marker.LineIndex, marker.MarkerOffset, marker.Length))
		);
	}

	/// <summary>Reports each slash whose gap the shape does not accept, at the slash alone.</summary>
	public static void ReportSlashGaps(SyntaxNodeAnalysisContext context, StanzaRule rule, bool spaced)
	{
		if (ReadLiteral(context) is not (MarkupLiteral literal, StanzaOptions options))
			return;

		// The pair shares each slash, so a file that enables both halves leaves it to the owner.
		if (!StanzaRuleState.IsTheOwningHalf(scope: context, options, rule, position: literal.Token.SpanStart))
			return;

		MarkupOptions markup = options.Markup with { Rules = MarkupRuleSet.From([rule.Descriptor.Id]) };

		ImmutableArray<MarkupSlashGap> slashGaps =
		[
			.. Measure(literal.Value, markup, context.CancellationToken).SlashGaps.Where(gap => gap.NeedsChange(spaced)),
		];

		ReportAtLineOffsets(
			context,
			descriptor: rule.Descriptor,
			literal,
			spans: slashGaps.Select(static gap => (gap.LineIndex, gap.SlashOffset, Length: MarkupSlashGap.SlashLength))
		);
	}

	/// <summary>Reports the bracket and the end tag of each empty element, with the shape its fix writes.</summary>
	public static void ReportEmptyElements(SyntaxNodeAnalysisContext context, StanzaRule rule)
	{
		if (ReadLiteral(context) is not (MarkupLiteral literal, StanzaOptions options))
			return;

		MarkupOptions markup = options.Markup with { Rules = MarkupRuleSet.From([rule.Descriptor.Id]) };

		ImmutableArray<MarkupEmptyElement> elements = [.. Measure(literal.Value, markup, context.CancellationToken).EmptyElements];

		if (elements.IsEmpty)
			return;

		// The tag follows the slash pair: the tight half decides only when it owns the gap there.
		bool spaced = !StanzaRuleState.IsTheOwningHalf(
			scope: context,
			options,
			rule: rule.GetDeferral(DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight),
			position: literal.Token.SpanStart
		);

		ReportAtLineOffsets(
			context,
			descriptor: rule.Descriptor,
			literal,
			spans: elements.Select(static element => (element.LineIndex, element.BracketOffset, element.Length)),
			fixOptions: new FixOptions(SpacedSlash: spaced)
		);
	}

	/// <summary>Reports the blank lines inside an element, on the line after them.</summary>
	public static void ReportPadding(SyntaxNodeAnalysisContext context, StanzaRule rule) =>
		ReportRemovalGaps(context, rule, rulesFor: (_, _) => MarkupRuleSet.From([rule.Descriptor.Id]));

	/// <summary>Reports the rule's removals, read with the rules <paramref name="rulesFor"/> picks.</summary>
	private static void ReportRemovalGaps(
		SyntaxNodeAnalysisContext context,
		StanzaRule rule,
		Func<MarkupLiteral, StanzaOptions, MarkupRuleSet> rulesFor
	)
	{
		if (ReadLiteral(context) is not (MarkupLiteral literal, StanzaOptions options))
			return;

		MarkupOptions markup = options.Markup with { Rules = rulesFor(literal, options) };

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

	/// <summary>Reports a span of the literal's value that a line and an offset of that line name.</summary>
	private static void ReportAtLineOffsets(
		SyntaxNodeAnalysisContext context,
		DiagnosticDescriptor descriptor,
		MarkupLiteral literal,
		IEnumerable<(int LineIndex, int Offset, int Length)> spans,
		FixOptions? fixOptions = null
	)
	{
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

		MarkupValueLines lines = new(literal.Value);

		foreach ((int valueLineIndex, int offset, int length) in spans)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			(int fileLineIndex, int fileLineOffset) = lines.GetFileLineAndOffset(lineIndex: valueLineIndex, offsetInLine: offset, indentationLength);
			int lineIndex = literal.FirstValueLine + fileLineIndex;

			if (lineIndex >= sourceText.Lines.Count)
				continue;

			TextLine textLine = sourceText.Lines[lineIndex];

			var span = new TextSpan(textLine.Start + fileLineOffset, length);

			if (span.End > textLine.End)
				continue;

			// The report concerns one span and carries no partner location, unlike the gap reports of the other markup rules.
			context.ReportDiagnostic(
				Diagnostic.Create(
					descriptor,
					location: Location.Create(syntaxTree, span),
					additionalLocations: null,
					properties: fixOptions?.ToProperties()
				)
			);
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
		if (!StanzaRuleState.IsTheOwningHalf(scope: context, options, rule, position: literal.Token.SpanStart))
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
	/// Leaves out a rule that is off over the literal, which owns nothing there, so the cap keeps the gaps.
	/// </summary>
	private static MarkupRuleSet GetEnabledRemovalOwners(SyntaxNodeAnalysisContext context, StanzaOptions options, StanzaRule rule, int position)
	{
		// Each rule reads the others through its own entry, and skips one it does not defer to:
		// the multi-line tight rule owns no gap that the commented rule shares. The cap reads every one through its own.
		StanzaRule[] owners =
		[
			.. s_removalOwners
				.Where(owner => owner == rule.Descriptor || rule.Defers(owner))
				.Select(owner => owner == rule.Descriptor ? rule : rule.GetDeferral(owner)),
		];

		return MarkupRuleSet.From(
			owners
				.Where(owner =>
					StanzaRuleState.IsEnabledByConfig(scope: context, owner)
					&& !StanzaRuleState.IsOffAt(scope: context, options, rule: owner, position)
				)
				.Select(static owner => owner.Descriptor.Id)
		);
	}

	public static ImmutableArray<MarkupGap> ReadGaps(string text, MarkupOptions markup, CancellationToken cancellationToken) =>
		[.. Measure(text, markup, cancellationToken).Gaps];

	public static ImmutableArray<MarkupGap> ReadCommentedGaps(string text, MarkupOptions markup, CancellationToken cancellationToken) =>
		[.. Measure(text, markup, cancellationToken).CommentedGaps];

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
