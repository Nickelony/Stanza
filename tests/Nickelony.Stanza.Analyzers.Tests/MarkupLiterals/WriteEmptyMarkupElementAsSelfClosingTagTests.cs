using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.MarkupLiterals;

[TestClass]
public sealed class WriteEmptyMarkupElementAsSelfClosingTagTests
{
	[TestMethod]
	public async Task AnEmptyElementOfTwoTagsTakesOneSelfClosingTag()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<ItemGroup>
						  <Compile Include="a.cs"></Compile>
						  <None></None>
						  <Compile Include="b.cs"   ></Compile>
						  <Compile Include="c.cs" />
						</ItemGroup>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<ItemGroup>
						  <Compile Include="a.cs" />
						  <None />
						  <Compile Include="b.cs" />
						  <Compile Include="c.cs" />
						</ItemGroup>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyMarkupElementAsSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag)
			);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElementWithAnyContentStaysAsWritten()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a> </a>
						  <b>
						  </b>
						  <c><!--note--></c>
						  <d><![CDATA[]]></d>
						  <e><?pi data?></e>
						  <f>text</f>
						  <g><h/></g>
						  <i xml:space="preserve">  </i>
						  <j>&#32;</j>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyMarkupElementAsSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag)
			);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task AnHtmlElementThatNeedsAnEndTagStaysAsWritten()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <div></div>
						  <SPAN></SPAN>
						  <script src="a.js"></script>
						  <textarea></textarea>
						  <li></li>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyMarkupElementAsSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag)
			);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task AVoidHtmlElementAndAPrefixedNameTakeTheSelfClosingTag()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root xmlns:x="urn:x">
						  <br></br>
						  <x:div></x:div>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root xmlns:x="urn:x">
						  <br />
						  <x:div />
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyMarkupElementAsSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task WrappedAttributesAndANamespacePrefixAreKept()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root xmlns:x="urn:x">
						  <x:Item
						    a="1"
						    b="2"></x:Item>
						  <x:Item
						    a="1"
						    ></x:Item>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root xmlns:x="urn:x">
						  <x:Item
						    a="1"
						    b="2" />
						  <x:Item
						    a="1"
						    />
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyMarkupElementAsSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEndTagThatWrapsStaysAsWritten()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <Item></Item
						  >
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyMarkupElementAsSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag)
			);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task AUtf8LiteralIsWrittenToo()
	{
		const string source = """"
			class C
			{
				void M()
				{
					var markup = """
						<root>
						  <Item></Item>
						</root>
						"""u8;
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					var markup = """
						<root>
						  <Item />
						</root>
						"""u8;
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyMarkupElementAsSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AFileEnablingTheTightHalfOfThePairGetsTheTightShape()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <Item a="1"></Item>
						  <Item a="2" ></Item>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <Item a="1"/>
						  <Item a="2"/>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag,
				DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight
			)
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AFileEnablingTheSpacedHalfOfThePairGetsTheSpacedShape()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <Item a="1"></Item>
						  <Item a="2"/>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <Item a="1" />
						  <Item a="2" />
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag,
				DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement
			)
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AFileEnablingBothHalvesGetsTheSpacedShapeBecauseItIsTheDefaultHalf()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <Item a="1"></Item>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <Item a="1" />
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag,
				DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement,
				DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight
			)
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheSpacedRuleHandsTheShapeToTheTightOne()
	{
		string source = $$""""
			class C
			{
				void M()
				{
					#pragma warning disable {{DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement.Id}}
					const string markup = """
						<root>
						  <Item a="1"></Item>
						</root>
						""";
				}
			}
			"""";

		string fixedSource = $$""""
			class C
			{
				void M()
				{
					#pragma warning disable {{DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement.Id}}
					const string markup = """
						<root>
						  <Item a="1"/>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag,
				DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement,
				DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight
			)
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	public static IEnumerable<object[]> SlashRuleSets { get; } =
	[
		[
			"neither slash rule",
			new[] { DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag },
			"<Item />",
			"<Item a=\"1\" />",
			"<None/>",
			"<None />",
		],

		[
			"the spaced slash rule",

			new[]
			{
				DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag,
				DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement,
			},

			"<Item />",
			"<Item a=\"1\" />",
			"<None />",
			"<None />",
		],

		[
			"the tight slash rule",

			new[]
			{
				DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag,
				DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight,
			},

			"<Item/>",
			"<Item a=\"1\"/>",
			"<None/>",
			"<None/>",
		],

		[
			"both slash rules",

			new[]
			{
				DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag,
				DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement,
				DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight,
			},

			"<Item />",
			"<Item a=\"1\" />",
			"<None />",
			"<None />",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(SlashRuleSets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheSlashRulesAndThisRuleSettleOnTheSameTextInEitherFixOrder(
		string name,
		DiagnosticDescriptor[] enabled,
		string empty,
		string withAttribute,
		string tightSlash,
		string spacedSlash
	)
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <Item></Item>
						  <Item a="1"  ></Item>
						  <None/>
						  <None />
						</root>
						""";
				}
			}
			"""";

		string fixedSource = $$""""
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  {{empty}}
						  {{withAttribute}}
						  {{tightSlash}}
						  {{spacedSlash}}
						</root>
						""";
				}
			}
			"""";

		RunOptions options = new(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = TestRuleSets.Only(enabled),
		};

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, options);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: result.FixedText,
			source,
			options,
			message: $"{name}:"
		);
	}
}
