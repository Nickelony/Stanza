using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Arguments;

using static Nickelony.Stanza.Analyzers.Tests.ArgumentFamilyFixtures;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class RemoveRedundantArgumentNameTests
{
	[TestMethod]
	public async Task AnIdentifierThatRepeatsItsParameterLosesItsNameAndALiteralKeepsItsName()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path)
				{
				}

				internal static void Run(int width, int height, string path)
				{
					Draw(width: width, height: height, smooth: true, path: path);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path)
				{
				}

				internal static void Run(int width, int height, string path)
				{
					Draw(width, height, smooth: true, path);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.RemoveRedundantArgumentName)
			);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.RemoveRedundantArgumentName);

	[TestMethod]
	public async Task APositionalArgumentHasNoNameToRemoveBesideANamedOne()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path)
				{
				}

				internal static void Run(int width, int height, string path)
				{
					Draw(width, height, smooth: true, path: path);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path)
				{
				}

				internal static void Run(int width, int height, string path)
				{
					Draw(width, height, smooth: true, path);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnArgumentThatRepeatsItsParameterKeepsItsPosition()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run(int first, int second)
				{
					int third = 3;
					int fourth = 4;
					return Mix(first: first, second: second, third: third, fourth: fourth);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run(int first, int second)
				{
					int third = 3;
					int fourth = 4;
					return Mix(first, second, third, fourth);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnArgumentAfterANameThatIsOutOfPlaceKeepsItsName()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third) => first + second + third;

				internal static int Run(int first, int second, int third) =>
					Mix(first: first, third: third, second: second);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third) => first + second + third;

				internal static int Run(int first, int second, int third) =>
					Mix(first, third: third, second: second);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AReversedPairOfNamesLeavesTheCallAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second) => first + second;

				internal static int Run(int first, int second) => Mix(second: second, first: first);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AFileThatRequiresTheNameReportsNothing()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second) => first + second;

				internal static int Run(int first, int second) => Mix(first: first, second: second);
			}
			""";

		AnalyzerRunResult required =
			await RoslynTestHarness.ReportAsync<RemoveRedundantArgumentNameAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(matchingName: "require")
			);

		Assert.IsEmpty(required.Diagnostics);

		AnalyzerRunResult allowed =
			await RoslynTestHarness.ReportAsync<RemoveRedundantArgumentNameAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(matchingName: "allow")
			);

		Assert.HasCount(2, allowed.Diagnostics);
	}

	[TestMethod]
	public async Task AnArgumentThatDiffersInCaseLosesItsNameWhenTheFileIgnoresTheCase()
	{
		const string source = """
			internal static class Sample
			{
				private const int Value = 1;

				internal static int Mix(int value, int second) => value + second;

				internal static int Run(int second) => Mix(value: Value, second: second);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				private const int Value = 1;

				internal static int Mix(int value, int second) => value + second;

				internal static int Run(int second) => Mix(Value, second);
			}
			""";

		AnalyzerRunResult insensitive =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(nameCase: "insensitive")
			);

		Assert.HasCount(2, insensitive.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, insensitive);

		// The name that differs in case stays while the match is case-sensitive.
		AnalyzerRunResult sensitive =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		const string sensitiveFixedSource = """
			internal static class Sample
			{
				private const int Value = 1;

				internal static int Mix(int value, int second) => value + second;

				internal static int Run(int second) => Mix(value: Value, second);
			}
			""";

		Assert.HasCount(1, sensitive.Diagnostics);
		RoslynTestHarness.AssertFixedText(sensitiveFixedSource, sensitive);
	}

	[TestMethod]
	public async Task AParameterWhoseIdentifierDiffersInCaseLosesItsNameWhenTheFileIgnoresTheCase()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int Value, int second) => Value + second;

				internal static int Run()
				{
					int value = 1;
					return Mix(Value: value, second: 2);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int Value, int second) => Value + second;

				internal static int Run()
				{
					int value = 1;
					return Mix(value, second: 2);
				}
			}
			""";

		AnalyzerRunResult insensitive =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(nameCase: "insensitive")
			);

		Assert.HasCount(1, insensitive.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, insensitive);

		// Nothing changes while the match is case-sensitive, because `value` is not `Value`.
		AnalyzerRunResult sensitive =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.IsEmpty(sensitive.Diagnostics);

		// Under `require` the name stays, so the case decides nothing there.
		AnalyzerRunResult required =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(matchingName: "require", nameCase: "insensitive")
			);

		Assert.IsEmpty(required.Diagnostics);
	}

	[TestMethod]
	public async Task OnlyTheBareIdentifierLosesItsName()
	{
		const string source = """
			internal sealed class Sample
			{
				private readonly int _value = 1;

				private int other = 2;

				private int third = 3;

				private static int Mix(int value, int other, int third) => value + other + third;

				internal int Read() => Mix(value: this._value, other: (other), third: third);
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private readonly int _value = 1;

				private int other = 2;

				private int third = 3;

				private static int Mix(int value, int other, int third) => value + other + third;

				internal int Read() => Mix(value: this._value, other: (other), third);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AKeywordNameIsReadThroughItsEscape()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int @event, int second) => @event + second;

				internal static int Run(int @event, int second) => Mix(@event: @event, second: second);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int @event, int second) => @event + second;

				internal static int Run(int @event, int second) => Mix(@event, second);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheCountOfTheNamingRuleDoesNotDecide()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second) => first + second;

				internal static int Run(int first, int second) => Mix(first: first, second: second);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int first, int second) => first + second;

				internal static int Run(int first, int second) => Mix(first, second);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(minArguments: 0)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnExpandedParamsArrayLetsTheArgumentsBeforeItKeepTheirName()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Sum(int first, params int[] rest) => first + rest.Length;

				internal static int Run(int first) => Sum(first: first, 2, 3);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Sum(int first, params int[] rest) => first + rest.Length;

				internal static int Run(int first) => Sum(first, 2, 3);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnIndexerIsJudgedTheSameWay()
	{
		const string source = """
			internal sealed class Grid
			{
				private readonly int[,] _cells = new int[4, 4];

				internal int this[int row, int column] => _cells[row, column];

				internal int Read(int row, int column) => this[row: row, column: column];
			}
			""";

		const string fixedSource = """
			internal sealed class Grid
			{
				private readonly int[,] _cells = new int[4, 4];

				internal int this[int row, int column] => _cells[row, column];

				internal int Read(int row, int column) => this[row, column];
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AConstructorInitializerIsJudgedTheSameWay()
	{
		const string source = """
			internal class Base
			{
				internal Base(int first, int second)
				{
				}
			}

			internal sealed class Derived : Base
			{
				internal Derived(int first, int second)
					: base(first: first, second: second)
				{
				}
			}
			""";

		const string fixedSource = """
			internal class Base
			{
				internal Base(int first, int second)
				{
				}
			}

			internal sealed class Derived : Base
			{
				internal Derived(int first, int second)
					: base(first, second)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AReducedExtensionCallIsJudgedFromItsReceiverOnwards()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Bind(this string text, int second, int third) =>
					text.Length + second + third;

				internal static int Reduced(string text, int second, int third) =>
					text.Bind(second: second, third: third);

				internal static int Written(string text, int second, int third) =>
					Sample.Bind(text, second: second, third: third);

				internal static int Reversed(string text, int second, int third) =>
					text.Bind(third: third, second: second);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Bind(this string text, int second, int third) =>
					text.Length + second + third;

				internal static int Reduced(string text, int second, int third) =>
					text.Bind(second, third);

				internal static int Written(string text, int second, int third) =>
					Sample.Bind(text, second, third);

				internal static int Reversed(string text, int second, int third) =>
					text.Bind(third: third, second: second);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnObjectCreationIsJudgedTheSameWay()
	{
		const string source = """
			internal sealed class Draw
			{
				internal Draw(int width, int height)
				{
				}
			}

			internal static class Sample
			{
				internal static Draw Run(int width, int height)
				{
					Draw one = new(width: width, height: height);
					return new(width: width, height: height);
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Draw
			{
				internal Draw(int width, int height)
				{
				}
			}

			internal static class Sample
			{
				internal static Draw Run(int width, int height)
				{
					Draw one = new(width, height);
					return new(width, height);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAttributeArgumentListIsLeftAlone()
	{
		const string source = """
			internal sealed class MarkAttribute : Attribute
			{
				internal MarkAttribute(int value)
				{
				}
			}

			internal static class Sample
			{
				private const int value = 1;

				[Mark(value: value)]
				internal static void Run()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentInTheGapKeepsItsPlace()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second) => first + second;

				internal static int Run(int first, int second) =>
					Mix(first: /* keep */ first, second: 2);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int first, int second) => first + second;

				internal static int Run(int first, int second) =>
					Mix(/* keep */ first, second: 2);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentAfterTheColonLeavesNoSpaceBesideTheRemovedName()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third) => first + second + third;

				internal static int Run(int first, int second, int third) =>
					Mix(first, second: /* the second */ second, third /* a */ : /* b */ third);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third) => first + second + third;

				internal static int Run(int first, int second, int third) =>
					Mix(first, /* the second */ second, /* a */ /* b */ third);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task FixAllSettlesEveryNameInOnePass()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second) => first + second;

				internal static int Left(int first, int second) => Mix(first: first, second: second);

				internal static int Right(int first, int second) => Mix(first: second, second: first);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int first, int second) => first + second;

				internal static int Left(int first, int second) => Mix(first, second);

				internal static int Right(int first, int second) => Mix(first: second, second: first);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, Rule);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnOmittedLeadingOptionalParameterHoldsEveryName()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first = 1, int second = 2, int third = 3) =>
					first + second + third;

				internal static int Run(int second, int third) =>
					Mix(second: second, third: third);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnOmittedTrailingOptionalParameterHoldsNoName()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second = 2) => first + second;

				internal static int Run(int first) => Mix(first: first);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int first, int second = 2) => first + second;

				internal static int Run(int first) => Mix(first);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentBetweenTheNameAndTheColonSurvives()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int value) => value;

				internal static int Run(int value) => Mix(value /* keep */: value);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int value) => value;

				internal static int Run(int value) => Mix(/* keep */ value);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("ref")]
	[DataRow("in")]
	public async Task TheNameOfAnArgumentWithAModifierGoesWithoutLeavingAGap(string modifier)
	{
		string source = $$"""
			internal static class Sample
			{
				internal static void Draw({{modifier}} int value)
				{
				}

				internal static void Run()
				{
					int value = 1;
					Draw(value: {{modifier}} value);
				}
			}
			""";

		string fixedSource = $$"""
			internal static class Sample
			{
				internal static void Draw({{modifier}} int value)
				{
				}

				internal static void Run()
				{
					int value = 1;
					Draw({{modifier}} value);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheVariableOfAnOutDeclarationLosesItsName()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(out int run) => run = 1;

				internal static int Read()
				{
					Draw(run: out int run);
					return run;
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Draw(out int run) => run = 1;

				internal static int Read()
				{
					Draw(out int run);
					return run;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnOutDeclarationReadsByTheCaseRule()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(out int run) => run = 1;

				internal static int Read()
				{
					Draw(run: out int RUN);
					Draw(run: out int other);
					return RUN + other;
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Draw(out int run) => run = 1;

				internal static int Read()
				{
					Draw(out int RUN);
					Draw(run: out int other);
					return RUN + other;
				}
			}
			""";

		AnalyzerRunResult insensitive =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(nameCase: "insensitive")
			);

		Assert.HasCount(1, insensitive.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, insensitive);

		// The letters have to match in case, so nothing goes.
		AnalyzerRunResult sensitive =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.IsEmpty(sensitive.Diagnostics);
	}

	[TestMethod]
	public async Task ANameIsRemovedInAnExpressionBodyAndAFieldInitializer()
	{
		const string source = """
			internal sealed class Draw
			{
				internal Draw(int width, int height)
				{
				}
			}

			internal static class Sample
			{
				private const int width = 1;

				private const int height = 2;

				private static readonly Draw s_draw = new(width: width, height: height);

				internal static Draw Run(int width, int height) => new(width: width, height: height);
			}
			""";

		const string fixedSource = """
			internal sealed class Draw
			{
				internal Draw(int width, int height)
				{
				}
			}

			internal static class Sample
			{
				private const int width = 1;

				private const int height = 2;

				private static readonly Draw s_draw = new(width, height);

				internal static Draw Run(int width, int height) => new(width, height);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANameThatWouldFollowAKeptNameNeedsCSharp72()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second) => first + second;

				internal static int Run(int second) => Mix(first: 1, second: second);
			}
			""";

		using IDisposable older = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7_1);

		AnalyzerRunResult before =
			await RoslynTestHarness.ReportAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		using IDisposable newer = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7_2);

		AnalyzerRunResult after =
			await RoslynTestHarness.ReportAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.IsEmpty(before.Diagnostics);
		Assert.HasCount(1, after.Diagnostics);
	}

	[TestMethod]
	public async Task ANameThatKeepsTheOverloadInPlaceIsKept()
	{
		// Without the name, the string argument binds to the second overload instead.
		const string source = """
			internal static class Sample
			{
				internal static void M(object a)
				{
				}

				internal static void M(string x = "", object a = default)
				{
				}

				internal static void Run(string a)
				{
					M(a: a);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task APrimaryConstructorBaseCallLosesTheNamesItRepeats()
	{
		const string source = """
			internal class Parent(int first, int second)
			{
				internal int Sum => first + second;
			}

			internal sealed class Child(int first, int second) : Parent(first: first, second: second);
			""";

		const string fixedSource = """
			internal class Parent(int first, int second)
			{
				internal int Sum => first + second;
			}

			internal sealed class Child(int first, int second) : Parent(first, second);
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTwoArgumentNameRulesSettleOneCallTogether()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run()
				{
					int first = 1;
					int second = 2;

					return Mix(first: first, second, third: 3, fourth: 4);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run()
				{
					int first = 1;
					int second = 2;

					return Mix(first, second, third: 3, fourth: 4);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, BothRules);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.RemoveRedundantArgumentName.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTwoArgumentNameRulesSettleALooseNameTogether()
	{
		const string source = """
			internal static class Sample
			{
				private const int Value = 1;

				internal static int Mix(int value, int second, int third, int fourth) =>
					value + second + third + fourth;

				internal static int Run(int second, int third, int fourth)
				{
					return Mix(value: Value, second: second, third: third, fourth: fourth);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				private const int Value = 1;

				internal static int Mix(int value, int second, int third, int fourth) =>
					value + second + third + fourth;

				internal static int Run(int second, int third, int fourth)
				{
					return Mix(Value, second, third, fourth);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			BothRules,
			analyzerConfigOptions: Config(nameCase: "insensitive")
		);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Below C# 7.2 the naming rule keeps the shorthand only for the leading arguments, which is where the other rule writes it.
	[TestMethod]
	public async Task BeforeCSharp72TheTwoArgumentNameRulesSettleTogether()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height) { }

				internal static void Run(int width)
				{
					Draw(width: width, height: 480);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Draw(int width, int height) { }

				internal static void Run(int width)
				{
					Draw(width, height: 480);
				}
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			diagnosticOptions: BothRules,
			analyzerConfigOptions: Config(minArguments: 2)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The naming rule names the unnamed first argument below C# 7.2 and every argument after it,
	// so the removal rule leaves those names where they stand.
	[TestMethod]
	public async Task BeforeCSharp72ANameAfterAnUnnamedArgumentThatCarriesNoNameStays()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third) => first + second + third;

				internal static int Run(int second, int third)
				{
					return Mix(5, second: second, third: third);
				}
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	// Below C# 7.2 a named argument has to follow every positional one, so a fix on a later name removes the names before it.
	[TestMethod]
	public async Task EachFixAloneCompilesBelowCSharp72()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, int depth) { }

				internal static void Run(int width, int height, int depth)
				{
					Draw(width: width, height: height, depth: depth);
				}
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7);

		string[] texts = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.RemoveRedundantArgumentName,
			TestRuleSets.Only(DiagnosticDescriptors.RemoveRedundantArgumentName)
		);

		string[] calls =
		[
			.. texts.Select(static text =>
				text.Split('\n')
					.Single(static line =>
						line.TrimStart().StartsWith("Draw(", StringComparison.Ordinal)
					)
					.Trim()
			),
		];

		Assert.AreSequenceEqual(
			[
				"Draw(width, height: height, depth: depth);",
				"Draw(width, height, depth: depth);",
				"Draw(width, height, depth);",
			],

			calls
		);
	}

	// Without the names, an outer overload that the names ruled out can win, with a lambda that calls another method.
	[TestMethod]
	public async Task ANameThatKeepsOuterCandidatesOutOfALambdaArgumentIsKept()
	{
		const string source = """
			internal sealed class First
			{
				internal int Move(int width, int height) => 1;
			}

			internal sealed class Second
			{
				internal int Move(int left, int right) => 2;
			}

			internal static class Sample
			{
				internal static void Outer(Func<First, long> map)
				{
				}

				internal static void Outer(Func<Second, int> map)
				{
				}

				internal static void Run(int width, int height)
				{
					Outer(item => item.Move(width: width, height: height));
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	// A guard that returns or throws declares the name its condition reads for every statement after it.
	[TestMethod]
	public async Task ANameInAnInitializerLambdaStillReadsTheNameOfAnEarlierGuard()
	{
		const string source = """
			internal sealed class Sample
			{
				private static readonly Sample? s_sample = null;

				internal static readonly Func<string, int> Parse = text =>
				{
					if (!int.TryParse(text, out int value))
						return 0;

					return s_sample?.Clamp(value: value, low: 0, high: 10) ?? 0;
				};

				internal static readonly Func<object?, int> Cast = item =>
				{
					if (item is not int value)
						throw new ArgumentException();

					return s_sample?.Clamp(value: value, low: 0, high: 10) ?? 0;
				};

				internal int Clamp(int value, int low, int high) => value;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private static readonly Sample? s_sample = null;

				internal static readonly Func<string, int> Parse = text =>
				{
					if (!int.TryParse(text, out int value))
						return 0;

					return s_sample?.Clamp(value, low: 0, high: 10) ?? 0;
				};

				internal static readonly Func<object?, int> Cast = item =>
				{
					if (item is not int value)
						throw new ArgumentException();

					return s_sample?.Clamp(value, low: 0, high: 10) ?? 0;
				};

				internal int Clamp(int value, int low, int high) => value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The call that takes the lambda keeps the return its overload reads, so the name inside is still judged.
	[TestMethod]
	public async Task ANameInALambdaOfAnInitializerCallIsRemovedWhileTheLambdaKeepsItsReturn()
	{
		const string source = """
			using System.Linq;

			internal sealed class Sample
			{
				private static readonly string[] s_names = ["a"];

				private static readonly IEnumerable<int> s_lengths = s_names.Select(name =>
				{
					Validate(name: name);
					return name.Length;
				});

				private static void Validate(string name)
				{
				}
			}
			""";

		const string fixedSource = """
			using System.Linq;

			internal sealed class Sample
			{
				private static readonly string[] s_names = ["a"];

				private static readonly IEnumerable<int> s_lengths = s_names.Select(name =>
				{
					Validate(name);
					return name.Length;
				});

				private static void Validate(string name)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
