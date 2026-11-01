using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Limits;

/// <summary>Base of a report-only rule that reports each node past a numeric limit, where 0 turns it off.</summary>
internal abstract class LimitAnalyzer(DiagnosticDescriptor descriptor) : StanzaTreeAnalyzer(descriptor)
{
	/// <summary>Reads the limit of the rule from the options, where 0 turns the rule off.</summary>
	protected abstract int ReadLimit(StanzaOptions options);

	/// <summary>Collects the nodes under the root that pass the limit, one for each place to report.</summary>
	protected abstract IEnumerable<SyntaxNode> CollectPastLimit(SyntaxNode root, int limit, CancellationToken cancellationToken);

	/// <summary>Gets the place to report a node at, the whole node unless a rule names a part.</summary>
	protected virtual Location GetReportLocation(SyntaxNode node) => node.GetLocation();

	protected sealed override void Analyze(SyntaxTreeAnalysisContext context)
	{
		int limit = ReadLimit(StanzaOptions.From(context.Options, context.Tree));

		// A limit of zero turns the rule off, and it skips the walk.
		if (limit == 0)
			return;

		foreach (SyntaxNode node in CollectPastLimit(context.Tree.GetRoot(context.CancellationToken), limit, context.CancellationToken))
			context.ReportDiagnostic(Diagnostic.Create(Descriptor, GetReportLocation(node)));
	}
}
