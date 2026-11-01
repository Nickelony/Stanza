using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Caches a tree's comments, documentation comments and directive markers, from one trivia walk.</summary>
internal static class StanzaCommentCache
{
	public static bool HasDocumentationComment(SyntaxTree syntaxTree, CancellationToken cancellationToken) =>
		ReadTriviaMarkers(syntaxTree, cancellationToken).HasDocumentationComment;

	/// <summary>Scans the file's text on each call, so a rule can skip a file before the trivia walk.</summary>
	/// <remarks>
	/// A marker may be a false alarm, as a <c>//</c> in a string is, but never a miss.
	/// </remarks>
	private static TriviaMarkers ReadTriviaMarkers(SyntaxTree syntaxTree, CancellationToken cancellationToken)
	{
		SourceText sourceText = syntaxTree.GetText(cancellationToken);

		bool documentationComment = false;
		bool comment = false;

		for (int position = 0; position + 1 < sourceText.Length; position++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (sourceText[position] != '/')
				continue;

			char opener = sourceText[position + 1];

			if (opener is not ('/' or '*'))
				continue;

			comment = true;

			// The third character of `///` and `/**` repeats the opener,
			// which marks a possible documentation comment; `////` and `/**/` match too.
			if (position + 2 < sourceText.Length && sourceText[position + 2] == opener)
				documentationComment = true;

			if (documentationComment)
				break;
		}

		return new TriviaMarkers(documentationComment, comment);
	}

	public static ImmutableArray<SyntaxTrivia> GetComments(SyntaxTree syntaxTree, CancellationToken cancellationToken) =>
		GetTriviaSnapshot(syntaxTree, cancellationToken).Comments;

	public static ImmutableArray<SyntaxTrivia> GetDocumentationComments(SyntaxTree syntaxTree, CancellationToken cancellationToken) =>
		GetTriviaSnapshot(syntaxTree, cancellationToken).DocumentationComments;

	public static ImmutableArray<SyntaxTrivia> GetRegionMarkers(SyntaxTree syntaxTree, CancellationToken cancellationToken) =>
		GetTriviaSnapshot(syntaxTree, cancellationToken).RegionMarkers;

	/// <summary>The <c>#if</c> and <c>#endif</c> directives of a tree, in active and inactive code.</summary>
	public static ImmutableArray<SyntaxTrivia> GetConditionalBlockMarkers(SyntaxTree syntaxTree, CancellationToken cancellationToken) =>
		GetTriviaSnapshot(syntaxTree, cancellationToken).ConditionalBlockMarkers;

	private static TriviaSnapshot GetTriviaSnapshot(SyntaxTree syntaxTree, CancellationToken cancellationToken) =>
		s_triviaCache.GetOrCompute(syntaxTree, state: cancellationToken, static (tree, token) => ReadTriviaSnapshot(tree, token));

	private static TriviaSnapshot ReadTriviaSnapshot(SyntaxTree syntaxTree, CancellationToken cancellationToken)
	{
		SyntaxNode root = syntaxTree.GetRoot(cancellationToken);

		// Whether a file has a comment is read from its text without a walk, and Roslyn keeps a flag for directives,
		// so a file that carries neither is answered with no walk at all.
		if (!root.ContainsDirectives && !ReadTriviaMarkers(syntaxTree, cancellationToken).HasComment)
			return new TriviaSnapshot(Comments: [], DocumentationComments: [], RegionMarkers: [], ConditionalBlockMarkers: []);

		ImmutableArray<SyntaxTrivia>.Builder comments = ImmutableArray.CreateBuilder<SyntaxTrivia>();
		ImmutableArray<SyntaxTrivia>.Builder documentationComments = ImmutableArray.CreateBuilder<SyntaxTrivia>();
		ImmutableArray<SyntaxTrivia>.Builder regionMarkers = ImmutableArray.CreateBuilder<SyntaxTrivia>();
		ImmutableArray<SyntaxTrivia>.Builder conditionalBlockMarkers = ImmutableArray.CreateBuilder<SyntaxTrivia>();

		foreach (SyntaxTrivia trivia in root.DescendantTrivia())
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (StanzaTrivia.IsComment(trivia))
			{
				comments.Add(trivia);

				if (StanzaTrivia.IsDocumentationComment(trivia))
					documentationComments.Add(trivia);

				continue;
			}

			SyntaxNode? structure = trivia.GetStructure();

			if (structure is RegionDirectiveTriviaSyntax or EndRegionDirectiveTriviaSyntax)
				regionMarkers.Add(trivia);

			if (structure is IfDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax)
				conditionalBlockMarkers.Add(trivia);

			if (structure is DirectiveTriviaSyntax directive && !CarriesAMessage(directive))
				CollectDirectiveComments(directive, comments);
		}

		return new TriviaSnapshot(
			Comments: comments.ToImmutable(),
			DocumentationComments: documentationComments.ToImmutable(),
			RegionMarkers: regionMarkers.ToImmutable(),
			ConditionalBlockMarkers: conditionalBlockMarkers.ToImmutable()
		);
	}

	/// <summary>Collects the comments on a directive line, which the trivia walk of the tree does not enter.</summary>
	private static void CollectDirectiveComments(DirectiveTriviaSyntax directive, ImmutableArray<SyntaxTrivia>.Builder comments)
	{
		foreach (SyntaxTrivia trivia in directive.DescendantTrivia())
		{
			if (StanzaTrivia.IsComment(trivia))
				comments.Add(trivia);
		}
	}

	/// <summary>Whether the directive's text is a message, such as a region name, that no comment rule edits.</summary>
	private static bool CarriesAMessage(DirectiveTriviaSyntax directive) =>
		directive is RegionDirectiveTriviaSyntax or EndRegionDirectiveTriviaSyntax or ErrorDirectiveTriviaSyntax or WarningDirectiveTriviaSyntax;

	/// <summary>One trivia walk per tree serves every comment rule, instead of one walk for each.</summary>
	private static readonly WeakCache<SyntaxTree, TriviaSnapshot> s_triviaCache = new();

	private readonly record struct TriviaMarkers(bool HasDocumentationComment, bool HasComment);

	private sealed record TriviaSnapshot(
		ImmutableArray<SyntaxTrivia> Comments,
		ImmutableArray<SyntaxTrivia> DocumentationComments,
		ImmutableArray<SyntaxTrivia> RegionMarkers,
		ImmutableArray<SyntaxTrivia> ConditionalBlockMarkers
	);
}
