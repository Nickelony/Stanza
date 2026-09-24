using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateFileScopedNamespaceDeclarationFromFirstMemberTests
{
	public static IEnumerable<object[]> Members { get; } =
	[
		[
			"a member over several lines",

			"""
				class C
				{
					int Value => 1;
				}
				""",
		],

		["a member on one line", "class C { }"],
	];

	[TestMethod]
	[DynamicData(
		nameof(Members),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheMemberAfterTheHeaderIsSeparated(string name, string member)
	{
		string source = $$"""
			namespace N;
			{{member}}
			""";

		string fixedSource = $$"""
			namespace N;

			{{member}}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateFileScopedNamespaceDeclarationFromFirstMemberAnalyzer>(
				source
			);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task ACommentStaysWithTheMember()
	{
		const string source = """
			namespace N;
			// The only type this file holds.
			class C
			{
				int Value => 1;
			}
			""";

		const string fixedSource = """
			namespace N;

			// The only type this file holds.
			class C
			{
				int Value => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateFileScopedNamespaceDeclarationFromFirstMemberAnalyzer>(
				source
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AHeaderWithoutAMemberHasNothingToSeparate()
	{
		const string source = """
			namespace N;
			// Nothing to hold yet.
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateFileScopedNamespaceDeclarationFromFirstMemberAnalyzer>(
				source
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABraceBodiedNamespaceIsLeftAlone()
	{
		const string source = """
			namespace N
			{
				class C
				{
					int Value => 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateFileScopedNamespaceDeclarationFromFirstMemberAnalyzer>(
				source
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ADirectiveAtTheBoundaryKeepsTheGap()
	{
		const string source = """
			namespace N;
			#region Types
			class C
			{
				int Value => 1;
			}
			#endregion
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateFileScopedNamespaceDeclarationFromFirstMemberAnalyzer>(
				source
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AHeaderFollowedByAnImportSectionIsLeftAlone()
	{
		const string source = """
			namespace N;
			using System;

			class C
			{
				int Value => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateFileScopedNamespaceDeclarationFromFirstMemberAnalyzer>(
				source
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentThatTrailsTheSemicolonOverSeveralLinesStaysWithTheHeader()
	{
		const string source = """
			namespace N; /* note
			continued */
			class C
			{
				int Value => 1;
			}
			""";

		const string fixedSource = """
			namespace N; /* note
			continued */

			class C
			{
				int Value => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateFileScopedNamespaceDeclarationFromFirstMemberAnalyzer>(
				source
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMemberThatStartsWhereTheTrailingCommentEndsMovesBelowIt()
	{
		const string source = """
			namespace N; /* note
			continued */ class C { }
			""";

		const string fixedSource = """
			namespace N; /* note
			continued */

			class C { }
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateFileScopedNamespaceDeclarationFromFirstMemberAnalyzer>(
				source
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// An assembly attribute stands before the header, so it is no member the header gap separates.
	[TestMethod]
	public async Task AnAssemblyAttributeBeforeTheHeaderHasNoGapOfTheHeaderRule()
	{
		const string source = """
			using System;
			[assembly: CLSCompliant(true)]
			namespace N;

			class C { }
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}
}
