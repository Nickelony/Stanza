using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Regions;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class NameClosingRegionDirectiveTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> NameRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.NameClosingRegionDirective);

	[TestMethod]
	public async Task ABareClosingMarkerTakesTheName()
	{
		const string source = """
			internal sealed class Sample
			{
				#region Fields
				private int _count;
				#endregion
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				#region Fields
				private int _count;
				#endregion Fields
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingRegionDirectiveAnalyzer>(
				source,
				NameRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AClosingMarkerThatClosesNoRegionIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _first;
				#endregion
				private int _second;
			}
			""";

		// The marker is no source the compiler accepts, and a run over every analyzer fails if one throws on it.
		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			NameRuleOnly
		);

		Assert.IsEmpty(diagnostics);
	}

	[TestMethod]
	public void AClosingMarkerThatClosesNothingIsSkippedWhateverTheTreeHolds()
	{
		// The parser turns a marker that closes nothing into a bad directive, so only a tree built by hand holds one.
		SyntaxTrivia stray = SyntaxFactory.Trivia(
			SyntaxFactory.EndRegionDirectiveTrivia(isActive: true)
		);

		SyntaxTrivia opening = SyntaxFactory.Trivia(
			SyntaxFactory.RegionDirectiveTrivia(isActive: true)
		);

		SyntaxTrivia closing = SyntaxFactory.Trivia(
			SyntaxFactory.EndRegionDirectiveTrivia(isActive: true)
		);

		CompilationUnitSyntax root = SyntaxFactory
			.CompilationUnit()
			.WithEndOfFileToken(
				SyntaxFactory.Token(
					SyntaxFactory.TriviaList(stray, opening, closing),
					SyntaxKind.EndOfFileToken,
					SyntaxFactory.TriviaList()
				)
			);

		Assert.IsEmpty(RegionEdits.CreateEndRegionNameEdits(root, CancellationToken.None));
	}

	[TestMethod]
	public async Task ANameThatDoesNotMatchIsRewritten()
	{
		const string source = """
			internal sealed class Sample
			{
				#region Fields
				private int _count;
				#endregion Other
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				#region Fields
				private int _count;
				#endregion Fields
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingRegionDirectiveAnalyzer>(
				source,
				NameRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANestedRegionClosesAgainstItsOwnMarker()
	{
		const string source = """
			internal sealed class Sample
			{
				#region Outer
				#region Inner
				private int _count;
				#endregion
				#endregion
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				#region Outer
				#region Inner
				private int _count;
				#endregion Inner
				#endregion Outer
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingRegionDirectiveAnalyzer>(
				source,
				NameRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheWholeMessageOfTheRegionIsCopied()
	{
		const string source = """
			internal sealed class Sample
			{
				#region Fields and counters
				private int _count;
				#endregion
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				#region Fields and counters
				private int _count;
				#endregion Fields and counters
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingRegionDirectiveAnalyzer>(
				source,
				NameRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionWithoutANameIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				#region
				private int _count;
				#endregion Note
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingRegionDirectiveAnalyzer>(
				source,
				NameRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AMarkerInAnInactiveBlockIsWrittenToo()
	{
		const string source = """
			internal sealed class Sample
			{
			#if false
				#region Fields
				private int _count;
				#endregion
			#endif
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			#if false
				#region Fields
				private int _count;
				#endregion Fields
			#endif
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingRegionDirectiveAnalyzer>(
				source,
				NameRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	public static IEnumerable<object[]> ClosingLinesWithANote { get; } =
	[
		[
			"a note after the matching name",
			"#endregion Fields // TODO: split",
			"The name is written over the note of the marker.",
		],

		[
			"a note after another name",
			"#endregion Other // TODO: split",
			"The name is written over the note of the marker.",
		],

		[
			"a block comment after another name",
			"#endregion Other /* keep */",
			"The name is written over the block comment of the marker.",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(ClosingLinesWithANote),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AClosingMarkerThatCarriesANoteIsLeftAlone(
		string name,
		string closingLine,
		string message
	)
	{
		string source = $$"""
			internal sealed class Sample
			{
				#region Fields
				private int _count;
				{{closingLine}}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingRegionDirectiveAnalyzer>(
				source,
				NameRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, $"{name}: {message}");
	}

	[TestMethod]
	public async Task ANoteOnTheRegionLineTravelsWithTheName()
	{
		const string source = """
			internal sealed class Sample
			{
				#region Fields // the counters
				private int _count;
				#endregion
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				#region Fields // the counters
				private int _count;
				#endregion Fields // the counters
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingRegionDirectiveAnalyzer>(
				source,
				NameRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionNameFixWorksWithoutTheAdditionalLocation()
	{
		const string source = """
			internal sealed class Sample
			{
				#region Fields
				private int _count;
				#endregion
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				#region Fields
				private int _count;
				#endregion Fields
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<NameClosingRegionDirectiveAnalyzer>(
				source,
				NameRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EachBranchOfAConditionalNamesItsOwnClosingMarker()
	{
		const string source = """
			internal sealed class Sample
			{
			#if A
				#region First
				private int _count;
				#endregion
			#else
				#region Second
				private int _sum;
				#endregion
			#endif
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			#if A
				#region First
				private int _count;
				#endregion First
			#else
				#region Second
				private int _sum;
				#endregion Second
			#endif
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingRegionDirectiveAnalyzer>(
				source,
				NameRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
