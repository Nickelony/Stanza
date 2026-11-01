using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Arguments;

using static Nickelony.Stanza.Analyzers.Tests.Arguments.ArgumentFamilyFixtures;

namespace Nickelony.Stanza.Analyzers.Tests.Arguments;

[TestClass]
public sealed class NameLiteralArgumentsOfCallTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.NameLiteralArgumentsOfCall);

	private static IReadOnlyDictionary<string, ReportDiagnostic> BothNamingRules { get; } =
		TestRuleSets.Only(
			DiagnosticDescriptors.NameArgumentsOfCall,
			DiagnosticDescriptors.NameLiteralArgumentsOfCall
		);

	[TestMethod]
	public async Task ADefaultLiteralAndADefaultOfATypeArePassedByName()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Place(string label, int x, string y)
				{
				}

				internal static void Run(string label)
				{
					Place(label, default, default(string));
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Place(string label, int x, string y)
				{
				}

				internal static void Run(string label)
				{
					Place(label, x: default, y: default(string));
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANullOrDefaultLiteralWithASuppressedWarningIsPassedByName()
	{
		const string source = """
			#nullable enable
			internal static class Sample
			{
				internal static void Place(string label, string x, string y)
				{
				}

				internal static void Run(string label)
				{
					Place(label, null!, default!);
				}
			}
			""";

		const string fixedSource = """
			#nullable enable
			internal static class Sample
			{
				internal static void Place(string label, string x, string y)
				{
				}

				internal static void Run(string label)
				{
					Place(label, x: null!, y: default!);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult boolOnly =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(literals: "bool")
			);

		Assert.IsEmpty(boolOnly.Diagnostics);
	}

	[TestMethod]
	public async Task AConditionalElementAccessAndAnInitializerIndexerAreLeftAlone()
	{
		const string source = """
			internal sealed class Grid
			{
				internal int this[bool flag, int column]
				{
					get => column;
					set { }
				}
			}

			internal static class Sample
			{
				internal static int Read(Grid grid) => grid?[true, 1] ?? 0;

				internal static Grid Make() =>
					new Grid
					{
						[true, 1] = 2,
					};
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheBoolSettingNamesOnlyTrueAndFalse()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path, true, null);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path, overwrite: true, null);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(literals: "bool")
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheBoolSettingLeavesACallWithNoBooleanLiteralAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Save(string path, object backup, object other)
				{
				}

				internal static void Run(string path)
				{
					Save(path, null, default);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(literals: "bool")
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow("keywords")]
	[DataRow("KEYWORDS")]
	[DataRow("")]
	[DataRow("everything")]
	public async Task AnyValueButBoolNamesEveryKindOfLiteral(string literals)
	{
		const string source = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path, true, null);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path, overwrite: true, backup: null);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(
				source,
				Rule,
				analyzerConfigOptions: Config(literals: literals)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneArgumentStaysUnnamed()
	{
		const string source = """
			internal static class Sample
			{
				internal static void SetEnabled(bool enabled)
				{
				}

				internal static void Run()
				{
					SetEnabled(true);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task NumbersStringsAndCharactersStayUnnamed()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Clamp(int value, int minimum, string label, char mark)
				{
				}

				internal static void Run(int value)
				{
					Clamp(value, 0, "range", 'x');
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task OnlyTheLiteralsOfACallAreNamedAndTheRestStaysAsWritten()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path, object tag)
				{
				}

				internal static void Run(int height, string path)
				{
					Draw(640, height, true, path, null);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, string path, object tag)
				{
				}

				internal static void Run(int height, string path)
				{
					Draw(640, height, smooth: true, path, tag: null);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnArgumentThatAlreadyHasANameIsLeftAndTheLiteralsAroundItAreNamed()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path, overwrite: true, null);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path, overwrite: true, backup: null);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACallWhoseLiteralsAllHaveNamesIsLeftAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path, overwrite: true, backup: null);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AParamsArgumentStaysUnnamedWhileTheLiteralBeforeItIsNamed()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Log(bool flag, string message, params object[] values)
				{
				}

				internal static void Run()
				{
					Log(true, "text", false, null);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Log(bool flag, string message, params object[] values)
				{
				}

				internal static void Run()
				{
					Log(flag: true, "text", false, null);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALiteralPassedAsTheWholeParamsArrayIsLeftAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Log(string message, params object[] values)
				{
				}

				internal static void Run()
				{
					Log("text", null);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnInvocationOfADelegateIsLeftAlone()
	{
		const string source = """
			using System;

			internal static class Sample
			{
				internal static void Run(Func<bool, object, int> map)
				{
					map(true, null);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnObjectCreationAndAnIndexerAreJudgedTheSameWay()
	{
		const string source = """
			internal sealed class Widget
			{
				internal Widget(string name, bool visible)
				{
				}

				internal int this[int row, bool wrap] => row;
			}

			internal static class Sample
			{
				internal static int Run(string name)
				{
					var widget = new Widget(name, true);
					return widget[1, false];
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Widget
			{
				internal Widget(string name, bool visible)
				{
				}

				internal int this[int row, bool wrap] => row;
			}

			internal static class Sample
			{
				internal static int Run(string name)
				{
					var widget = new Widget(name, visible: true);
					return widget[1, wrap: false];
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AReducedExtensionCallIsNamedFromItsFirstExplicitArgument()
	{
		const string source = """
			internal static class Extensions
			{
				internal static void Show(this string text, bool bold, object tag)
				{
				}
			}

			internal static class Sample
			{
				internal static void Run(string text)
				{
					text.Show(true, null);
				}
			}
			""";

		const string fixedSource = """
			internal static class Extensions
			{
				internal static void Show(this string text, bool bold, object tag)
				{
				}
			}

			internal static class Sample
			{
				internal static void Run(string text)
				{
					text.Show(bold: true, tag: null);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACallInsideAnExpressionTreeIsLeftAlone()
	{
		const string source = """
			using System;
			using System.Linq.Expressions;

			internal static class Sample
			{
				internal static bool Check(int value, bool strict) => strict;

				internal static Expression<Func<int, bool>> Make() => value => Check(value, true);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task FixAllNamesEveryLiteralOfTheCallInOnePass()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup, bool flush) { }

				internal static void Run(string path)
				{
					Save(path, true, null, false);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup, bool flush) { }

				internal static void Run(string path)
				{
					Save(path, overwrite: true, backup: null, flush: false);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BeforeCSharp72TheLiteralAndEveryArgumentAfterItAreNamed()
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
					Draw(640, height, smooth: true, path: path);
				}
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7_1);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BeforeCSharp72ACallWhoseTailHoldsAParamsArgumentIsLeftAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Log(bool flag, string message, params object[] values)
				{
				}

				internal static void Run()
				{
					Log(true, "text", false);
				}
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7_1);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACallThatNamingTheTailWouldMakeAmbiguousIsLeftAlone()
	{
		// Before C# 7.2 the names reach the string too, and both overloads then accept `a: true, b: "x"`.
		const string source = """
			internal static class Sample
			{
				internal static void M(bool a, string b)
				{
				}

				internal static void M(string b, bool a)
				{
				}

				internal static void Run()
				{
					M(true, "x");
				}
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7_1);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnOverloadThatSwapsTheParametersKeepsEachLiteralAtTheParameterItBinds()
	{
		const string source = """
			internal static class Sample
			{
				internal static void M(int a, bool b)
				{
				}

				internal static void M(bool b, int a)
				{
				}

				internal static void Run()
				{
					M(1, true);
					M(false, 2);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void M(int a, bool b)
				{
				}

				internal static void M(bool b, int a)
				{
				}

				internal static void Run()
				{
					M(1, b: true);
					M(b: false, 2);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BeforeCSharp72ACallWhoseNamesWouldLetAnOuterOverloadPickAnotherMethodIsLeftAlone()
	{
		// The names reach the string too, so both lambdas bind and the `int` count picks the other `Outer`.
		const string source = """
			using System;

			internal sealed class First
			{
				internal int Mix(string second, bool first, int third) => 1;
			}

			internal sealed class Second
			{
				internal int Mix(bool first, string second, int third) => 2;
			}

			internal static class Sample
			{
				internal static void Outer(Func<First, int> map, int count)
				{
				}

				internal static void Outer(Func<Second, int> map, long count)
				{
				}

				internal static void Run(string label)
				{
					Outer(item => item.Mix(true, label, 3), 1);
				}
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7_1);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AConditionalDirectiveAmongTheArgumentsLeavesTheListUnnamedInEachView(
		string view
	)
	{
		// Without B the last literal binds to `c`, and with B it binds to `b`.
		string source =
			view
			+ """
				internal static class Sample
				{
					internal static void M(int a, bool b = false, bool c = false)
					{
					}

					internal static void Call() =>
						M(
							1,
				#if !B
							true,
				#endif
							true
						);
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ADirectiveThatIsNotConditionalAmongTheArgumentsStillLetsTheLiteralsBeNamed()
	{
		const string source = """
			internal static class Sample
			{
				internal static void M(int a, bool b)
				{
				}

				internal static void Call() =>
					M(
						1,
			#pragma warning disable CS0618
						true
					);
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void M(int a, bool b)
				{
				}

				internal static void Call() =>
					M(
						1,
			#pragma warning disable CS0618
						b: true
					);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("", true)]
	[DataRow("#define B\n", false)]
	public async Task OnlyTheCallOfTheActiveBranchIsNamedInEachView(
		string view,
		bool firstBranchIsOff
	)
	{
		const string source = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
			#if B
					Save(path, true, null);
			#else
					Save(path, false, null);
			#endif
				}
			}
			""";

		string fixedBody = firstBranchIsOff
			? source.Replace(
				"Save(path, false, null)",
				"Save(path, overwrite: false, backup: null)",
				StringComparison.Ordinal
			)
			: source.Replace(
				"Save(path, true, null)",
				"Save(path, overwrite: true, backup: null)",
				StringComparison.Ordinal
			);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(
				view + source,
				Rule
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(view + fixedBody, result);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AMemberDeclaredUnderAConditionalIsNamedInNeitherView(string view)
	{
		// The two declarations name the literal's parameter differently.
		string source =
			view
			+ """
				internal static class Sample
				{
				#if B
					internal static void M(int a, bool strict)
					{
					}
				#else
					internal static void M(int a, bool exact)
					{
					}
				#endif

					internal static void Call() =>
						M(1, true);
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AConditionalDirectiveInsideAnArgumentLeavesTheListUnnamedInEachView(
		string view
	)
	{
		// Without B the list holds three arguments and the `true` binds to `c`,
		// and with B it holds two and the `true` binds to `b`, so a name written in one view breaks the other.
		string source =
			view
			+ """
				internal static class Sample
				{
					internal static void M(object a, object b = null, object c = null)
					{
					}

					internal static void Call(int x) =>
						M(
							x +
				#if !B
							1, x +
				#endif
							2,
							true
						);
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AConditionalDirectiveInsideALambdaArgumentStillLetsTheLiteralsBeNamed(
		string view
	)
	{
		// The lambda body holds the directive, so the list has the same arguments in each view.
		string source =
			view
			+ """
				internal static class Sample
				{
					internal static void M(System.Action a, bool b)
					{
					}

					internal static void Call() =>
						M(
							() =>
							{
				#if B
								Call();
				#endif
							},
							true
						);
				}
				""";

		string fixedSource =
			view
			+ """
				internal static class Sample
				{
					internal static void M(System.Action a, bool b)
					{
					}

					internal static void Call() =>
						M(
							() =>
							{
				#if B
								Call();
				#endif
							},
							b: true
						);
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AConditionalDirectiveAfterTheLastArgumentLeavesTheListUnnamedInEachView(
		string view
	)
	{
		// The directive stands before the closing parenthesis, among the arguments, so a view may drop the last one.
		string source =
			view
			+ """
				internal static class Sample
				{
					internal static void M(int a, bool b = false, bool c = false)
					{
					}

					internal static void Call() =>
						M(
							1,
							true
				#if B
							, true
				#endif
						);
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AConstructorOfATypeDeclaredUnderAConditionalIsNamedInNeitherView(string view)
	{
		string source =
			view
			+ """
				#if B
				internal sealed class Widget
				{
					internal Widget(int a, bool strict)
					{
					}
				}
				#else
				internal sealed class Widget
				{
					internal Widget(int a, bool exact)
					{
					}
				}
				#endif

				internal static class Sample
				{
					internal static Widget Make() =>
						new Widget(1, true);
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AMethodWhoseParameterListHoldsAConditionalIsNamedInNeitherView(string view)
	{
		string source =
			view
			+ """
				internal static class Sample
				{
					internal static void M(
						int a,
				#if B
						bool strict
				#else
						bool exact
				#endif
					)
					{
					}

					internal static void Call() =>
						M(1, true);
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACallThatTheRuleNamingEveryArgumentReportsIsLeftToThatRule()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, object tag) { }

				internal static void Run(int width, int height)
				{
					Draw(width, height, true, null);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			BothNamingRules,
			Config(minArguments: 4)
		);

		string[] ids = [.. result.Diagnostics.Select(static diagnostic => diagnostic.Id)];

		Assert.Contains(DiagnosticDescriptors.NameArgumentsOfCall.Id, ids);
		Assert.DoesNotContain(DiagnosticDescriptors.NameLiteralArgumentsOfCall.Id, ids);
	}

	[TestMethod]
	public async Task ACallBelowTheCountOfTheRuleNamingEveryArgumentIsReportedByTheLiteralRule()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path, true, null);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			BothNamingRules,
			Config(minArguments: 4)
		);

		string[] ids = [.. result.Diagnostics.Select(static diagnostic => diagnostic.Id)];

		Assert.DoesNotContain(DiagnosticDescriptors.NameArgumentsOfCall.Id, ids);
		Assert.Contains(DiagnosticDescriptors.NameLiteralArgumentsOfCall.Id, ids);
	}

	[TestMethod]
	[DataRow("none")]
	[DataRow("default")]
	public async Task TheLiteralRuleReportsWhileTheRuleNamingEveryArgumentIsOff(string severity)
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, object tag) { }

				internal static void Run(int width, int height)
				{
					Draw(width, height, true, null);
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = new(BothNamingRules)
		{
			[DiagnosticDescriptors.NameArgumentsOfCall.Id] =
				severity == "none" ? ReportDiagnostic.Suppress : ReportDiagnostic.Default,
		};

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			rules,
			Config(minArguments: 4)
		);

		string[] ids = [.. result.Diagnostics.Select(static diagnostic => diagnostic.Id)];

		Assert.DoesNotContain(DiagnosticDescriptors.NameArgumentsOfCall.Id, ids);
		Assert.Contains(DiagnosticDescriptors.NameLiteralArgumentsOfCall.Id, ids);
	}

	[TestMethod]
	public async Task TheLiteralRuleReportsWhereAPragmaSilencesTheRuleNamingEveryArgument()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, object tag) { }

				internal static void Run(int width, int height)
				{
			#pragma warning disable ZA0501
					Draw(width, height, true, null);
			#pragma warning restore ZA0501
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			BothNamingRules,
			Config(minArguments: 4)
		);

		string[] ids = [.. result.Diagnostics.Select(static diagnostic => diagnostic.Id)];

		Assert.DoesNotContain(DiagnosticDescriptors.NameArgumentsOfCall.Id, ids);
		Assert.Contains(DiagnosticDescriptors.NameLiteralArgumentsOfCall.Id, ids);
	}

	[TestMethod]
	public async Task TheLiteralRuleReportsACallThatTheRuleNamingEveryArgumentLeavesAlone()
	{
		// The ZA0501 rule leaves the call alone because naming every argument makes both overloads apply,
		// and naming the `true` alone does not, as `a` stands at the second parameter of the other overload.
		const string source = """
			internal static class Sample
			{
				internal static void M(bool a, string b)
				{
				}

				internal static void M(string b, bool a)
				{
				}

				internal static void Run()
				{
					M(true, "x");
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			BothNamingRules,
			Config(minArguments: 2)
		);

		string[] ids = [.. result.Diagnostics.Select(static diagnostic => diagnostic.Id)];

		Assert.DoesNotContain(DiagnosticDescriptors.NameArgumentsOfCall.Id, ids);
		Assert.Contains(DiagnosticDescriptors.NameLiteralArgumentsOfCall.Id, ids);
	}

	[TestMethod]
	public async Task NamingEveryArgumentAndTheLiteralsNeverReportOneCallTwice()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, object tag) { }

				internal static void Run(int width, int height)
				{
					Draw(width, height, true, null);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Draw(int width, int height, bool smooth, object tag) { }

				internal static void Run(int width, int height)
				{
					Draw(width: width, height: height, smooth: true, tag: null);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.NameArgumentsOfCall,
				DiagnosticDescriptors.NameLiteralArgumentsOfCall
			),

			Config(minArguments: 4, matchingName: "require")
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheRuleRemovingARedundantNameLeavesTheNameOfALiteral()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path: path, overwrite: true, backup: null);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path, overwrite: true, backup: null);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveRedundantArgumentName,
				DiagnosticDescriptors.NameLiteralArgumentsOfCall
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheLiteralRuleAndTheRuleRemovingARedundantNameSettleInEitherOrderOfFixes()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path: path, true, null);
				}
			}
			""";

		const string fixedSource = """
			internal static class Sample
			{
				internal static void Save(string path, bool overwrite, object backup) { }

				internal static void Run(string path)
				{
					Save(path, overwrite: true, backup: null);
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> removeRule = TestRuleSets.Only(
			DiagnosticDescriptors.RemoveRedundantArgumentName
		);

		AnalyzerRunResult namedFirst =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(source, Rule);

		AnalyzerRunResult thenRemoved =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(
				namedFirst.FixedText,
				removeRule
			);

		AnalyzerRunResult removedFirst =
			await RoslynTestHarness.RunAsync<RemoveRedundantArgumentNameAnalyzer>(
				source,
				removeRule
			);

		AnalyzerRunResult thenNamed =
			await RoslynTestHarness.RunAsync<NameLiteralArgumentsOfCallAnalyzer>(
				removedFirst.FixedText,
				Rule
			);

		RoslynTestHarness.AssertFixedText(fixedSource, thenRemoved);
		RoslynTestHarness.AssertFixedText(fixedSource, thenNamed);
	}
}
