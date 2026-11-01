using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers.Tests.Documentation;

internal static class DocumentationFamilyFixtures
{
	internal static IReadOnlyDictionary<string, ReportDiagnostic> SpacedSlashRules { get; } =
		TestRuleSets.Only(
			DiagnosticDescriptors.MarkLanguageKeywords,
			DiagnosticDescriptors.SeparateSlashOfSelfClosingTag
		);

	internal static IReadOnlyDictionary<string, ReportDiagnostic> ParagraphRuleOff { get; } =
		new Dictionary<string, ReportDiagnostic>
		{
			[DiagnosticDescriptors.RemoveDocumentationPadding.Id] = ReportDiagnostic.Warn,
			[DiagnosticDescriptors.MarkDocumentationParagraphs.Id] = ReportDiagnostic.Suppress,
		};
}
