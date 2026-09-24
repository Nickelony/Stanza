using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Documentation;

/// <summary>The indentation, line ending and delimiters a comment is written with.</summary>
internal readonly record struct DocCommentFrame(string Indentation, string LineEnding, DocCommentDelimiters Delimiters)
{
	/// <summary>Reads the frame of a comment from the text before its first marker.</summary>
	public static DocCommentFrame Read(SourceText sourceText, TextSpan prefix, DocumentationCommentTriviaSyntax comment)
	{
		return new DocCommentFrame(
			Indentation: sourceText.ToString(prefix),
			LineEnding: StanzaText.ReadPreferredLineEnding(sourceText, sourceText.Lines.GetLineFromPosition(prefix.Start).LineNumber),
			Delimiters: DocCommentSyntax.GetDelimiters(comment)
		);
	}
}
