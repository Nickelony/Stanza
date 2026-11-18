using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Reads the <c>disable</c> and <c>restore</c> pragma pairs between the units of one container.</summary>
internal static class SuppressionBlocks
{
	/// <summary>Whether the directive is the <c>disable</c> of a pair.</summary>
	/// <remarks>
	/// A pair names the same codes in both directives, and a pragma with no pair or outside the units stays as written.
	/// </remarks>
	public static bool IsOpener(DirectiveTriviaSyntax directive) => GetBlocks(directive.SyntaxTree).Openers.Contains(directive.SpanStart);

	/// <summary>Whether the directive is the <c>restore</c> of a pair.</summary>
	public static bool IsCloser(DirectiveTriviaSyntax directive) => GetBlocks(directive.SyntaxTree).Closers.Contains(directive.SpanStart);

	private static Blocks GetBlocks(SyntaxTree syntaxTree) => s_blocks.GetOrCompute(syntaxTree, static tree => ReadBlocks(tree));

	/// <summary>Pairs each <c>restore</c> with the latest open <c>disable</c> of the same codes.</summary>
	private static Blocks ReadBlocks(SyntaxTree syntaxTree)
	{
		HashSet<int> openers = [];
		HashSet<int> closers = [];

		List<(PragmaWarningDirectiveTriviaSyntax Directive, string Codes)> open = [];

		// The callers pass no token, and the analyzers have read the same cached markers with theirs first.
		foreach (SyntaxTrivia trivia in StanzaCommentCache.GetPragmaMarkers(syntaxTree, CancellationToken.None))
		{
			// The compiler ignores a directive in a branch the build leaves out, so it opens and closes nothing.
			if (trivia.GetStructure() is not PragmaWarningDirectiveTriviaSyntax { IsActive: true } pragma)
				continue;

			string codes = ReadCodes(pragma);

			if (pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
			{
				open.Add((pragma, codes));
				continue;
			}

			int index = open.FindLastIndex(candidate => candidate.Codes == codes);

			if (index < 0)
				continue;

			PragmaWarningDirectiveTriviaSyntax opener = open[index].Directive;
			open.RemoveAt(index);

			// A pair is a block only where both directives stand between the units of one container.
			if (FindContainer(opener) is { } container && FindContainer(pragma) == container)
			{
				openers.Add(opener.SpanStart);
				closers.Add(pragma.SpanStart);
			}
		}

		return new Blocks(openers, closers);
	}

	private static SyntaxNode? FindContainer(DirectiveTriviaSyntax directive) => ConditionalBlocks.FindUnitContainer(directive.ParentTrivia.Token);

	/// <summary>Reads the codes a pragma names, in a fixed order; a pragma with none names every warning.</summary>
	private static string ReadCodes(PragmaWarningDirectiveTriviaSyntax pragma)
	{
		IEnumerable<string> codes = pragma.ErrorCodes.Select(static code => NormalizeCode(ReadCode(code)));
		return string.Join(",", codes.OrderBy(static code => code, System.StringComparer.Ordinal));
	}

	/// <summary>Reads a code as <c>StanzaPragmas</c> does, so a Unicode escape in it reads as the character.</summary>
	private static string ReadCode(ExpressionSyntax code) =>
		code is IdentifierNameSyntax identifier ? identifier.Identifier.ValueText : code.ToString().Trim();

	/// <summary>Reads a number as the compiler does: <c>618</c> is <c>CS0618</c>.</summary>
	private static string NormalizeCode(string code)
	{
		bool isNumber = code.Length > 0 && code.All(static character => character is >= '0' and <= '9');

		if (isNumber && int.TryParse(s: code, style: NumberStyles.None, provider: CultureInfo.InvariantCulture, result: out int number))
			return "CS" + number.ToString(format: "D4", provider: CultureInfo.InvariantCulture);

		return code;
	}

	private static readonly WeakCache<SyntaxTree, Blocks> s_blocks = new();

	private sealed record Blocks(HashSet<int> Openers, HashSet<int> Closers);
}
