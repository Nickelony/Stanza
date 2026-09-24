using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Regions;

internal static class RegionAnalysis
{
	/// <summary>Reports each region edit, with the change it makes riding the diagnostic.</summary>
	public static void ReportEdits(SyntaxTreeAnalysisContext context, DiagnosticDescriptor descriptor, List<RegionEdit> edits)
	{
		foreach (RegionEdit edit in edits)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			// The change rides the diagnostic, so the fix applies it without finding every edit of the file again.
			context.ReportDiagnostic(
				Diagnostic.Create(
					descriptor,
					location: Location.Create(context.Tree, edit.Anchor),
					additionalLocations: edit.Neighbor is { } neighbor ? [Location.Create(context.Tree, neighbor)] : [],
					properties: new FixOptions(Change: edit.Change).ToProperties()
				)
			);
		}
	}
}
