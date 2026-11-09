using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Nickelony.Stanza.Analyzers.Commas;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CommaCodeFixProvider)), Shared]
internal sealed class CommaCodeFixProvider : StanzaCodeFixProvider
{
	private static readonly FixTable s_fixes = new([
		FixRow.For(
			DiagnosticDescriptors.AddTrailingComma,
			"Add the trailing comma",
			FromCarriedChange(static (document, cancellationToken) => CommaEdits.CreateAddEdits(document.Root, cancellationToken))
		),

		FixRow.For(
			DiagnosticDescriptors.RemoveTrailingComma,
			"Remove the trailing comma",
			FromCarriedChange(static (document, cancellationToken) => CommaEdits.CreateRemoveEdits(document.Root, cancellationToken))
		),
	]);

	protected override FixTable Fixes => s_fixes;
}
