using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateImportDirectiveGroupsTests
{
	[TestMethod]
	public async Task TheKindChangeOfTheSectionIsSeparated()
	{
		const string source = """
			global using System;
			global using System.Linq;
			using System.Numerics;
			using System.Text;
			""";

		const string fixedSource = """
			global using System;
			global using System.Linq;

			using System.Numerics;
			using System.Text;
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateImportDirectiveGroupsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AGroupsLengthDoesNotDecideWhetherItIsSeparated()
	{
		const string source = """
			global using System;
			using System.Text;
			using static Helpers;
			using Items = System.Collections.Generic.List<int>;
			""";

		const string fixedSource = """
			global using System;

			using System.Text;

			using static Helpers;

			using Items = System.Collections.Generic.List<int>;
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateImportDirectiveGroupsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAliasIsAKindApartFromAPlainUsing()
	{
		const string source = """
			using System.Text;
			using Items = System.Collections.Generic.List<int>;
			""";

		const string fixedSource = """
			using System.Text;

			using Items = System.Collections.Generic.List<int>;
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateImportDirectiveGroupsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryKindIsAGroupOfItsOwn()
	{
		const string source = """
			global using System;
			global using static Helpers;
			global using Work = System.Threading.Tasks.Task<int>;
			using System.Text;
			using static Verify;
			using Items = System.Collections.Generic.List<int>;
			""";

		const string fixedSource = """
			global using System;

			global using static Helpers;

			global using Work = System.Threading.Tasks.Task<int>;

			using System.Text;

			using static Verify;

			using Items = System.Collections.Generic.List<int>;
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateImportDirectiveGroupsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(5, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunOfOneKindAndAnExistingLineAreLeftAlone()
	{
		const string tight = """
			using System;
			using System.Linq;
			using System.Text;
			""";

		AnalyzerRunResult tightResult =
			await RoslynTestHarness.RunAsync<SeparateImportDirectiveGroupsAnalyzer>(
				tight,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(tightResult.Diagnostics);

		const string separated = """
			global using System;

			using System.Text;
			""";

		AnalyzerRunResult separatedResult =
			await RoslynTestHarness.RunAsync<SeparateImportDirectiveGroupsAnalyzer>(
				separated,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(separatedResult.Diagnostics);
	}

	[TestMethod]
	public async Task AnExternAliasIsAKindOfItsOwn()
	{
		const string source = """
			extern alias Foo;
			using System;
			""";

		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateImportDirectiveGroups.Id, diagnostics[0].Id);
	}

	[TestMethod]
	public async Task ADirectiveAtTheBoundaryKeepsTheGroups()
	{
		const string source = """
			global using System;
			#region Plain imports
			using System.Text;
			#endregion
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateImportDirectiveGroupsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
