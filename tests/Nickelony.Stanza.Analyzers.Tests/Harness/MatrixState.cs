using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

/// <summary>One state of a rule matrix: the rules on, their severities and the run.</summary>
internal sealed record MatrixState(
	DiagnosticDescriptor[] RulesOn,
	Dictionary<string, ReportDiagnostic> Rules,
	string Label,
	RunOptions Run
);
