using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Expressions;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Expressions;

[TestClass]
public sealed class UsePatternForNullCheckTests
{
	private const string Token =
		"class Token\n{\n"
		+ "\tpublic static bool operator ==(Token left, Token right) => true;\n"
		+ "\tpublic static bool operator !=(Token left, Token right) => false;\n"
		+ "\tpublic override bool Equals(object other) => true;\n"
		+ "\tpublic override int GetHashCode() => 0;\n"
		+ "}\n\n";

	private const string Money =
		"struct Money\n{\n"
		+ "\tpublic static bool operator ==(Money left, Money right) => true;\n"
		+ "\tpublic static bool operator !=(Money left, Money right) => false;\n"
		+ "\tpublic override bool Equals(object other) => true;\n"
		+ "\tpublic override int GetHashCode() => 0;\n"
		+ "}\n\n";

	private const string Pair =
		"class Pair\n{\n"
		+ "\tpublic static object operator ==(Pair left, Pair right) => left;\n"
		+ "\tpublic static object operator !=(Pair left, Pair right) => right;\n"
		+ "\tpublic override bool Equals(object other) => true;\n"
		+ "\tpublic override int GetHashCode() => 0;\n"
		+ "}\n\n";

	private const string ExtensionOperators =
		"class Token\n{\n}\n\nstatic class TokenOperators\n{\n\textension(Token)\n\t{\n"
		+ "\t\tpublic static bool operator ==(Token left, Token right) => true;\n"
		+ "\t\tpublic static bool operator !=(Token left, Token right) => false;\n"
		+ "\t}\n}\n\n";

	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.UsePatternForNullCheck);

	// A comparison whose == against null tests only for null, with the types it needs, the parameters and the pattern it becomes.
	public static IEnumerable<object[]> NullChecks { get; } =
	[
		["an == of an object", "", "object value", "value == null", "value is null"],
		["a != of an object", "", "object value", "value != null", "value is not null"],
		["null on the left of ==", "", "object value", "null == value", "value is null"],
		["null on the left of !=", "", "object value", "null != value", "value is not null"],
		["a parenthesized operand", "", "object value", "(value) == null", "(value) is null"],
		["a parenthesized check", "", "object value", "(value != null)", "(value is not null)"],
		["a string", "", "string name", "name == null", "name is null"],

		[
			"a record",
			"record Order(int Id);\n\n",
			"Order order",
			"order != null",
			"order is not null",
		],

		["a delegate", "", "Action handler", "handler == null", "handler is null"],
		["a Delegate", "", "Delegate handler", "handler != null", "handler is not null"],

		[
			"a MulticastDelegate",
			"",
			"MulticastDelegate handler",
			"null == handler",
			"handler is null",
		],

		["a nullable int", "", "int? count", "count == null", "count is null"],

		[
			"a nullable struct whose operator lifts",
			Money,
			"Money? price",
			"price == null",
			"price is null",
		],

		["an interface", "", "IDisposable resource", "resource != null", "resource is not null"],

		[
			"a conditional access to a value",
			"",
			"string name",
			"name?.Length == null",
			"name?.Length is null",
		],

		[
			"a conditional access to a string",
			"",
			"Exception error",
			"error?.Message != null",
			"error?.Message is not null",
		],

		["an as cast", "", "object value", "value as string == null", "value as string is null"],

		[
			"an as cast to a nullable value",
			"",
			"object value",
			"value as int? == null",
			"value as int? is null",
		],

		[
			"a check compared again",
			"",
			"object value, bool flag",
			"value == null == flag",
			"value is null == flag",
		],

		[
			"an equality that returns an object",
			Pair,
			"Pair left, Pair right",
			"left == right == null",
			"(left == right) is null",
		],

		[
			"a cast to object of a type with its own operator",
			Token,
			"Token token",
			"(object)token == null",
			"(object)token is null",
		],

		["a null-forgiving operand", "", "object value", "value! != null", "value! is not null"],

		[
			"an operand on the line above",
			"",
			"object value",
			"value\n\t\t== null",
			"value\n\t\tis null",
		],

		[
			"a comment before the operator",
			"",
			"object value",
			"value /* first */ == null",
			"value /* first */ is null",
		],
	];

	// A comparison whose pattern would not mean the same, or would not compile, with the types it needs and the parameters.
	public static IEnumerable<object[]> OtherComparisons { get; } =
	[
		["a type with its own operator", Token, "Token token", "token == null"],
		["a type with its own operator, null on the left", Token, "Token token", "null != token"],

		[
			"a type that inherits an operator",
			Token + "class Coin : Token { }\n\n",
			"Coin coin",
			"coin == null",
		],

		[
			"a nullable struct with an operator of a nullable",
			NullableOperator,
			"Slot? slot",
			"slot == null",
		],

		["a non-nullable value type", "", "int count", "count == null"],
		["a non-nullable struct whose operator lifts", Money, "Money price", "price != null"],

		[
			"a constant",
			"static class Names\n{\n\tpublic const string Empty = null;\n}\n\n",
			"",
			"Names.Empty == null",
		],

		["two nulls", "", "", "null == null"],

		[
			"a record that defines its own equality",
			RecordWithEquality,
			"Order order",
			"order == null",
		],

		[
			"a comment that the reversed operands would lose",
			"",
			"object value",
			"null /* first */ == value",
		],

		["a comparison with a parenthesized null", "", "object value", "value == (null)"],
	];

	private const string NullableOperator =
		"struct Slot\n{\n"
		+ "\tpublic static bool operator ==(Slot? left, Slot? right) => true;\n"
		+ "\tpublic static bool operator !=(Slot? left, Slot? right) => false;\n"
		+ "\tpublic override bool Equals(object other) => true;\n"
		+ "\tpublic override int GetHashCode() => 0;\n"
		+ "}\n\n";

	private const string RecordWithEquality =
		"record Order(int Id)\n{\n"
		+ "\tpublic virtual bool Equals(Order other) => true;\n"
		+ "\tpublic override int GetHashCode() => Id;\n"
		+ "}\n\n";

	[TestMethod]
	[DynamicData(
		nameof(NullChecks),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ANullCheckBecomesAPattern(
		string name,
		string types,
		string parameters,
		string condition,
		string pattern
	)
	{
		AnalyzerRunResult result = await RunAsync(Member(types, parameters, condition));

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(Member(types, parameters, pattern), result, name);
	}

	[TestMethod]
	[DynamicData(
		nameof(OtherComparisons),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AComparisonThatIsNoPlainNullCheckIsLeftAlone(
		string name,
		string types,
		string parameters,
		string condition
	)
	{
		AnalyzerRunResult result = await RunAsync(Member(types, parameters, condition));
		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task AnUnconstrainedTypeParameterBecomesAPattern()
	{
		const string source = """
			class C
			{
				bool M<T>(T item) => item == null;
			}
			""";

		const string fixedSource = """
			class C
			{
				bool M<T>(T item) => item is null;
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task ATypeParameterWhoseBaseClassHasItsOwnOperatorIsLeftAlone()
	{
		string source =
			Token
			+ """
				class C
				{
					bool M<T>(T item)
						where T : Token => item == null;
				}
				""";

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task AStaticAbstractOperatorOfAConstraintIsLeftAlone()
	{
		const string source = """
			interface IEquatableToken<T>
				where T : IEquatableToken<T>
			{
				static abstract bool operator ==(T left, T right);

				static abstract bool operator !=(T left, T right);
			}

			class C
			{
				bool M<T>(T item)
					where T : class, IEquatableToken<T> => item != null;
			}
			""";

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task AnOperatorInAnotherViewLeavesTheCheckAloneInBothViews()
	{
		const string types = """
			class Token
			{
			#if EXTRA
				public static bool operator ==(Token left, Token right) => true;

				public static bool operator !=(Token left, Token right) => false;
			#endif

				public override bool Equals(object other) => true;

				public override int GetHashCode() => 0;
			}


			""";

		foreach (string view in new[] { string.Empty, "#define EXTRA\n" })
		{
			AnalyzerRunResult result = await RunAsync(
				view + Member(types, "Token token", "token == null")
			);

			Assert.IsEmpty(result.Diagnostics, $"view '{view.Trim()}'");
		}
	}

	[TestMethod]
	public async Task ARecordEqualsInAnotherViewLeavesTheCheckAloneInBothViews()
	{
		const string types = """
			record Order(int Id)
			{
			#if EXTRA
				public virtual bool Equals(Order other) => true;

				public override int GetHashCode() => Id;
			#endif
			}


			""";

		foreach (string view in new[] { string.Empty, "#define EXTRA\n" })
		{
			AnalyzerRunResult result = await RunAsync(
				view + Member(types, "Order order", "order == null")
			);

			Assert.IsEmpty(result.Diagnostics, $"view '{view.Trim()}'");
		}
	}

	// CS8521 forbids other patterns on a pointer, but C# 8 lets it take the null constant.
	[TestMethod]
	public async Task APointerBecomesAPattern()
	{
		const string source = """
			unsafe class C
			{
				bool M(int* pointer) => pointer == null;

				bool N(delegate*<void> pointer) => pointer != null;
			}
			""";

		const string fixedSource = """
			unsafe class C
			{
				bool M(int* pointer) => pointer is null;

				bool N(delegate*<void> pointer) => pointer is not null;
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	// The pattern would not compile: CS8370 asks for C# 8 before a pointer takes the null constant.
	[TestMethod]
	public async Task CSharp7KeepsThePointerComparisonAndRewritesTheOther()
	{
		const string source = """
			unsafe class C
			{
				bool M(int* pointer) => pointer == null;

				bool N(object value) => value == null;
			}
			""";

		const string fixedSource = """
			unsafe class C
			{
				bool M(int* pointer) => pointer == null;

				bool N(object value) => value is null;
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7_3);

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The pattern would not compile: CS8511 asks for C# 8 before an open type takes the null constant.
	[TestMethod]
	public async Task CSharp7KeepsTheTypeParameterComparisonAndRewritesTheOther()
	{
		const string source = """
			using System;

			class C
			{
				bool M<T>(T item) => item == null;

				bool N<T>(T item) where T : IDisposable => item == null;

				bool O(object value) => value == null;
			}
			""";

		const string fixedSource = """
			using System;

			class C
			{
				bool M<T>(T item) => item == null;

				bool N<T>(T item) where T : IDisposable => item == null;

				bool O(object value) => value is null;
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7_3);

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The pattern would not compile: CS0037 forbids null for a non-nullable value type.
	[TestMethod]
	public async Task ANonNullableValueTypeIsLeftAloneWhereThePatternWouldNotCompile()
	{
		const string source = """
			class C
			{
				bool M(int count) => count == null;
			}
			""";

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task ADynamicOperandIsLeftAlone()
	{
		const string source = """
			class C
			{
				bool M(dynamic value) => value == null;
			}
			""";

		Assert.IsEmpty((await ReportAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task AnOperandThatDoesNotBindIsLeftAlone()
	{
		const string source = """
			class C
			{
				bool M() => missing == null;
			}
			""";

		Assert.IsEmpty((await ReportAsync(source)).Diagnostics);
	}

	// The pattern would not compile: CS8122 forbids a pattern in an expression tree.
	[TestMethod]
	public async Task ALambdaThatBecomesAnExpressionTreeIsLeftAlone()
	{
		const string source = """
			using System.Linq.Expressions;

			class C
			{
				Expression<Func<Item, bool>> M() => item => item.Name != null;
			}
			""";

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task ALambdaInsideAnExpressionTreeIsLeftAlone()
	{
		const string source = """
			using System.Linq;
			using System.Linq.Expressions;

			class C
			{
				Expression<Func<Item, bool>> M() => item => item.Tags.Any(tag => tag == null);
			}
			""";

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task AQueryableFilterIsLeftAlone()
	{
		const string source = """
			using System.Linq;

			class C
			{
				IQueryable<Item> M(IQueryable<Item> items) => items.Where(item => item.Name != null);
			}
			""";

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task AQueryOverAQueryableIsLeftAlone()
	{
		const string source = """
			using System.Linq;

			class C
			{
				IQueryable<string> M(IQueryable<Item> items) =>
					from item in items
					where item.Name != null
					orderby item.Name == null
					select item.Name;
			}
			""";

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task AQueryableFilterOfAnItemIsLeftAlone()
	{
		const string source = """
			using System.Linq;

			class C
			{
				IQueryable<string> M(IQueryable<string> items) => items.Where(item => item == null);
			}
			""";

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	// A join takes its inner sequence as an ordinary argument, so a lambda or a comparison there holds a pattern.
	[TestMethod]
	public async Task TheInnerSequenceOfAJoinOverAQueryableBecomesAPattern()
	{
		const string source = """
			using System.Collections.Generic;
			using System.Linq;

			class C
			{
				IQueryable<string> M(IQueryable<string> items, IEnumerable<string> others) =>
					from item in items
					join other in (others != null ? others : items) on item equals other
					select item;

				IQueryable<string> N(IQueryable<string> items, IEnumerable<string> others) =>
					from item in items
					join other in others.Where(name => name == null) on item equals other
					select item;
			}
			""";

		const string fixedSource = """
			using System.Collections.Generic;
			using System.Linq;

			class C
			{
				IQueryable<string> M(IQueryable<string> items, IEnumerable<string> others) =>
					from item in items
					join other in (others is not null ? others : items) on item equals other
					select item;

				IQueryable<string> N(IQueryable<string> items, IEnumerable<string> others) =>
					from item in items
					join other in others.Where(name => name is null) on item equals other
					select item;
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheKeysOfAJoinOverAQueryableAreLeftAlone()
	{
		const string source = """
			using System.Collections.Generic;
			using System.Linq;

			class C
			{
				IQueryable<string> M(IQueryable<Item> items, IEnumerable<Item> others) =>
					from item in items
					join other in others on item.Name == null equals other.Name == null
					select item.Name;
			}
			""";

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task AQueryOverAnEnumerableBecomesAPattern()
	{
		const string source = """
			using System.Linq;

			class C
			{
				IEnumerable<string> M(IEnumerable<Item> items) =>
					from item in items
					where item.Name != null
					select item.Name;
			}
			""";

		const string fixedSource = """
			using System.Linq;

			class C
			{
				IEnumerable<string> M(IEnumerable<Item> items) =>
					from item in items
					where item.Name is not null
					select item.Name;
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task ALambdaThatBecomesADelegateBecomesAPattern()
	{
		const string source = """
			class C
			{
				Func<Item, bool> M() => item => item.Name != null;
			}
			""";

		const string fixedSource = """
			class C
			{
				Func<Item, bool> M() => item => item.Name is not null;
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task CSharp8KeepsTheInequalityAndRewritesTheEquality()
	{
		const string source = """
			class C
			{
				bool M(object value) => value == null;

				bool N(object value) => value != null;
			}
			""";

		const string fixedSource = """
			class C
			{
				bool M(object value) => value is null;

				bool N(object value) => value != null;
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp8);

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task CSharp7RewritesTheEquality()
	{
		const string source = """
			class C
			{
				bool M(object value) => value == null;
			}
			""";

		const string fixedSource = """
			class C
			{
				bool M(object value) => value is null;
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp7);

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task CSharp6KeepsBothComparisons()
	{
		const string source = """
			class C
			{
				bool M(object value) => value == null;

				bool N(object value) => value != null;
			}
			""";

		using IDisposable version = RoslynTestHarness.AtLanguageVersion(LanguageVersion.CSharp6);

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	// The comparison binds to the reference equality of object ahead of the extension, so it calls no operator.
	[TestMethod]
	public async Task AnExtensionOperatorTheComparisonDoesNotCallLeavesItAPattern()
	{
		RoslynTestHarness.RequireRoslynWithCSharp14();

		AnalyzerRunResult result = await RunAsync(
			Member(ExtensionOperators, "Token token", "token == null")
		);

		RoslynTestHarness.AssertFixedText(
			Member(ExtensionOperators, "Token token", "token is null"),
			result
		);
	}

	private static string Member(string types, string parameters, string condition) =>
		$"{types}class C\n{{\n\tbool M({parameters}) => {condition};\n}}\n";

	private static Task<AnalyzerRunResult> RunAsync(string source) =>
		RoslynTestHarness.RunAsync<UsePatternForNullCheckAnalyzer>(source, Rule);

	private static Task<AnalyzerRunResult> ReportAsync(string source)
	{
		return RoslynTestHarness.ReportAsync(
			source,

			new RunOptions([new UsePatternForNullCheckAnalyzer()])
			{
				DiagnosticOptions = Rule,
				RequireCompiles = false,
			}
		);
	}
}
