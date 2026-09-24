using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class DocCommentBlockRuleTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> ParagraphAndLayout { get; } =
		TestRuleSets.Only(
			DiagnosticDescriptors.MarkDocumentationParagraphs,
			DiagnosticDescriptors.LayOutDocumentationComment
		);

	public static IEnumerable<object[]> BlockDefects { get; } =
	[
		[
			"a blank line becomes paragraphs",
			DiagnosticDescriptors.MarkDocumentationParagraphs,

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>
					 * The first thought.
					 *
					 * The second thought.
					 * </summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>
					 * <para>
					 * The first thought.
					 * </para>
					 * <para>
					 * The second thought.
					 * </para>
					 * </summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"a spaced slash is made tight",
			DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight,

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>Binds <see langword="null" /> when the section is missing.</summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>Binds <see langword="null"/> when the section is missing.</summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"a tight slash is spaced",
			DiagnosticDescriptors.SeparateSlashOfSelfClosingTag,

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>Binds <see langword="null"/> when the section is missing.</summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>Binds <see langword="null" /> when the section is missing.</summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"a padding line is removed",
			DiagnosticDescriptors.RemoveDocumentationPadding,

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>
					 *
					 * Binds the options from the configuration section.
					 * </summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>
					 * Binds the options from the configuration section.
					 * </summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"the null keyword is marked up",
			DiagnosticDescriptors.MarkNullKeyword,

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>Binds the options or null when the section is missing.</summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>Binds the options or <see langword="null"/> when the section is missing.</summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"the text is given its period",
			DiagnosticDescriptors.EndDocumentationTextWithPeriod,

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>Binds the options from the configuration section that the host reads at startup</summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>Binds the options from the configuration section that the host reads at startup.</summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"the whitespace inside a tag is collapsed",
			DiagnosticDescriptors.CollapseWhitespaceInsideTag,

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>Binds <see  cref="Bind"/> the options.</summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>Binds <see cref="Bind"/> the options.</summary>
					 */
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"the elements are ordered",
			DiagnosticDescriptors.OrderElementsOfDocumentationComment,

			"""
				internal sealed class Sample
				{
					/**
					 * <param name="value">The options to bind.</param>
					 * <summary>Binds the options.</summary>
					 */
					internal int Bind(int value)
					{
						return value;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/**
					 * <summary>Binds the options.</summary>
					 * <param name="value">The options to bind.</param>
					 */
					internal int Bind(int value)
					{
						return value;
					}
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(BlockDefects),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ADefectInABlockIsFixedByItsRule(
		string name,
		DiagnosticDescriptor rule,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.Only(rule)
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task AnElementOnTheOpeningLineOfABlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/** <param name="value">The options to bind.</param>
				 * <summary>Binds the options.</summary>
				 */
				internal int Bind(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.OrderElementsOfDocumentationComment)
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheParagraphRuleAndTheLayoutRuleSettleTogether()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				 * <summary>
				 * Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.
				 *
				 * The second paragraph of the same element.
				 * </summary>
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
				 * <summary>
				 * <para>
				 * Binds the options from the configuration section, validates them and lets the host fail at
				 * startup rather than at the first request.
				 * </para>
				 * <para>
				 * The second paragraph of the same element.
				 * </para>
				 * </summary>
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, ParagraphAndLayout);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
