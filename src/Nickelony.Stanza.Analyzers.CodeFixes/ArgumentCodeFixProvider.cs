using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Nickelony.Stanza.Analyzers.Arguments;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ArgumentCodeFixProvider)), Shared]
internal sealed class ArgumentCodeFixProvider : StanzaCodeFixProvider
{
	private static readonly FixTable s_fixes = new([
		FixRow.For(
			DiagnosticDescriptors.NameArgumentsOfCall,
			"Add the argument names",

			FromSync(
				static (document, diagnostic, cancellationToken) =>
					ArgumentEdits.CreateNameInsertions(root: document.Root, diagnostic, cancellationToken)
			)
		),

		FixRow.For(
			DiagnosticDescriptors.RemoveRedundantArgumentName,
			"Remove the argument name",

			FromSync(
				static (document, diagnostic, cancellationToken) =>
					ArgumentEdits.CreateNameRemovals(root: document.Root, sourceText: document.Text, diagnostic, cancellationToken)
			)
		),
	]);

	protected override FixTable Fixes => s_fixes;
}
