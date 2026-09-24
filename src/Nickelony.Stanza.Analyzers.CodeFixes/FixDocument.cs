using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

/// <summary>The document a fix reads, with its syntax root and text fetched once.</summary>
internal readonly record struct FixDocument(Document Document, SyntaxNode Root, SourceText Text);
