using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

internal readonly struct BraceTarget(TextSpan headerSpan, StatementSyntax body, bool isMultiLine)
{
	public TextSpan HeaderSpan { get; } = headerSpan;
	public StatementSyntax Body { get; } = body;
	public bool IsMultiLine { get; } = isMultiLine;

	public BlockSyntax? Block => Body as BlockSyntax;
}
