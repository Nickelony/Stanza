using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class OrderElementsOfDocumentationCommentTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> OrderRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.OrderElementsOfDocumentationComment);

	[TestMethod]
	public async Task TheElementsOfACommentAreWrittenInTheCanonicalOrder()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Reads the value twice.</remarks>
				/// <summary>Binds the value.</summary>
				/// <param name="value">The value to bind.</param>
				internal int Bind(int value)
				{
					return value + value;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the value.</summary>
				/// <param name="value">The value to bind.</param>
				/// <remarks>Reads the value twice.</remarks>
				internal int Bind(int value)
				{
					return value + value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The tool's order reads no layout, so an element moves across a blank line, which stays where it stands.
	[TestMethod]
	public async Task AnElementMovesAcrossABlankLine()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns>The bound value.</returns>
				///
				/// <summary>Binds the value.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the value.</summary>
				///
				/// <returns>The bound value.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A `/** */` comment may leave out the star of each line, so an element stands after the indentation alone.
	[TestMethod]
	public async Task TheElementsOfACommentWithoutStarsAreOrdered()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				   <returns>The bound value.</returns>
				   <summary>Binds the value.</summary>
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				   <summary>Binds the value.</summary>
				   <returns>The bound value.</returns>
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A move takes whole lines, so an element that shares its line with prose would carry the prose along.
	[TestMethod]
	public async Task AnElementBesideProseOnItsLineStays()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns>The bound value.</returns> Read once.
				/// <summary>Binds the value.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow("class")]
	[DataRow("struct")]
	[DataRow("record")]
	public async Task TheParameterElementsFollowThePrimaryConstructor(string keyword)
	{
		string source = $$"""
			/// <summary>Holds the pair.</summary>
			/// <param name="second">The second value.</param>
			/// <param name="first">The first value.</param>
			internal {{keyword}} Sample(int first, int second) { }
			""";

		string fixedSource = $$"""
			/// <summary>Holds the pair.</summary>
			/// <param name="first">The first value.</param>
			/// <param name="second">The second value.</param>
			internal {{keyword}} Sample(int first, int second) { }
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheParameterElementsFollowTheSignature()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the pair.</summary>
				/// <param name="second">The second value.</param>
				/// <param name="first">The first value.</param>
				internal int Bind(int first, int second)
				{
					return first + second;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the pair.</summary>
				/// <param name="first">The first value.</param>
				/// <param name="second">The second value.</param>
				internal int Bind(int first, int second)
				{
					return first + second;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentThatAlreadyReadsInOrderIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the pair.</summary>
				/// <param name="first">The first value.</param>
				/// <param name="second">The second value.</param>
				/// <returns>The sum.</returns>
				/// <remarks>Reads both values.</remarks>
				internal int Bind(int first, int second)
				{
					return first + second;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABlockCommentWhoseMarkerSharesTheLastLineIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				 * <remarks>Reads the value twice.</remarks>
				 * <param name="value">The value to bind.</param> */
				internal int Bind(int value)
				{
					return value + value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "The marker the block closes with moves with the run.");
	}

	[TestMethod]
	public async Task AnUnnamedElementSharingTheFirstLineIsNeverMovedPast()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <inheritdoc/> <remarks>Reads the value twice.</remarks>
				/// <summary>Binds the value.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnElementTheOrderDoesNotNameSplitsTheComment()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the pair.</summary>
				/// <include file="docs.xml" path="doc/members/member[@name='Bind']/*"/>
				/// <remarks>Reads the pair.</remarks>
				/// <param name="value">The value to bind.</param>
				internal int Bind(int value)
				{
					return value + value;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the pair.</summary>
				/// <include file="docs.xml" path="doc/members/member[@name='Bind']/*"/>
				/// <param name="value">The value to bind.</param>
				/// <remarks>Reads the pair.</remarks>
				internal int Bind(int value)
				{
					return value + value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunThatOpensWithAnIncludeIsOrderedFromItOn()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <include file="docs.xml" path="doc/members/member[@name='Bind']/*"/>
				/// <param name="value">The value to bind.</param>
				/// <summary>Binds the pair.</summary>
				internal int Bind(int value)
				{
					return value + value;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <include file="docs.xml" path="doc/members/member[@name='Bind']/*"/>
				/// <summary>Binds the pair.</summary>
				/// <param name="value">The value to bind.</param>
				internal int Bind(int value)
				{
					return value + value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TwoIncludesSplitTheCommentIntoThreeRuns()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Reads the pair.</remarks>
				/// <param name="first">The first value.</param>
				/// <include file="docs.xml" path="doc/members/member[@name='Bind']/*"/>
				/// <returns>The pair.</returns>
				/// <summary>Binds the pair.</summary>
				/// <include file="docs.xml" path="doc/members/member[@name='Bind']/*"/>
				internal int Bind(int first)
				{
					return first + first;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <param name="first">The first value.</param>
				/// <remarks>Reads the pair.</remarks>
				/// <include file="docs.xml" path="doc/members/member[@name='Bind']/*"/>
				/// <summary>Binds the pair.</summary>
				/// <returns>The pair.</returns>
				/// <include file="docs.xml" path="doc/members/member[@name='Bind']/*"/>
				internal int Bind(int first)
				{
					return first + first;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTypeParameterElementsFollowTheDeclaration()
	{
		const string source = """
			/// <summary>Binds the pair.</summary>
			/// <typeparam name="TSecond">The second type.</typeparam>
			/// <typeparam name="TFirst">The first type.</typeparam>
			internal sealed class Sample<TFirst, TSecond>
			{
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			/// <summary>Binds the pair.</summary>
			/// <typeparam name="TFirst">The first type.</typeparam>
			/// <typeparam name="TSecond">The second type.</typeparam>
			internal sealed class Sample<TFirst, TSecond>
			{
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALineOfProseBetweenTwoElementsLeavesTheCommentAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Reads the value twice.</remarks>
				/// The prose that stands between the two elements.
				/// <summary>Binds the value.</summary>
				internal int Bind(int value)
				{
					return value + value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentWhoseElementsShareALineIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Reads the value twice.</remarks><summary>Binds the value.</summary>
				/// <param name="value">The value to bind.</param>
				internal int Bind(int value)
				{
					return value + value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentThatSharesItsLineWithCodeIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind(int value)
				{
					int doubled = value + value; /// <remarks>Reads the value twice.</remarks>
					return doubled;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task FixAllOrdersEveryCommentInOnePass()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Reads the value twice.</remarks>
				/// <summary>Binds the value.</summary>
				internal int Bind(int value)
				{
					return value + value;
				}

				internal int Pick(int value)
				{
					/// <returns>The value.</returns>
					/// <summary>Picks the value.</summary>
					return value;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the value.</summary>
				/// <remarks>Reads the value twice.</remarks>
				internal int Bind(int value)
				{
					return value + value;
				}

				internal int Pick(int value)
				{
					/// <summary>Picks the value.</summary>
					/// <returns>The value.</returns>
					return value;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, OrderRuleOnly);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnExceptionElementMovesAheadOfTheRemarks()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Reads the section.</remarks>
				/// <exception cref="System.ArgumentNullException">The section is missing.</exception>
				/// <summary>Binds the options.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options.</summary>
				/// <exception cref="System.ArgumentNullException">The section is missing.</exception>
				/// <remarks>Reads the section.</remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("internal Sample(int first, int second) { }")]
	[DataRow("public static Sample operator +(Sample first, Sample second) => first;")]
	[DataRow("internal int this[int first, int second] => first;")]
	[DataRow("internal delegate int Handler(int first, int second);")]
	public async Task TheParameterElementsFollowTheSignatureOfEachMemberKind(string member)
	{
		string source = $$"""
			internal sealed class Sample
			{
				/// <summary>Binds the pair.</summary>
				/// <param name="second">The second value.</param>
				/// <param name="first">The first value.</param>
				{{member}}
			}
			""";

		string fixedSource = $$"""
			internal sealed class Sample
			{
				/// <summary>Binds the pair.</summary>
				/// <param name="first">The first value.</param>
				/// <param name="second">The second value.</param>
				{{member}}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheParameterElementsFollowTheSignatureOfALocalFunction()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/// <summary>Adds the pair.</summary>
					/// <param name="second">The second value.</param>
					/// <param name="first">The first value.</param>
					int Add(int first, int second) => first + second;

					return Add(1, 2);
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/// <summary>Adds the pair.</summary>
					/// <param name="first">The first value.</param>
					/// <param name="second">The second value.</param>
					int Add(int first, int second) => first + second;

					return Add(1, 2);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("internal TSecond Bind<TFirst, TSecond>() => default!;")]
	[DataRow("internal delegate TSecond Handler<TFirst, TSecond>(TFirst first);")]
	public async Task TheTypeParameterElementsFollowTheDeclarationOfAMethodOrADelegate(
		string member
	)
	{
		string source = $$"""
			internal sealed class Sample
			{
				/// <summary>Binds the pair.</summary>
				/// <typeparam name="TSecond">The second type.</typeparam>
				/// <typeparam name="TFirst">The first type.</typeparam>
				{{member}}
			}
			""";

		string fixedSource = $$"""
			internal sealed class Sample
			{
				/// <summary>Binds the pair.</summary>
				/// <typeparam name="TFirst">The first type.</typeparam>
				/// <typeparam name="TSecond">The second type.</typeparam>
				{{member}}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A destructor takes no parameter, so a name it does not declare keeps its written place after the summary.
	[TestMethod]
	public async Task AParameterElementOnADestructorKeepsItsWrittenOrder()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Runs on collection.</remarks>
				/// <param name="second">The second value.</param>
				/// <param name="first">The first value.</param>
				/// <summary>Releases the sample.</summary>
				~Sample() { }
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Releases the sample.</summary>
				/// <param name="second">The second value.</param>
				/// <param name="first">The first value.</param>
				/// <remarks>Runs on collection.</remarks>
				~Sample() { }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderElementsOfDocumentationCommentAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
