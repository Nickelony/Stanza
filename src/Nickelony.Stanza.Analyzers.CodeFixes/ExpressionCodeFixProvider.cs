using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Nickelony.Stanza.Analyzers.Expressions;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExpressionCodeFixProvider)), Shared]
internal sealed class ExpressionCodeFixProvider : StanzaCodeFixProvider
{
	/// <summary>Each fix rewrites the syntax at the diagnostic, since the analyzer bound the operand already.</summary>
	private static readonly FixTable s_fixes = new([
		FixRow.For(
			DiagnosticDescriptors.UsePatternForNullCheck,
			"Use the pattern",
			FromSync(static (document, diagnostic) => NullCheckEdits.CreatePattern(document.Root, diagnostic.Location.SourceSpan))
		),

		FixRow.For(
			DiagnosticDescriptors.UseEqualityOperatorForNullCheck,
			"Use the equality operator",
			FromSync(static (document, diagnostic) => NullCheckEdits.CreateEqualityCheck(document.Root, diagnostic.Location.SourceSpan))
		),
	]);

	protected override FixTable Fixes => s_fixes;
}
