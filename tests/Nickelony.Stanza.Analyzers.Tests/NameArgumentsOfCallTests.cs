using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Arguments;

using static Nickelony.Stanza.Analyzers.Tests.ArgumentFamilyFixtures;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class NameArgumentsOfCallTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.NameArgumentsOfCall);

	[TestMethod]
	public async Task OnlyTheCallInTheActiveBranchIsNamed()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path)
				{
				}

				internal static void Run()
				{
			#if DEBUG
					Draw(640, 480, true, "debug.png");
			#else
					Draw(640, 480, true, "release.png");
			#endif
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path)
				{
				}

				internal static void Run()
				{
			#if DEBUG
					Draw(640, 480, true, "debug.png");
			#else
					Draw(width: 640, height: 480, smooth: true, path: "release.png");
			#endif
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACallThatPassesEnoughArgumentsNamesThemAll()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run()
				{
					int value = 1;
					return Mix(value, 2, 3, 4);
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
					int value = 1;
					return Mix(first: value, second: 2, third: 3, fourth: 4);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BeforeCSharp72EveryArgumentIsNamedWhateverItsName()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path)
				{
				}

				internal static void Run(int height, string path)
				{
					Draw(640, height, true, path);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path)
				{
				}

				internal static void Run(int height, string path)
				{
					Draw(width: 640, height: height, smooth: true, path: path);
				}
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7_1);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BeforeCSharp72ALeadingArgumentThatCarriesItsNameKeepsItsPosition()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path)
				{
				}

				internal static void Run(int width, string path)
				{
					Draw(width, 480, true, path);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path)
				{
				}

				internal static void Run(int width, string path)
				{
					Draw(width, height: 480, smooth: true, path: path);
				}
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7_1);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACallThatNamingWouldMakeAmbiguousIsLeftAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static void M(int a, string b)
				{
				}

				internal static void M(string b, int a)
				{
				}

				internal static void Run()
				{
					M(1, "x");
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule,
			Config(minArguments: 2)
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AFunctionPointerOrDynamicCallIsLeftAlone()
	{
		const string source = """
			internal static unsafe class Sample
			{
				internal static void Run(delegate*<int, int, int, int, void> draw, dynamic target)
				{
					draw(1, 2, 3, 4);
					target.Draw(1, 2, 3, 4);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACallThatPassesTooFewArgumentsIsLeftAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(
					int first,
					int second,
					int third,
					int fourth = 4,
					int fifth = 5,
					int sixth = 6
				) => first + second + third + fourth + fifth + sixth;

				internal static int Run() => Mix(1, 2, 3);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheCountIsTheOneTheFileNames()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run() => Mix(1, 2, 3, 4);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run() => Mix(first: 1, second: 2, third: 3, fourth: 4);
			}
			""";

		AnalyzerRunResult lowered = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule,
			analyzerConfigOptions: Config(minArguments: 3)
		);

		Assert.HasCount(1, lowered.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, lowered);

		// Zero makes the rule report nothing for every call, whatever the call passes.
		AnalyzerRunResult off = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule,
			analyzerConfigOptions: Config(minArguments: 0)
		);

		Assert.IsEmpty(off.Diagnostics);

		// A value that is not a number leaves the default in place.
		var unreadable = new Dictionary<string, string>
		{
			["stanza_named_argument_count"] = "four",
		};

		AnalyzerRunResult fallback = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule,
			analyzerConfigOptions: unreadable
		);

		Assert.HasCount(1, fallback.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, fallback);
	}

	[TestMethod]
	public async Task AnIdentifierThatAlreadyCarriesTheNameKeepsItsPosition()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int value, int second, int third, int fourth) =>
					value + second + third + fourth;

				internal static int Run()
				{
					int value = 1;
					return Mix(value, 2, 3, 4);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int value, int second, int third, int fourth) =>
					value + second + third + fourth;

				internal static int Run()
				{
					int value = 1;
					return Mix(value, second: 2, third: 3, fourth: 4);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnInArgumentThatCarriesTheNameKeepsItsPosition()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Sum(int first, int second, int third, in int run) =>
					first + second + third + run;

				internal static int Run()
				{
					int run = 4;
					return Sum(1, 2, 3, in run);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Sum(int first, int second, int third, in int run) =>
					first + second + third + run;

				internal static int Run()
				{
					int run = 4;
					return Sum(first: 1, second: 2, third: 3, in run);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnOutDeclarationThatCarriesTheNameKeepsItsPosition()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Sum(int first, int second, int third, out int run)
				{
					run = first;
					return first + second + third;
				}

				internal static int Run() => Sum(1, 2, 3, out int run);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Sum(int first, int second, int third, out int run)
				{
					run = first;
					return first + second + third;
				}

				internal static int Run() => Sum(first: 1, second: 2, third: 3, out int run);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The call is bound alone, and an out variable the statement declared before it is still in scope there.
	[TestMethod]
	public async Task ACallThatReadsAnOutVariableOfItsStatementIsNamed()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Read(out int value)
				{
					value = 1;
					return value;
				}

				internal static int Sum(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run()
				{
					int total = Read(out int start) + Sum(start, 2, 3, 4);
					return total;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Read(out int value)
				{
					value = 1;
					return value;
				}

				internal static int Sum(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run()
				{
					int total = Read(out int start) + Sum(first: start, second: 2, third: 3, fourth: 4);
					return total;
				}
			}
			""";

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheShorthandCanBeDisabled()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int value, int second, int third, int fourth) =>
					value + second + third + fourth;

				internal static int Run()
				{
					int value = 1;
					return Mix(value, 2, 3, 4);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int value, int second, int third, int fourth) =>
					value + second + third + fourth;

				internal static int Run()
				{
					int value = 1;
					return Mix(value: value, second: 2, third: 3, fourth: 4);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule,
			analyzerConfigOptions: Config(matchingName: "require")
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnIdentifierThatDiffersInCaseKeepsItsPositionWhenTheFileIgnoresTheCase()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int Value, int second, int third, int fourth) =>
					Value + second + third + fourth;

				internal static int Run()
				{
					int value = 1;
					return Mix(value, 2, 3, 4);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int Value, int second, int third, int fourth) =>
					Value + second + third + fourth;

				internal static int Run()
				{
					int value = 1;
					return Mix(value, second: 2, third: 3, fourth: 4);
				}
			}
			""";

		const string caseSensitiveFixedSource = """
			internal static class Sample
			{
				internal static int Mix(int Value, int second, int third, int fourth) =>
					Value + second + third + fourth;

				internal static int Run()
				{
					int value = 1;
					return Mix(Value: value, second: 2, third: 3, fourth: 4);
				}
			}
			""";

		AnalyzerRunResult insensitive =
			await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(nameCase: "insensitive")
			);

		Assert.HasCount(1, insensitive.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, insensitive);

		// Compared case-sensitively, `value` does not match `Value`, so the argument becomes `Value: value`.
		AnalyzerRunResult sensitive = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, sensitive.Diagnostics);
		RoslynTestHarness.AssertFixedText(caseSensitiveFixedSource, sensitive);

		// Any value other than `insensitive` compares case-sensitively,
		// so it reports the call as the case-sensitive run does.
		AnalyzerRunResult unreadable =
			await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(nameCase: "nonsense")
			);

		Assert.HasCount(1, unreadable.Diagnostics);
		RoslynTestHarness.AssertFixedText(caseSensitiveFixedSource, unreadable);
	}

	[TestMethod]
	public async Task AnIdentifierThatCarriesAnotherNameIsNamed()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run()
				{
					int second = 1;
					return Mix(second, 2, 3, 4);
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
					int second = 1;
					return Mix(first: second, second: 2, third: 3, fourth: 4);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnArgumentThatAlreadyCarriesANameIsLeftAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run() => Mix(1, second: 2, third: 3, 4);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run() => Mix(first: 1, second: 2, third: 3, fourth: 4);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANameThatIsAKeywordIsEscaped()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int @event, int second, int third, int fourth) =>
					@event + second + third + fourth;

				internal static int Run() => Mix(1, 2, 3, 4);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int @event, int second, int third, int fourth) =>
					@event + second + third + fourth;

				internal static int Run() => Mix(@event: 1, second: 2, third: 3, fourth: 4);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnObjectCreationIsJudgedTheSameWay()
	{
		const string source = """
			internal sealed class Slot
			{
				internal Slot(int first, int second, int third, int fourth)
				{
				}
			}

			internal static class Sample
			{
				internal static Slot Run() => new Slot(1, 2, 3, 4);

				internal static Slot Target()
				{
					Slot slot = new(1, 2, 3, 4);
					return slot;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Slot
			{
				internal Slot(int first, int second, int third, int fourth)
				{
				}
			}

			internal static class Sample
			{
				internal static Slot Run() => new Slot(first: 1, second: 2, third: 3, fourth: 4);

				internal static Slot Target()
				{
					Slot slot = new(first: 1, second: 2, third: 3, fourth: 4);
					return slot;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATargetTypedCreationThatALambdaOrALocalFunctionReturnsIsNamed()
	{
		const string source = """
			internal sealed class Size
			{
				internal Size(int width, int height, int depth, int scale)
				{
				}
			}

			internal static class Sample
			{
				internal static void Run()
				{
					Size Local()
					{
						return new(1, 2, 3, 4);
					}

					Size Arrow() => new(5, 6, 7, 8);

					IEnumerable<Size> Many()
					{
						yield return new(9, 10, 11, 12);
					}

					Func<Size> lambda = () =>
					{
						return new(13, 14, 15, 16);
					};
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Size
			{
				internal Size(int width, int height, int depth, int scale)
				{
				}
			}

			internal static class Sample
			{
				internal static void Run()
				{
					Size Local()
					{
						return new(width: 1, height: 2, depth: 3, scale: 4);
					}

					Size Arrow() => new(width: 5, height: 6, depth: 7, scale: 8);

					IEnumerable<Size> Many()
					{
						yield return new(width: 9, height: 10, depth: 11, scale: 12);
					}

					Func<Size> lambda = () =>
					{
						return new(width: 13, height: 14, depth: 15, scale: 16);
					};
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATargetTypedCreationALambdaReturnsIsLeftAloneWhenNamingMakesItAmbiguous()
	{
		const string source = """
			internal sealed class Slot
			{
				internal Slot(int first, string second, int third, int fourth)
				{
				}

				internal Slot(string second, int first, int third, int fourth)
				{
				}
			}

			internal static class Sample
			{
				internal static Func<Slot> Run() => () =>
				{
					return new(1, "x", 3, 4);
				};
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACallInAnInitializerOrAnArrowBodyIsNamed()
	{
		const string source = """
			internal readonly struct Size
			{
				internal Size(int width, int height, int depth, int scale)
				{
				}
			}

			internal sealed class Box<T>
			{
				internal Box(T content, int first, int second, int third)
				{
				}
			}

			internal sealed class Sample(int width, int height)
			{
				private static readonly Size? s_size = new(1, 2, 3, 4);

				private static readonly Box<string>[] s_boxes = [new("a", 1, 2, 3)];

				private readonly int _total = Mix(width, height, 3, 4);

				internal Sample()
					: this(Mix(1, 2, 3, 4), 5)
				{
				}

				internal Box<int> Boxed => new(width, height, 3, 4);

				private static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		const string fixedSource = """
			internal readonly struct Size
			{
				internal Size(int width, int height, int depth, int scale)
				{
				}
			}

			internal sealed class Box<T>
			{
				internal Box(T content, int first, int second, int third)
				{
				}
			}

			internal sealed class Sample(int width, int height)
			{
				private static readonly Size? s_size = new(width: 1, height: 2, depth: 3, scale: 4);

				private static readonly Box<string>[] s_boxes = [new(content: "a", first: 1, second: 2, third: 3)];

				private readonly int _total = Mix(first: width, second: height, third: 3, fourth: 4);

				internal Sample()
					: this(Mix(first: 1, second: 2, third: 3, fourth: 4), 5)
				{
				}

				internal Box<int> Boxed => new(content: width, first: height, second: 3, third: 4);

				private static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(5, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATargetTypedCreationOutsideAStatementIsLeftAloneWhenNamingMakesItAmbiguous()
	{
		const string source = """
			internal sealed class Slot
			{
				internal Slot(int first, string second, int third, int fourth)
				{
				}

				internal Slot(string second, int first, int third, int fourth)
				{
				}
			}

			internal static class Sample
			{
				private static readonly Slot s_slot = new(1, "x", 3, 4);

				internal static Slot Run() => new(1, "x", 3, 4);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AConditionalCallALambdaReturnsIsNamed()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static Func<Sample?, int?> Run() => sample =>
				{
					return sample?.Mix(1, 2, 3, 4);
				};
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static Func<Sample?, int?> Run() => sample =>
				{
					return sample?.Mix(first: 1, second: 2, third: 3, fourth: 4);
				};
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACallInAQueryOfALocalDeclarationIsNamed()
	{
		const string source = """
			using System.Linq;

			internal sealed class Sample
			{
				internal int Run(int[] values)
				{
					var query = from value in values select Mix(value, 2, 3, 4);
					return query.Sum();
				}

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		const string fixedSource = """
			using System.Linq;

			internal sealed class Sample
			{
				internal int Run(int[] values)
				{
					var query = from value in values select Mix(first: value, second: 2, third: 3, fourth: 4);
					return query.Sum();
				}

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AConditionalCallInAnElseIfConditionIsNamed()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Run(Sample? sample, bool flag)
				{
					if (flag)
						return 1;
					else if (sample?.Mix(1, 2, 3, 4) > 0)
						return 2;

					return 3;
				}

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Run(Sample? sample, bool flag)
				{
					if (flag)
						return 1;
					else if (sample?.Mix(first: 1, second: 2, third: 3, fourth: 4) > 0)
						return 2;

					return 3;
				}

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EachCallOfAChainOfConditionalAccessesIsNamed()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int? Run(Sample? sample)
				{
					return sample?.Chain(1, 2, 3, 4)?.Mix(1, 2, 3, 4);
				}

				internal Sample? Chain(int first, int second, int third, int fourth) => this;

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int? Run(Sample? sample)
				{
					return sample?.Chain(first: 1, second: 2, third: 3, fourth: 4)?.Mix(first: 1, second: 2, third: 3, fourth: 4);
				}

				internal Sample? Chain(int first, int second, int third, int fourth) => this;

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A guard that returns or throws declares the name its condition reads for every statement after it.
	[TestMethod]
	public async Task ACallInAnInitializerLambdaStillReadsTheNameOfAnEarlierGuard()
	{
		const string source = """
			internal sealed class Sample
			{
				private static readonly Sample? s_sample = null;

				internal static readonly Func<string, int> Parse = text =>
				{
					if (!int.TryParse(text, out int parsed))
						return 0;

					return s_sample?.Mix(parsed, 2, 3, 4) ?? 0;
				};

				internal static readonly Func<object?, int> Cast = item =>
				{
					if (item is not int value)
						throw new ArgumentException();

					return s_sample?.Mix(value, 2, 3, 4) ?? 0;
				};

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private static readonly Sample? s_sample = null;

				internal static readonly Func<string, int> Parse = text =>
				{
					if (!int.TryParse(text, out int parsed))
						return 0;

					return s_sample?.Mix(first: parsed, second: 2, third: 3, fourth: 4) ?? 0;
				};

				internal static readonly Func<object?, int> Cast = item =>
				{
					if (item is not int value)
						throw new ArgumentException();

					return s_sample?.Mix(first: value, second: 2, third: 3, fourth: 4) ?? 0;
				};

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The speculation sets aside the branches and statements beside the call, so what a call reads from them must stay.
	[TestMethod]
	public async Task ACallInAnInitializerLambdaStillReadsTheNamesDeclaredBesideIt()
	{
		const string source = """
			internal sealed class Sample
			{
				internal static readonly Func<Sample?, object, int> Run = (sample, value) =>
				{
					int local = 1;
					int.TryParse("2", out int parsed);
					Other(sample, value);

					if (value is int number)
						return sample?.Mix(local, parsed, number, 4) ?? 0;
					else if (value is long wide)
						return sample?.Mix(1, 2, 3, (int)wide) ?? 0;

					return 0;
				};

				private static void Other(Sample? sample, object value)
				{
				}

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal static readonly Func<Sample?, object, int> Run = (sample, value) =>
				{
					int local = 1;
					int.TryParse("2", out int parsed);
					Other(sample, value);

					if (value is int number)
						return sample?.Mix(first: local, second: parsed, third: number, fourth: 4) ?? 0;
					else if (value is long wide)
						return sample?.Mix(first: 1, second: 2, third: 3, fourth: (int)wide) ?? 0;

					return 0;
				};

				private static void Other(Sample? sample, object value)
				{
				}

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACallInABlockLambdaOfAnInitializerIsNamedAfterAConditionalAccessOrInAQuery()
	{
		const string source = """
			using System.Linq;

			internal sealed class Sample
			{
				private static readonly Func<Sample?, int?> s_access = sample =>
				{
					return sample?.Mix(1, 2, 3, 4);
				};

				internal static Func<int[], int> Query { get; } = values =>
				{
					return (from value in values select new Sample().Mix(value, 2, 3, 4)).Sum();
				};

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		const string fixedSource = """
			using System.Linq;

			internal sealed class Sample
			{
				private static readonly Func<Sample?, int?> s_access = sample =>
				{
					return sample?.Mix(first: 1, second: 2, third: 3, fourth: 4);
				};

				internal static Func<int[], int> Query { get; } = values =>
				{
					return (from value in values select new Sample().Mix(first: value, second: 2, third: 3, fourth: 4)).Sum();
				};

				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AConstructorInitializerIsJudgedTheSameWay()
	{
		const string source = """
			internal class Base
			{
				protected Base(int first, int second, int third, int fourth)
				{
				}
			}

			internal sealed class Derived : Base
			{
				internal Derived(int value)
					: base(value, 2, 3, 4)
				{
				}
			}
			""";

		const string fixedSource = """
			internal class Base
			{
				protected Base(int first, int second, int third, int fourth)
				{
				}
			}

			internal sealed class Derived : Base
			{
				internal Derived(int value)
					: base(first: value, second: 2, third: 3, fourth: 4)
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AThisInitializerIsJudgedTheSameWay()
	{
		const string source = """
			internal sealed class Sample
			{
				internal Sample(int first, int second, int third, int fourth)
				{
				}

				internal Sample(int value)
					: this(value, 2, 3, 4)
				{
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal Sample(int first, int second, int third, int fourth)
				{
				}

				internal Sample(int value)
					: this(first: value, second: 2, third: 3, fourth: 4)
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnIndexerIsJudgedTheSameWay()
	{
		const string source = """
			internal sealed class Grid
			{
				private readonly int[] _cells = new int[16];

				internal int this[int row, int column, int depth, int index] => _cells[0];

				internal int Read() => this[1, 2, 3, 4];
			}
			""";

		const string fixedSource = """
			internal sealed class Grid
			{
				private readonly int[] _cells = new int[16];

				internal int this[int row, int column, int depth, int index] => _cells[0];

				internal int Read() => this[row: 1, column: 2, depth: 3, index: 4];
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnArrayAccessIsLeftAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Read(int[,,,] cells) => cells[0, 1, 2, 3];
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnOmittedOptionalParameterIsNotAnArgument()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(
					int first,
					int second,
					int third,
					int fourth,
					int fifth = 5
				) => first + second + third + fourth + fifth;

				internal static int Run() => Mix(1, 2, 3, 4);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(
					int first,
					int second,
					int third,
					int fourth,
					int fifth = 5
				) => first + second + third + fourth + fifth;

				internal static int Run() => Mix(first: 1, second: 2, third: 3, fourth: 4);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAttributeArgumentListIsLeftAlone()
	{
		const string source = """
			internal sealed class MarkAttribute : Attribute
			{
				internal MarkAttribute(int first, int second, int third, int fourth)
				{
				}
			}

			internal static class Sample
			{
				[Mark(1, 2, 3, 4)]
				internal static void Run()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnExpandedParamsArrayLeavesTheCallAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Sum(int first, params int[] rest) => first + rest.Length;

				internal static int Run() => Sum(1, 2, 3, 4);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnArrayArgumentIsNamedLikeAnyOther()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Sum(int first, int second, int third, int[] rest) =>
					first + second + third + rest.Length;

				internal static int Run()
				{
					int[] values = [2, 3, 4];
					return Sum(1, values[0], values[1], values);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Sum(int first, int second, int third, int[] rest) =>
					first + second + third + rest.Length;

				internal static int Run()
				{
					int[] values = [2, 3, 4];
					return Sum(first: 1, second: values[0], third: values[1], rest: values);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnInvocationOfADelegateIsLeftAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Run(Func<int, int, int, int, int> combine) => combine(1, 2, 3, 4);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AConditionalElementAccessAndAnInitializerIndexerAreLeftAlone()
	{
		const string source = """
			internal sealed class Grid
			{
				internal int this[int row, int column, int layer]
				{
					get => row + column + layer;
					set { }
				}
			}

			internal static class Sample
			{
				internal static int Run(Grid? grid) => grid?[1, 2, 3] ?? 0;

				internal static Grid Build() => new Grid { [1, 2, 3] = 4 };
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task FixAllSettlesTheWholeCallInOnePass()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int a, int b, int c, int d) => a + b + c + d;

				internal static int Run() => Mix(1, 2, 3, 4) + Mix(5, 6, 7, 8);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int a, int b, int c, int d) => a + b + c + d;

				internal static int Run() => Mix(a: 1, b: 2, c: 3, d: 4) + Mix(a: 5, b: 6, c: 7, d: 8);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, Rule);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AReducedExtensionCallIsNamedFromItsReceiverOnwards()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(this string text, int second, int third, int fourth, int fifth) =>
					text.Length + second + third + fourth + fifth;

				internal static int Run(string text)
				{
					return text.Mix(2, 3, 4, 5);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(this string text, int second, int third, int fourth, int fifth) =>
					text.Length + second + third + fourth + fifth;

				internal static int Run(string text)
				{
					return text.Mix(second: 2, third: 3, fourth: 4, fifth: 5);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACallInsideAnExpressionTreeIsLeftAlone()
	{
		// A named argument inside an expression tree is error CS0853.
		const string source = """
			internal sealed class Svc
			{
				internal int Run(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static System.Linq.Expressions.Expression<Func<Svc, int>> Tree() =>
					svc => svc.Run(1, 2, 3, 4);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACallInsideALambdaThatConvertsToAnyExpressionTypeIsLeftAlone()
	{
		// A lambda with a natural type is a tree as an `Expression` or a `LambdaExpression` too.
		const string source = """
			using System.Linq.Expressions;

			internal static class Sample
			{
				internal static int Draw(int width, int height, int depth, int scale) =>
					width + height + depth + scale;

				internal static void Log(LambdaExpression expression)
				{
				}

				internal static void Run()
				{
					Expression tree = () => Draw(1, 2, 3, 4);
					Log(() => Draw(5, 6, 7, 8));
					Expression<Func<int>> typed = () => Draw(9, 10, 11, 12);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACallInsideALambdaNestedInAnExpressionTreeIsLeftAlone()
	{
		const string source = """
			internal sealed class Svc
			{
				internal int Run(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static System.Linq.Expressions.Expression<Func<Svc, bool>> Tree(Svc[] items) =>
					svc => System.Linq.Enumerable.Any(items, item => item.Run(1, 2, 3, 4) > svc.Run(1, 2, 3, 4));
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	// Each clause of a query over a queryable takes an expression tree, which may not hold a named argument (CS0853).
	[TestMethod]
	[DataRow("where item.Run(1, 2, 3, 4) > 0 select item")]
	[DataRow("orderby item.Run(1, 2, 3, 4) select item")]
	[DataRow("orderby item.GetHashCode(), item.Run(1, 2, 3, 4) descending select item")]
	[DataRow("select item.Run(1, 2, 3, 4)")]
	[DataRow("group item by item.Run(1, 2, 3, 4)")]
	[DataRow("let total = item.Run(1, 2, 3, 4) select total")]
	public async Task ACallInsideAQueryOverAQueryableIsLeftAlone(string clauses)
	{
		string source = $$"""
			using System.Linq;

			internal sealed class Svc
			{
				internal int Run(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static object Query(IQueryable<Svc> items) =>
					from item in items
					{{clauses}};
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	// A join takes its inner sequence as an ordinary argument, and only its key and result selectors as expression trees.
	[TestMethod]
	public async Task ACallInTheInnerSequenceOfAJoinOverAQueryableIsNamed()
	{
		const string source = """
			using System.Collections.Generic;
			using System.Linq;

			internal sealed class Svc
			{
				internal static IEnumerable<int> Make(int first, int second, int third, int fourth) =>
					new[] { first, second, third, fourth };

				internal static object Query(IQueryable<int> items) =>
					from item in items
					join other in Make(1, 2, 3, 4) on item equals other
					select item;
			}
			""";

		const string fixedSource = """
			using System.Collections.Generic;
			using System.Linq;

			internal sealed class Svc
			{
				internal static IEnumerable<int> Make(int first, int second, int third, int fourth) =>
					new[] { first, second, third, fourth };

				internal static object Query(IQueryable<int> items) =>
					from item in items
					join other in Make(first: 1, second: 2, third: 3, fourth: 4) on item equals other
					select item;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACallInAnOrderingOverAnEnumerableIsNamed()
	{
		const string source = """
			using System.Collections.Generic;
			using System.Linq;

			internal sealed class Svc
			{
				internal int Run(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static object Query(IEnumerable<Svc> items) =>
					from item in items
					orderby item.Run(1, 2, 3, 4)
					select item;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		const string fixedSource = """
			using System.Collections.Generic;
			using System.Linq;

			internal sealed class Svc
			{
				internal int Run(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static object Query(IEnumerable<Svc> items) =>
					from item in items
					orderby item.Run(first: 1, second: 2, third: 3, fourth: 4)
					select item;
			}
			""";

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The names map each argument to the parameter it binds to, so they leave every overload of the outer call as applicable as it was.
	[TestMethod]
	public async Task ACallInALambdaOfAnOverloadedCallIsNamedWithoutChangingTheOuterCall()
	{
		const string source = """
			internal sealed class First
			{
				internal int Run(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}

			internal sealed class Second
			{
				internal string Run(int first, int second, int third, int fourth) =>
					string.Empty;
			}

			internal static class Sample
			{
				internal static int Outer(Func<First, int> function) => 1;

				internal static int Outer(Func<Second, int> function) => 2;

				internal static int Call() => Outer(item => item.Run(1, 2, 3, 4));
			}
			""";

		const string fixedSource = """
			internal sealed class First
			{
				internal int Run(int first, int second, int third, int fourth) =>
					first + second + third + fourth;
			}

			internal sealed class Second
			{
				internal string Run(int first, int second, int third, int fourth) =>
					string.Empty;
			}

			internal static class Sample
			{
				internal static int Outer(Func<First, int> function) => 1;

				internal static int Outer(Func<Second, int> function) => 2;

				internal static int Call() => Outer(item => item.Run(first: 1, second: 2, third: 3, fourth: 4));
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACallInsideADelegateLambdaIsStillNamed()
	{
		const string source = """
			internal sealed class Svc
			{
				internal int Run(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static Func<Svc, int> Delegate() =>
					svc => svc.Run(1, 2, 3, 4);
			}
			""";

		const string fixedSource = """
			internal sealed class Svc
			{
				internal int Run(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static Func<Svc, int> Delegate() =>
					svc => svc.Run(first: 1, second: 2, third: 3, fourth: 4);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AConditionalCallIsNamedLikeAnyOther()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int? Run(Sample? sample)
				{
					return sample?.Mix(1, 2, 3, 4);
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int? Run(Sample? sample)
				{
					return sample?.Mix(first: 1, second: 2, third: 3, fourth: 4);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APrimaryConstructorBaseCallIsNamedLikeAnyOther()
	{
		const string source = """
			internal class Parent(int first, int second, int third, int fourth)
			{
				internal int Sum => first + second + third + fourth;
			}

			internal sealed class Child(int a, int b) : Parent(a, b, 3, 4);
			""";

		const string fixedSource = """
			internal class Parent(int first, int second, int third, int fourth)
			{
				internal int Sum => first + second + third + fourth;
			}

			internal sealed class Child(int a, int b) : Parent(first: a, second: b, third: 3, fourth: 4);
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTwoArgumentNameRulesWriteACaseDifferingNameAndLeaveItAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Mix(int Value, int second, int third, int fourth) =>
					Value + second + third + fourth;

				internal static int Run()
				{
					int value = 1;

					return Mix(value, 2, 3, 4);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static int Mix(int Value, int second, int third, int fourth) =>
					Value + second + third + fourth;

				internal static int Run()
				{
					int value = 1;

					return Mix(Value: value, second: 2, third: 3, fourth: 4);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, BothRules);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Named, the arguments would fit First as well, and the call taking the lambda would pick it.
	[TestMethod]
	public async Task ACallWhoseNamesWouldLetAnOuterOverloadPickAnotherMethodIsLeftAlone()
	{
		const string source = """
			internal sealed class First
			{
				internal int Mix(string second, int first, int third, int fourth) => 1;
			}

			internal sealed class Second
			{
				internal int Mix(int first, string second, int third, int fourth) => 2;
			}

			internal static class Sample
			{
				internal static void Outer(Func<First, int> map, int count)
				{
				}

				internal static void Outer(Func<Second, int> map, long count)
				{
				}

				internal static void Run(int width, string label)
				{
					Outer(item => item.Mix(width, label, 3, 4), 1);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			Rule
		);

		Assert.IsEmpty(result.Diagnostics);
	}
}
