using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Expressions;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Expressions;

[TestClass]
public sealed class UseEqualityOperatorForNullCheckTests
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

	private const string ExtensionOperators =
		"class Token\n{\n}\n\nstatic class TokenOperators\n{\n\textension(Token)\n\t{\n"
		+ "\t\tpublic static bool operator ==(Token left, Token right) => true;\n"
		+ "\t\tpublic static bool operator !=(Token left, Token right) => false;\n"
		+ "\t}\n}\n\n";

	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.UseEqualityOperatorForNullCheck);

	// A pattern that tests only for null, with the types it needs, the parameters and the comparison it becomes.
	public static IEnumerable<object[]> NullChecks { get; } =
	[
		["is null", "", "object value", "value is null", "value == null"],
		["is not null", "", "object value", "value is not null", "value != null"],
		["an empty property pattern", "", "object value", "value is { }", "value != null"],

		[
			"a negated empty property pattern",
			"",
			"object value",
			"value is not { }",
			"value == null",
		],

		["a parenthesized operand", "", "object value", "(value) is null", "(value) == null"],
		["a string", "", "string name", "name is null", "name == null"],

		[
			"a record",
			"record Order(int Id);\n\n",
			"Order order",
			"order is not null",
			"order != null",
		],

		["a delegate", "", "Action handler", "handler is null", "handler == null"],
		["a Delegate", "", "Delegate handler", "handler is not null", "handler != null"],
		["a nullable int", "", "int? count", "count is null", "count == null"],

		[
			"a nullable struct whose operator lifts",
			Money,
			"Money? price",
			"price is null",
			"price == null",
		],

		["an interface", "", "IDisposable resource", "resource is not null", "resource != null"],
		["a conditional access", "", "string name", "name?.Length is null", "name?.Length == null"],

		[
			"a cast to object of a type with its own operator",
			Token,
			"Token token",
			"(object)token is null",
			"(object)token == null",
		],

		[
			"the left operand of an equality",
			"",
			"object value, bool flag",
			"value is null == flag",
			"value == null == flag",
		],

		[
			"the right operand of an equality",
			"",
			"object value, bool flag",
			"flag == value is null",
			"flag == (value == null)",
		],

		[
			"the right operand of an inequality",
			"",
			"object value, bool flag",
			"flag != value is not null",
			"flag != (value != null)",
		],

		[
			"the operand of a pattern",
			"",
			"object value",
			"value is null is true",
			"(value == null) is true",
		],

		[
			"the operand of a type test",
			"",
			"object value",
			"value is null is bool",
			"(value == null) is bool",
		],

		[
			"an operand of a logical and",
			"",
			"object value, bool flag",
			"value is null && flag",
			"value == null && flag",
		],

		[
			"an operand of a bitwise and",
			"",
			"object value, bool flag",
			"flag & value is not null",
			"flag & value != null",
		],

		[
			"a negated parenthesized check",
			"",
			"object value",
			"!(value is null)",
			"!(value == null)",
		],

		[
			"a comment before the null",
			"",
			"object value",
			"value is /* none */ null",
			"value == /* none */ null",
		],
	];

	// A pattern whose comparison would not mean the same, with the types it needs and the parameters.
	public static IEnumerable<object[]> OtherPatterns { get; } =
	[
		["a type with its own operator", Token, "Token token", "token is null"],
		["a type with its own operator, negated", Token, "Token token", "token is not null"],

		[
			"a type that inherits an operator",
			Token + "class Coin : Token { }\n\n",
			"Coin coin",
			"coin is { }",
		],

		[
			"a nullable struct with an operator of a nullable",
			NullableOperator,
			"Slot? slot",
			"slot is null",
		],

		[
			"a record that defines its own equality",
			RecordWithEquality,
			"Order order",
			"order is null",
		],

		[
			"a constant",
			"static class Names\n{\n\tpublic const string Empty = null;\n}\n\n",
			"",
			"Names.Empty is null",
		],

		["a pattern that also tests a value", "", "string name", "name is null or \"\""],
		["a property pattern with a property", "", "string name", "name is { Length: 0 }"],

		[
			"an empty property pattern that declares a variable",
			"",
			"object value",
			"value is { } found && found.ToString() != null",
		],

		["a type pattern", "", "object value", "value is string"],
		["a parenthesized null", "", "object value", "value is (null)"],
		["a comment inside the negation", "", "object value", "value is /* none */ not null"],

		[
			"a comment inside the empty property pattern",
			"",
			"object value",
			"value is { /* any */ }",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(NullChecks),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ANullCheckPatternBecomesAComparison(
		string name,
		string types,
		string parameters,
		string condition,
		string comparison
	)
	{
		AnalyzerRunResult result = await RunAsync(Member(types, parameters, condition));

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(Member(types, parameters, comparison), result, name);
	}

	[TestMethod]
	[DynamicData(
		nameof(OtherPatterns),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task APatternThatIsNoPlainNullCheckIsLeftAlone(
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
	public async Task AnUnconstrainedTypeParameterBecomesAComparison()
	{
		const string source = """
			class C
			{
				bool M<T>(T item) => item is not null;
			}
			""";

		const string fixedSource = """
			class C
			{
				bool M<T>(T item) => item != null;
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task APointerBecomesAComparison()
	{
		const string source = """
			unsafe class C
			{
				bool M(int* pointer) => pointer is not null;
			}
			""";

		const string fixedSource = """
			unsafe class C
			{
				bool M(int* pointer) => pointer != null;
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
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
					where T : class, IEquatableToken<T> => item is null;
			}
			""";

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task AnOperatorInAnotherViewLeavesThePatternAloneInBothViews()
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
				view + Member(types, "Token token", "token is null")
			);

			Assert.IsEmpty(result.Diagnostics, $"view '{view.Trim()}'");
		}
	}

	[TestMethod]
	public async Task ADynamicOperandIsLeftAlone()
	{
		const string source = """
			class C
			{
				bool M(dynamic value) => value is null;
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
				bool M() => missing is null;
			}
			""";

		Assert.IsEmpty((await ReportAsync(source)).Diagnostics);
	}

	// The comparison binds to the reference equality of object ahead of the extension, so it calls no operator.
	[TestMethod]
	public async Task AnExtensionOperatorTheComparisonWouldNotCallLeavesItAComparison()
	{
		RoslynTestHarness.RequireRoslynWithCSharp14();

		AnalyzerRunResult result = await RunAsync(
			Member(ExtensionOperators, "Token token", "token is null")
		);

		RoslynTestHarness.AssertFixedText(
			Member(ExtensionOperators, "Token token", "token == null"),
			result
		);
	}

	private static string Member(string types, string parameters, string condition) =>
		$"{types}class C\n{{\n\tbool M({parameters}) => {condition};\n}}\n";

	private static Task<AnalyzerRunResult> RunAsync(string source) =>
		RoslynTestHarness.RunAsync<UseEqualityOperatorForNullCheckAnalyzer>(source, Rule);

	private static Task<AnalyzerRunResult> ReportAsync(string source)
	{
		return RoslynTestHarness.ReportAsync(
			source,

			new RunOptions([new UseEqualityOperatorForNullCheckAnalyzer()])
			{
				DiagnosticOptions = Rule,
				RequireCompiles = false,
			}
		);
	}
}
