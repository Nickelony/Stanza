using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests.Commas;

[TestClass]
public sealed class CommaFamilyTests
{
	private const string WithoutTheComma = """
		enum Level
		{
			Low,
			High
		}
		""";

	private const string WithTheComma = """
		enum Level
		{
			Low,
			High,
		}
		""";

	[TestMethod]
	public async Task BothHalvesEnabledLeaveTheListToTheAddingHalf()
	{
		AnalyzerRunResult missing = await RoslynTestHarness.RunAllAsync(
			WithoutTheComma,

			TestRuleSets.Only(
				DiagnosticDescriptors.AddTrailingComma,
				DiagnosticDescriptors.RemoveTrailingComma
			)
		);

		Assert.AreEqual(
			DiagnosticDescriptors.AddTrailingComma.Id,
			missing.Diagnostics.Single().Id,
			"The adding half is the one that reports a list that lacks the comma."
		);

		RoslynTestHarness.AssertFixedText(WithTheComma, missing);

		AnalyzerRunResult written = await RoslynTestHarness.RunAllAsync(
			WithTheComma,

			TestRuleSets.Only(
				DiagnosticDescriptors.AddTrailingComma,
				DiagnosticDescriptors.RemoveTrailingComma
			)
		);

		Assert.IsEmpty(
			written.Diagnostics,
			"The removing half stays silent where the adding half owns the list."
		);
	}

	[TestMethod]
	public async Task OnlyTheRemovingHalfEnabledOwnsTheList()
	{
		AnalyzerRunResult written = await RoslynTestHarness.RunAllAsync(
			WithTheComma,
			TestRuleSets.Only(DiagnosticDescriptors.RemoveTrailingComma)
		);

		Assert.AreEqual(
			DiagnosticDescriptors.RemoveTrailingComma.Id,
			written.Diagnostics.Single().Id
		);

		RoslynTestHarness.AssertFixedText(WithoutTheComma, written);

		AnalyzerRunResult missing = await RoslynTestHarness.RunAllAsync(
			WithoutTheComma,
			TestRuleSets.Only(DiagnosticDescriptors.RemoveTrailingComma)
		);

		Assert.IsEmpty(missing.Diagnostics);
	}

	[TestMethod]
	public async Task NeitherHalfEnabledReportsNothing()
	{
		foreach (string source in new[] { WithoutTheComma, WithTheComma })
		{
			AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

			Assert.IsFalse(
				result.Diagnostics.Any(diagnostic =>
					diagnostic.Id == DiagnosticDescriptors.AddTrailingComma.Id
					|| diagnostic.Id == DiagnosticDescriptors.RemoveTrailingComma.Id
				)
			);
		}
	}

	[TestMethod]
	public async Task APragmaOverTheOwnerHandsTheListToTheOtherHalf()
	{
		const string source = """
			#pragma warning disable ZA0198
			enum Level
			{
				Low,
				High,
			}
			#pragma warning restore ZA0198
			""";

		const string fixedSource = """
			#pragma warning disable ZA0198
			enum Level
			{
				Low,
				High
			}
			#pragma warning restore ZA0198
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.AddTrailingComma,
				DiagnosticDescriptors.RemoveTrailingComma
			)
		);

		Assert.AreEqual(
			DiagnosticDescriptors.RemoveTrailingComma.Id,
			result.Diagnostics.Single().Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task RemovingTheCommaThatWasAddedRestoresTheList()
	{
		AnalyzerRunResult added = await RoslynTestHarness.RunAllAsync(
			WithoutTheComma,
			TestRuleSets.Only(DiagnosticDescriptors.AddTrailingComma)
		);

		AnalyzerRunResult removed = await RoslynTestHarness.RunAllAsync(
			added.FixedText,
			TestRuleSets.Only(DiagnosticDescriptors.RemoveTrailingComma)
		);

		RoslynTestHarness.AssertFixedText(WithoutTheComma, removed);
	}
}
