using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateImportSectionTests
{
	[TestMethod]
	public async Task AHeaderAndAnImportSectionWithoutABlankLineBelongToTheImportRule()
	{
		const string source = """
			namespace N;
			using System;
			class C
			{
				int Value => 1;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source);

		string reported = string.Join(
			",",

			result
				.Diagnostics.Select(diagnostic => diagnostic.Id)
				.Distinct()
				.OrderBy(id => id, StringComparer.Ordinal)
		);

		Assert.AreEqual(DiagnosticDescriptors.SeparateImportSection.Id, reported);
	}

	[TestMethod]
	public async Task ImportSectionIsSeparatedFromTheFirstMember()
	{
		const string source = """
			using System;
			using System.Linq;
			class C
			{
			}
			""";

		const string fixedSource = """
			using System;
			using System.Linq;

			class C
			{
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateImportSectionAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task GlobalUsingsAndAFileScopedNamespaceAreSeparated()
	{
		const string source = """
			global using System;
			using System.Linq;
			namespace N;

			class C
			{
			}
			""";

		const string fixedSource = """
			global using System;
			using System.Linq;

			namespace N;

			class C
			{
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateImportSectionAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ImportSectionsWithoutAMemberOrWithABlankLineStayTight()
	{
		const string withoutMember = """
			using System;
			using System.Linq;
			""";

		AnalyzerRunResult withoutMemberResult =
			await RoslynTestHarness.RunAsync<SeparateImportSectionAnalyzer>(withoutMember);

		Assert.IsEmpty(withoutMemberResult.Diagnostics);

		const string withBlankLine = """
			using System;

			class C
			{
			}
			""";

		AnalyzerRunResult withBlankLineResult =
			await RoslynTestHarness.RunAsync<SeparateImportSectionAnalyzer>(withBlankLine);

		Assert.IsEmpty(withBlankLineResult.Diagnostics);
	}

	[TestMethod]
	public async Task AnExternAliasIsPartOfTheImportSectionAndIsNeverSeparated()
	{
		const string source = """
			extern alias Foo;
			using System;
			class C
			{
				int Value => 1;
			}
			""";

		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source
		);

		Assert.HasCount(1, diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateImportSection.Id, diagnostics[0].Id);
	}

	[TestMethod]
	public async Task TheImportBoundaryBelongsToTheImportRuleOnly()
	{
		const string source = """
			using System;
			class C
			{
				void M()
				{
					DoThing();
				}
			}
			""";

		// The type after the import section spans multiple lines,
		// which would hand the boundary to the multi-line construct rule if the import section rule did not own it.
		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateImportSection.Id, result.Diagnostics[0].Id);
	}

	// An assembly attribute is no member, so no rule judges the gap below the usings or the one below the attribute.
	[TestMethod]
	public async Task AnAssemblyAttributeAfterTheImportSectionIsJudgedByNoRule()
	{
		const string source = """
			using System;
			[assembly: CLSCompliant(true)]
			class C { }
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}
}
