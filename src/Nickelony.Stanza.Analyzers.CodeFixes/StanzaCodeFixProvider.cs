using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

/// <summary>Base of the providers: a subclass lists a row per diagnostic ID, and each row builds its changes.</summary>
internal abstract class StanzaCodeFixProvider : CodeFixProvider
{
	/// <summary>The row of each diagnostic ID the provider fixes.</summary>
	protected abstract FixTable Fixes { get; }

	/// <summary>Whether a later fix's inserts at one position land inside an earlier fix's.</summary>
	protected virtual bool NestsInserts => false;

	/// <summary>Wraps a builder that needs no <see langword="await"/> as the asynchronous one a row holds.</summary>
	protected static FixChanges FromSync(Func<FixDocument, Diagnostic, CancellationToken, ImmutableArray<TextChange>> create) =>
		(document, diagnostic, cancellationToken) => Task.FromResult(create(document, diagnostic, cancellationToken));

	public sealed override ImmutableArray<string> FixableDiagnosticIds => Fixes.Ids;

	/// <summary>Merges the text changes itself rather than through the batch fixer.</summary>
	/// <remarks>
	/// Two nested statements close at one position, and the batch fixer drops a fix that inserts where another does.
	/// </remarks>
	public sealed override FixAllProvider GetFixAllProvider() => FixAllProvider.Create(FixAllDocumentAsync);

	private async Task<Document?> FixAllDocumentAsync(FixAllContext context, Document document, ImmutableArray<Diagnostic> diagnostics)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		SourceText sourceText = await document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);

		if (root is null)
			return null;

		var view = new FixDocument(document, root, sourceText);
		var sets = new List<ImmutableArray<TextChange>>();
		var fixedRuns = new HashSet<(string Id, int? Start, int? End, int Container)>();

		foreach (Diagnostic diagnostic in diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start))
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (!Fixes.TryGet(diagnostic.Id, out FixRow row))
				continue;

			// A fix-all runs one action, so a diagnostic fixed by another action of this provider stays as it is.
			if (context.CodeActionEquivalenceKey is { } key && row.DiagnosticId != key)
				continue;

			if (!StanzaDiagnosticLookup.IsDiagnosticInDocument(diagnostic, sourceText))
				continue;

			// The first gap of a run builds the changes and the rest are skipped,
			// rather than each building them again for the merge to drop.
			if (!FixOptions.TryClaimRun(root, diagnostic, fixedRuns))
				continue;

			ImmutableArray<TextChange> changes = await row.Create(document: view, diagnostic, cancellationToken: context.CancellationToken)
				.ConfigureAwait(false);

			if (!changes.IsEmpty)
				sets.Add(changes);
		}

		ImmutableArray<TextChange> merged = FixMerge.Merge(sets, insertsNest: NestsInserts);

		return merged.IsEmpty ? null : document.WithText(sourceText.WithChanges(merged));
	}

	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		context.CancellationToken.ThrowIfCancellationRequested();

		SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		SourceText sourceText = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);

		if (root is null)
			return;

		var view = new FixDocument(context.Document, root, sourceText);

		foreach (Diagnostic diagnostic in context.Diagnostics)
		{
			// A bounds check, not a freshness check:
			// it keeps a diagnostic whose positions fall outside the text away from the finder below,
			// which looks the unit up in the syntax tree by those positions.
			if (!StanzaDiagnosticLookup.IsDiagnosticInDocument(diagnostic, sourceText) || !Fixes.TryGet(diagnostic.Id, out FixRow row))
				continue;

			ImmutableArray<TextChange> changes = await row.Create(document: view, diagnostic, cancellationToken: context.CancellationToken)
				.ConfigureAwait(false);

			// A change the fix cannot make must not be offered: it would silently do nothing.
			if (changes.IsEmpty)
				continue;

			var action = CodeAction.Create(
				row.Title,
				cancellationToken => ApplyFixAsync(document: context.Document, registeredChanges: changes, cancellationToken),
				equivalenceKey: row.DiagnosticId
			);

			context.RegisterCodeFix(action, diagnostic);
		}
	}

	/// <summary>
	/// Applies the changes to the document the fix was registered for, not to one read again from the workspace.
	/// </summary>
	private static async Task<Document> ApplyFixAsync(
		Document document,
		ImmutableArray<TextChange> registeredChanges,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Registered changes are spans into the text the diagnostic was reported on.
		SourceText sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
		return document.WithText(sourceText.WithChanges(registeredChanges));
	}
}
