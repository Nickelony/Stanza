using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Arguments;
using Nickelony.Stanza.Analyzers.Bodies;

using static Nickelony.Stanza.Analyzers.Tests.BodyFamilyFixtures;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class ArgumentFamilyTests
{
	[TestMethod]
	public async Task NamingTheArgumentsOfACallCanWidenABodyPastTheWidth()
	{
		const string source = """
			class C
			{
				static int Draw(int width, int height, bool smooth, int depth) { return width; }

				static int M() => Draw(640, 480, true, 3);
			}
			""";

		AnalyzerRunResult before =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(70)
			);

		Assert.IsEmpty(before.Diagnostics, "The body fits before the arguments are named.");

		// The naming is the change that leaves the default arrow rule work, which the last assertion pins.
		using IDisposable skipped = RoslynTestHarness.SkippingDefaultRules();

		AnalyzerRunResult named = await RoslynTestHarness.RunAsync<NameArgumentsOfCallAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.NameArgumentsOfCall),
			analyzerConfigOptions: Width(70)
		);

		AnalyzerRunResult after =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				named.FixedText,
				analyzerConfigOptions: Width(70)
			);

		const string fixedSource = """
			class C
			{
				static int Draw(int width, int height, bool smooth, int depth) { return width; }

				static int M() =>
					Draw(width: 640, height: 480, smooth: true, depth: 3);
			}
			""";

		Assert.HasCount(1, after.Diagnostics, "The named arguments leave work for the arrow rule.");
		RoslynTestHarness.AssertFixedText(fixedSource, after);
	}

	// A wider scope rebinds the calls beside each call, which makes the work quadratic.
	private const string CallsBesideEachOther = """
		record Q(int A, int B, int C, int D);

		class Data
		{
			static readonly int Total = Mix(1, 2, 3, 4);

			static readonly Q[] Items = [new Q(1, 2, 3, 4), new(5, 6, 7, 8)];

			static Q[] Arrow => [new Q(1, 2, 3, 4), new(5, 6, 7, 8)];

			static void Run()
			{
				Q[] items = [new Q(1, 2, 3, 4), new(5, 6, 7, 8)];
			}

			static void Alone()
			{
				Mix(1, 2, 3, 4);
			}

			static int Mix(int a, int b, int c, int d) => a;
		}
		""";

	// An `if` holds every `else if` below it, so speculating the statement rebinds the rest of the chain for each call.
	private const string CallsAfterAConditionalAccess = """
		using System.Linq;

		class Data
		{
			static int Run(string s, int[] values)
			{
				if (s?.StartsWith("a") == true)
					return 1;
				else if (s?.StartsWith("b") == true)
					return 2;

				var mixed = (from value in values select Mix(value, 1), Other(1, 2));
				return mixed.Item2;
			}

			static int Mix(int a, int b) => a;

			static int Other(int a, int b) => b;
		}
		""";

	[TestMethod]
	[DataRow(CallsBesideEachOther, DisplayName = "calls beside each other")]
	[DataRow(CallsAfterAConditionalAccess, DisplayName = "calls after a conditional access")]
	public void EachCallIsSpeculatedWithoutTheCallsAroundIt(string source)
	{
		SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();

		foreach (SyntaxNode call in root.DescendantNodes().Where(IsCall))
		{
			SyntaxNode scope = ArgumentSpeculation.FindSpeculationScope(call)!;

			// A call that binds alone is its own scope.
			// One that a query or a `?.` gives its meaning takes the clause or access around it, with no other call in it.
			if (!IsBoundByItsSurroundings(call))
			{
				Assert.AreSame(call, scope, call.ToString());
				continue;
			}

			Assert.AreSame(
				call,
				Assert.ContainsSingle(IsCall, scope.DescendantNodesAndSelf()),
				call.ToString()
			);
		}
	}

	[TestMethod]
	public void ABlockLambdaKeepsItsStatementScopeUnlessAnInitializerHoldsIt()
	{
		SyntaxNode root = CSharpSyntaxTree
			.ParseText(
				"""
				using System;

				class Data
				{
					static readonly Func<Data?, int?> s_field = d => { return d?.Mix(1, 2); };

					Data(Func<Data?, int?> f) { }

					Data() : this(d => { return d?.Mix(3, 4); }) { }

					Func<Data?, int?> Arrow => d => { return d?.Mix(5, 6); };

					event Func<Data?, int?> Changed = d => { return d?.Mix(7, 8); };

					int Mix(int a, int b) => a;
				}

				class Holder(Func<Data?, int?> f) : Data(d => { return d?.Mix(9, 10); });
				"""
			)
			.GetRoot();

		SyntaxNode[] scopes =
		[
			.. root.DescendantNodes()
				.OfType<InvocationExpressionSyntax>()
				.Select(static call => ArgumentSpeculation.FindSpeculationScope(call)!),
		];

		// Only a field or property initializer cannot speculate a lambda's statement, so only it costs the whole clause.
		Assert.IsInstanceOfType<EqualsValueClauseSyntax>(scopes[0]);
		Assert.IsInstanceOfType<ConditionalAccessExpressionSyntax>(scopes[1]);
		Assert.IsInstanceOfType<ConditionalAccessExpressionSyntax>(scopes[2]);
		Assert.IsInstanceOfType<EqualsValueClauseSyntax>(scopes[3]);
		Assert.IsInstanceOfType<ConditionalAccessExpressionSyntax>(scopes[4]);
	}

	[TestMethod]
	public void ACallInALambdaIsSpeculatedWithoutTheStatementsBesideIt()
	{
		string statements = string.Concat(
			Enumerable.Range(0, 50).Select(static i => $"Foo({i}, 2, 3, 4);\n")
		);

		string source = $$"""
			using System;

			class Data
			{
				static void Foo(int a, int b, int c, int d) { }

				static void Run(Action a) { }

				static void M()
				{
					Run(() =>
					{
						{{statements}}
					});
				}
			}
			""";

		ArgumentSpeculation.Speculation speculation = Speculate(source, callIndex: 25);

		// The speculation keeps the outer call and the checked call, and none of the 49 calls beside it.
		Assert.HasCount(
			2,
			speculation.Edited.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
		);
	}

	[TestMethod]
	public void ALambdaKeepsTheReturnsBesideTheCallInItsSpeculation()
	{
		const string source = """
			using System;

			class Data
			{
				static void Foo(int a, int b, int c, int d) { }

				static void Run(Func<int> f) { }

				static void M(bool flag)
				{
					Run(() =>
					{
						Foo(1, 2, 3, 4);
						Foo(5, 6, 7, 8);
						if (flag)
							return 1;
						Foo(9, 10, 11, 12);
						return 2;
					});
				}
			}
			""";

		ArgumentSpeculation.Speculation speculation = Speculate(source, callIndex: 2);

		Assert.HasCount(
			2,
			speculation.Edited.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>()
		);

		Assert.HasCount(
			2,
			speculation.Edited.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
		);
	}

	private static ArgumentSpeculation.Speculation Speculate(string source, int callIndex)
	{
		SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();

		InvocationExpressionSyntax call = root.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.ElementAt(callIndex);

		List<(ArgumentSyntax Argument, string Name)> arguments =
		[
			.. call.ArgumentList.Arguments.Select(static argument => (argument, "a")),
		];

		return ArgumentSpeculation.BuildSpeculation(call, arguments, namesThem: true)!;
	}

	private static bool IsCall(SyntaxNode node) =>
		node is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax;

	private static bool IsBoundByItsSurroundings(SyntaxNode call)
	{
		return call.Ancestors()
			.TakeWhile(static ancestor => ancestor is not StatementSyntax)
			.Any(static ancestor =>
				ancestor is QueryExpressionSyntax or ConditionalAccessExpressionSyntax
			);
	}
}
