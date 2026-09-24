using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Arguments;

/// <summary>Builds the text changes that add or remove argument names.</summary>
internal static class ArgumentEdits
{
	public static ImmutableArray<TextChange> CreateNameInsertions(SyntaxNode root, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		ImmutableArray<(int Index, string Name)> names = FixOptions.Parse(diagnostic).ArgumentNames;

		if (
			names.IsDefaultOrEmpty
			|| root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<BaseArgumentListSyntax>() is not { } argumentList
			|| argumentList.Span != diagnostic.Location.SourceSpan
		)
		{
			return [];
		}

		ImmutableArray<TextChange>.Builder changes = ImmutableArray.CreateBuilder<TextChange>(names.Length);

		// An index past the list or an argument that has a name now means the call changed since the report, so nothing is written.
		foreach ((int index, string name) in names)
		{
			if (index >= argumentList.Arguments.Count || argumentList.Arguments[index] is not { NameColon: null } argument)
				return [];

			changes.Add(new TextChange(new TextSpan(argument.SpanStart, 0), FormatName(name)));
		}

		return changes.MoveToImmutable();
	}

	public static ImmutableArray<TextChange> CreateNameRemovals(
		SyntaxNode root,
		SourceText sourceText,
		Diagnostic diagnostic,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (
			root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<NameColonSyntax>() is not { } nameColon
			|| nameColon.Parent is not ArgumentSyntax argument
			|| nameColon.Span != diagnostic.Location.SourceSpan
		)
		{
			return [];
		}

		if (ArgumentBinding.AllowsNamesBeforePositions(argument.SyntaxTree) || argument.Parent is not BaseArgumentListSyntax argumentList)
			return CreateRemovals(sourceText, nameColon, argument);

		// Before C# 7.2 a named argument has to come after every positional one,
		// so the fix also removes each name before its own, which the rule reports in the same run.
		ImmutableArray<TextChange>.Builder changes = ImmutableArray.CreateBuilder<TextChange>();

		foreach (ArgumentSyntax earlier in argumentList.Arguments.TakeWhile(earlier => earlier != argument))
		{
			if (earlier.NameColon is { } earlierName)
				changes.AddRange(CreateRemovals(sourceText, earlierName, earlier));
		}

		changes.AddRange(CreateRemovals(sourceText, nameColon, argument));

		return changes.ToImmutable();
	}

	private static ImmutableArray<TextChange> CreateRemovals(SourceText sourceText, NameColonSyntax nameColon, ArgumentSyntax argument)
	{
		// A `ref`, `out` or `in` argument writes its keyword before the expression,
		// so the argument starts at the keyword rather than at the expression.
		int contentStart = argument.RefKindKeyword.IsKind(SyntaxKind.None) ? argument.Expression.SpanStart : argument.RefKindKeyword.SpanStart;

		var gap = TextSpan.FromBounds(nameColon.ColonToken.Span.End, contentStart);
		bool gapIsWhitespace = StanzaText.IsWhitespaceOnly(sourceText, gap);

		if (gapIsWhitespace && StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(nameColon.Name.Span.End, nameColon.ColonToken.SpanStart)))
			return [new TextChange(TextSpan.FromBounds(nameColon.SpanStart, contentStart), string.Empty)];

		// A comment keeps its place, and the colon takes one space beside it with it,
		// so the comment ends up beside what stood before the name rather than a space away from it.
		TextSpan colon = nameColon.ColonToken.Span;

		TextSpan colonRemoval = gapIsWhitespace
			? ReadSpanThroughPrecedingSpaces(sourceText, colon, limit: nameColon.Name.Span.End)
			: ReadSpanThroughFollowingSpaces(sourceText, colon, limit: contentStart);

		return
		[
			new TextChange(
				TextSpan.FromBounds(
					nameColon.Name.SpanStart,
					StanzaText.SkipWhitespace(sourceText, nameColon.Name.Span.End, nameColon.ColonToken.SpanStart)
				),

				string.Empty
			),

			new TextChange(colonRemoval, string.Empty),
		];
	}

	private static TextSpan ReadSpanThroughFollowingSpaces(SourceText sourceText, TextSpan span, int limit)
	{
		int end = span.End;

		while (end < limit && sourceText[end] is ' ' or '\t')
			end++;

		return TextSpan.FromBounds(span.Start, end);
	}

	private static TextSpan ReadSpanThroughPrecedingSpaces(SourceText sourceText, TextSpan span, int limit)
	{
		int start = span.Start;

		while (start > limit && sourceText[start - 1] is ' ' or '\t')
			start--;

		return TextSpan.FromBounds(start, span.End);
	}

	private static string FormatName(string name) => $"{ArgumentBinding.EscapeName(name)}: ";
}
