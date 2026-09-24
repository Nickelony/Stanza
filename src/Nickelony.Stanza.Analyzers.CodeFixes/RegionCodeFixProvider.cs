using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Nickelony.Stanza.Analyzers.Regions;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RegionCodeFixProvider)), Shared]
internal sealed class RegionCodeFixProvider : StanzaCodeFixProvider
{
	private static readonly FixTable s_fixes = new([
		BuildRow(
			DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors,
			"Add the blank line",
			static (document, cancellationToken) => RegionEdits.CreateMarkerGapEdits(document.Root, document.Text, cancellationToken)
		),

		BuildRow(
			DiagnosticDescriptors.NameClosingRegionDirective,
			"Write the region name",
			static (document, cancellationToken) => RegionEdits.CreateEndRegionNameEdits(document.Root, cancellationToken)
		),

		BuildRow(
			DiagnosticDescriptors.RemoveEmptyRegions,
			"Remove the empty region",
			static (document, cancellationToken) => RegionEdits.CreateEmptyRegionEdits(document.Root, document.Text, cancellationToken)
		),
	]);

	protected override FixTable Fixes => s_fixes;

	/// <summary>
	/// Applies the change a diagnostic carries, so a Fix All need not find the file's edits once per diagnostic.
	/// </summary>
	private static FixRow BuildRow(DiagnosticDescriptor descriptor, string title, Func<FixDocument, CancellationToken, List<RegionEdit>> create)
	{
		// A diagnostic that arrives without its properties finds its edit again.
		return FixRow.For(
			descriptor,
			title,

			FromSync(
				(document, diagnostic, cancellationToken) =>
					FixOptions.Parse(diagnostic).Change is { } change && change.Span.End <= document.Text.Length
						? [change]
						: StanzaFixes.FindChangeAt(create(document, cancellationToken), diagnostic.Location.SourceSpan.Start)
			)
		);
	}
}
