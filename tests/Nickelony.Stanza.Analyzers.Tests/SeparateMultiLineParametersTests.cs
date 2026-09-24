using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateMultiLineParametersTests
{
	[TestMethod]
	public async Task WrappedParameterTypeGetsSeparated()
	{
		const string source = """
			class C
			{
				void M(
					IReadOnlyDictionary<
						string,
						int> lookup,
					int capacity)
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(
					IReadOnlyDictionary<
						string,
						int> lookup,

					int capacity)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineParametersAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineParameters)
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MultiLineParameterDefaultGetsSeparated()
	{
		const string source = """
			class C
			{
				void M(
					string name = "a" +
						"b",
					int retries = 3)
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(
					string name = "a" +
						"b",

					int retries = 3)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineParametersAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineParameters)
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SingleLineParametersStayTight()
	{
		const string source = """
			class C
			{
				public C(string first, int retries)
				{
				}

				public string this[int index, string key]
				{
					get
					{
						return key;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineParametersAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineParameters)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task IndexerParametersGetSeparated()
	{
		const string source = """
			class C
			{
				string this[
					IReadOnlyDictionary<
						string,
						int> lookup,
					int index]
				{
					get
					{
						return string.Empty;
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				string this[
					IReadOnlyDictionary<
						string,
						int> lookup,

					int index]
				{
					get
					{
						return string.Empty;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineParametersAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineParameters)
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task WrappedParameterTypesGetSeparatedInDelegatesLambdasAndRecords()
	{
		const string source = """
			delegate void D(
				Dictionary<
					string,
					int> lookup,
				int capacity);

			record R(
				Dictionary<
					string,
					int> lookup,
				int capacity);

			class C
			{
				void M()
				{
					System.Action<Dictionary<
						string,
						int>, int> action = (
						Dictionary<
							string,
							int> lookup,
						int capacity) => DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			delegate void D(
				Dictionary<
					string,
					int> lookup,

				int capacity);

			record R(
				Dictionary<
					string,
					int> lookup,

				int capacity);

			class C
			{
				void M()
				{
					System.Action<Dictionary<
						string,
						int>, int> action = (
						Dictionary<
							string,
							int> lookup,

						int capacity) => DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineParametersAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineParameters)
			);

		Assert.HasCount(3, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
