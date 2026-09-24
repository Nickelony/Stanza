extern alias stanza;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Xml;
using Nickelony.Stanza.Tests.Shared;

using AnalyzerDocs = stanza::Nickelony.Stanza.Analyzers.Documentation;
using AnalyzerEngine = stanza::Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class DocReprintParityTests
{
	private const int TabColumns = 4;

	private const int CommentWidth = 92;

	public static IEnumerable<object[]> GetBodyRows()
	{
		return
		[
			[
				"a summary that wraps",
				"<summary>The margin invalidates itself when its text view, visual lines, or scroll offset change and after it toggles a bookmark.</summary>",
			],

			[
				"a summary holding inline elements",
				"<summary>Use <see cref=\"Widget\"/> here, with <c>that</c> beside <paramref name=\"value\"/>.</summary>",
			],

			[
				"two inline elements beside a space",
				"<summary>Use <see cref=\"A\"/> <see cref=\"B\"/> here.</summary>",
			],

			[
				"a tag written across two lines",
				"<summary>Text that mentions\n<see cref=\"A.Longer.Name\"/> and more text after it.</summary>",
			],

			[
				"remarks holding paragraphs",
				"<remarks>\n<para>The first paragraph runs on long enough that the fill has to break it somewhere.</para>\n<para>The second paragraph is shorter.</para>\n</remarks>",
			],

			[
				"a nested element around text",
				"<returns>Binds <c>the options from the configuration section</c> at startup.</returns>",
			],

			[
				"a nested element written across two lines",
				"<summary>Binds <c>the options from\nthe configuration section</c> at startup, and then some more words to wrap.</summary>",
			],

			[
				"a summary holding paragraph markers",
				"<summary>\n<para>The first paragraph runs on long enough that the fill has to break it somewhere.</para>\n<para>The second paragraph is shorter.</para>\n</summary>",
			],

			[
				"an empty element beside a block element",
				"<summary>Alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron.</summary>\n<seealso cref=\"Widget\"/>\n<returns>Pi rho sigma tau upsilon phi chi psi omega, and then some more words to wrap.</returns>",
			],

			[
				"two sibling elements",
				"<summary>Alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron.</summary>\n<returns>Pi rho sigma tau upsilon phi chi psi omega, and then some more words to wrap.</returns>",
			],

			[
				"two sibling elements beside a blank line",
				"<summary>Alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron.</summary>\n\n<returns>Pi rho sigma tau upsilon phi chi psi omega, and then some more words to wrap.</returns>",
			],

			[
				"a summary holding a list",
				"<summary>\nBinds the options of the sample.\n<list type=\"bullet\">\n<item><description>One.</description></item>\n</list>\n</summary>",
			],

			[
				"a list between two paragraphs",
				"<remarks>\nThe first paragraph runs on long enough that the fill has to break it somewhere.\n<list type=\"bullet\">\n<item><description>One.</description></item>\n</list>\nThe second paragraph is shorter.\n</remarks>",
			],
		];
	}

	public static IEnumerable<object[]> GetRefusedBodyRows()
	{
		return
		[
			[
				"a blank line inside a nested element",
				"<summary>Text that mentions <c>one\n\ntwo</c> and more after it.</summary>",
			],

			[
				"a blank line inside an element around a phrase",
				"<summary>Binds the options of the sample.</summary>\n<returns>Text with <see cref=\"Widget\">one\n\ntwo</see> inside it.</returns>",
			],

			[
				"a list beside an opaque element",
				"<remarks>Binds the options.\n<list type=\"bullet\">\n<item><description>One.</description></item>\n</list>\n<code>var value = 1;</code>\n</remarks>",
			],
		];
	}

	public static IEnumerable<object[]> GetPaddingRows()
	{
		return
		[
			[
				"a blank line between two sibling elements",
				"<summary>Binds the options from the configuration section at startup.</summary>\n\n<returns>Delta.</returns>",
				true,
			],

			[
				"a blank line with prose on both sides",
				"Alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron.\n\nPi rho sigma tau upsilon phi chi psi omega, and then some more words to wrap.",
				false,
			],

			[
				"a blank line beside an inline element",
				"Alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron.\n\n<see cref=\"Widget\"/>",
				false,
			],

			[
				"a blank line after an element's start tag",
				"<summary>\n\nBinds the options from the configuration section at startup, and then some more words.</summary>",
				true,
			],

			[
				"a blank line before an element's end tag",
				"<summary>Binds the options from the configuration section at startup, and then some more words.\n\n</summary>",
				true,
			],

			[
				"a blank line on each side of an inline element at the top level",
				"<summary>Binds the options from the configuration section at startup.</summary>\n\n<see cref=\"Widget\"/>\n\n<returns>Delta.</returns>",
				true,
			],

			[
				"a run of blank lines between two elements",
				"<summary>Binds the options from the configuration section at startup.</summary>\n\n\n<returns>Delta.</returns>",
				true,
			],
		];
	}

	[TestMethod]
	[DynamicData(
		nameof(GetRefusedBodyRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void BothWalksDeclineTheSameBodies(string name, string body)
	{
		Assert.IsNull(
			CommentText(CommentSource(body)),
			$"{name}: the comment walk wrote the body."
		);

		Assert.IsEmpty(Reprint(MemberSource(body)), $"{name}: the markup reprint wrote the body.");
	}

	[TestMethod]
	[DynamicData(
		nameof(GetBodyRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void BothWalksWriteTheSameLines(string name, string body)
	{
		List<string> fromComment = CommentLines(body);
		string written = MemberBody(body);
		List<string> fromMember = MemberLines(written);

		Assert.IsNotEmpty(fromComment, $"{name}: the comment walk wrote nothing.");

		Assert.AreEqual(
			string.Join("\n", fromComment),
			string.Join("\n", fromMember),
			$"{name}: the two walks wrote different lines for\n{written}"
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(GetPaddingRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void BothPassesTakeTheSamePaddingOut(string name, string body, bool isPadding)
	{
		List<string> fromComment = CommentLinesWithoutPadding(body);
		string written = MemberBody(body, DocCleanupOptions.None with { Padding = true });
		List<string> fromMember = MemberLines(written);

		Assert.AreEqual(
			string.Join("\n", fromComment),
			string.Join("\n", fromMember),
			$"{name}: the two passes took different blank lines out of\n{written}"
		);

		Assert.AreEqual(
			isPadding ? 0 : 1,
			fromMember.Count(line => line.Length == 0),
			$"{name}: the written body keeps {fromMember.Count(line => line.Length == 0)} blank line(s)."
		);
	}

	public static IEnumerable<object[]> GetLayoutRows()
	{
		foreach (bool indentText in new[] { true, false })
		{
			foreach (DocExpansion expansion in Enum.GetValues<DocExpansion>())
			{
				yield return
				[
					$"{(indentText ? "nested" : "hanging")} indentation with {expansion} expanded",
					indentText,
					expansion.ToString(),
				];
			}
		}
	}

	// The layout options reach both walks the same way: the style of the indentation and what the comment expands.
	[TestMethod]
	[DynamicData(
		nameof(GetLayoutRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void BothWalksWriteTheSameLinesUnderEachIndentStyleAndExpansion(
		string name,
		bool indentText,
		string expansionName
	)
	{
		DocExpansion expansion = Enum.Parse<DocExpansion>(expansionName);

		string[] bodies =
		[
			"<summary>Binds the options of the sample.</summary>\n<returns>The bound options, or nothing when the section is missing.</returns>",
			"<remarks>\n<para>The first paragraph runs on long enough that the fill has to break it somewhere.</para>\n<para>The second paragraph is shorter.</para>\n</remarks>",
			"<summary>\nBinds the options of the sample.\n<list type=\"bullet\">\n<item><description>One value that runs long enough for the fill to break it somewhere.</description></item>\n</list>\n</summary>",
		];

		foreach (string body in bodies)
		{
			DocLayoutOptions layout = Layout(CommentWidth, indentSize: 2) with
			{
				IndentText = indentText,
				Expansion = expansion,
			};

			List<string> fromComment = CommentLines(body, layout);

			string written = MemberBody(
				body,

				layout: layout with
				{
					ContentWidth = CommentWidth + TabColumns,
				}
			);

			List<string> fromMember = MemberLines(written);

			Assert.IsNotEmpty(fromComment, $"{name}: the comment walk wrote nothing for {body}");

			Assert.AreEqual(
				string.Join("\n", fromComment),
				string.Join("\n", fromMember),
				$"{name}: the two walks wrote different lines for\n{written}"
			);
		}
	}

	[TestMethod]
	public void BothWalksIndentWithTabsTheSameWay()
	{
		const string body =
			"<remarks>\n<para>Binds the options from the configuration section, validates them and lets the host fail at startup.</para>\n</remarks>";

		List<string> fromComment = CommentLines(
			body,
			Layout(CommentWidth, indentSize: TabColumns, indentWithTabs: true)
		);

		string written = MemberBody(
			body,
			layout: Layout(CommentWidth + TabColumns, indentSize: TabColumns, indentWithTabs: true)
		);

		List<string> fromMember = MemberLines(written);

		Assert.IsNotEmpty(fromComment, "the comment walk wrote nothing.");

		Assert.Contains("\t", string.Join("\n", fromMember), "the written body holds no tab.");

		Assert.AreEqual(
			string.Join("\n", fromComment),
			string.Join("\n", fromMember),
			$"the two walks wrote different tab-indented lines for\n{written}"
		);
	}

	private static string? CommentText(string source, DocLayoutOptions? layout = null)
	{
		var frame = new AnalyzerDocs.DocCommentFrame(
			Indentation: "\t",
			LineEnding: "\n",
			Delimiters: AnalyzerDocs.DocCommentDelimiters.Lines
		);

		return AnalyzerDocs.DocCommentEdits.BuildCommentText(
			comment: GetComment(source),
			frame,
			layout: ToAnalyzer(layout ?? Layout(CommentWidth)),
			trailing: string.Empty,
			markParagraphs: true
		);
	}

	private static string CommentSource(string body)
	{
		return "class C\n{\n\t/// "
			+ body.Replace("\n", "\n\t/// ", StringComparison.Ordinal)
			+ "\n\tvoid M() { }\n}\n";
	}

	private static List<string> CommentLines(string body, DocLayoutOptions? layout = null) =>
		WrittenLines(CommentSource(body), layout);

	private static List<string> WrittenLines(string source, DocLayoutOptions? layout = null)
	{
		if (CommentText(source, layout) is not { } text)
		{
			Assert.Fail("The comment walk declined the body.");
			return [];
		}

		List<string> lines = [];

		foreach (string line in text.Split('\n'))
		{
			// The lines after the first repeat the indentation the caller hands the comment,
			// and the opening line stands outside it.
			string content = line.StartsWith('\t') ? line[1..] : line;

			Assert.StartsWith("///", content);

			content = content[3..];

			lines.Add(content.StartsWith(' ') ? content[1..] : content);
		}

		return lines;
	}

	private static List<string> CommentLinesWithoutPadding(string body)
	{
		string source = CommentSource(body);

		List<AnalyzerDocs.DocCommentEdit> padding = AnalyzerDocs.DocCommentEdits.CreatePaddingEdits(
			GetComment(source)
		);

		if (padding.Count > 0)
		{
			StringBuilder builder = new(source);

			for (int i = padding.Count - 1; i >= 0; i--)
				_ = builder.Remove(padding[i].Change.Span.Start, padding[i].Change.Span.Length);

			return WrittenLines(builder.ToString());
		}

		return CommentLines(body);
	}

	private static string MemberSource(string body)
	{
		return "<member name=\"X\">\n\t"
			+ body.Replace("\n", "\n\t", StringComparison.Ordinal)
			+ "\n</member>\n";
	}

	private static string MemberBody(
		string body,
		DocCleanupOptions cleanup = default,
		DocLayoutOptions? layout = null
	)
	{
		List<MarkupReprintedBody> bodies = Reprint(MemberSource(body), cleanup, layout);

		Assert.HasCount(1, bodies, "the markup reprint declined the body.");

		return bodies[0].Text;
	}

	private static List<string> MemberLines(string written)
	{
		// The body opens on a line of its own and closes on one:
		// the first and the last item of the split are the two tags' own lines,
		// and every line between them carries the indentation.
		string[] split = written.Split('\n');
		return [.. split[1..^1].Select(line => line.StartsWith('\t') ? line[1..] : line)];
	}

	private static List<MarkupReprintedBody> Reprint(
		string source,
		DocCleanupOptions cleanup = default,
		DocLayoutOptions? layout = null
	)
	{
		var sourceText = MarkupSourceText.Build(source);

		(MarkupElement? root, _) = MarkupParser.Parse(
			sourceText,
			MarkupLimits.DefaultMaxDocumentCharacters
		);

		return MarkupReprint.Collect(
			root,
			sourceText,
			reprintLayout: layout ?? Layout(CommentWidth + TabColumns),
			cleanup
		);
	}

	private static DocumentationCommentTriviaSyntax GetComment(string source)
	{
		return CSharpSyntaxTree
			.ParseText(source)
			.GetRoot()
			.DescendantTrivia(descendIntoTrivia: true)
			.Select(trivia => trivia.GetStructure())
			.OfType<DocumentationCommentTriviaSyntax>()
			.First();
	}

	private static DocLayoutOptions Layout(
		int contentWidth,
		int indentSize = DocLayoutOptions.DefaultIndentSize,
		bool indentWithTabs = false
	)
	{
		return new(
			ContentWidth: contentWidth,
			TabColumns,
			MinTailWords: DocLayoutOptions.DefaultMinTailWords,
			MinLineWords: DocLayoutOptions.DefaultMinLineWords,
			MaxOverflowColumns: DocLayoutOptions.DefaultMaxOverflowColumns,
			IndentSize: indentSize,
			IndentText: true,
			Expansion: DocExpansion.None,
			WeakTailWords: null,
			IndentWithTabs: indentWithTabs
		);
	}

	// The analyzer compiles its own copy of the engine, so a layout crosses over to it field by field.
	private static AnalyzerEngine.DocLayoutOptions ToAnalyzer(DocLayoutOptions layout)
	{
		return new(
			ContentWidth: layout.ContentWidth,
			TabColumns: layout.TabColumns,
			MinTailWords: layout.MinTailWords,
			MinLineWords: layout.MinLineWords,
			MaxOverflowColumns: layout.MaxOverflowColumns,
			IndentSize: layout.IndentSize,
			IndentText: layout.IndentText,
			Expansion: (AnalyzerEngine.DocExpansion)layout.Expansion,

			WeakTailWords: AnalyzerEngine.DocNameSet.Parse(
				layout.WeakTailWords?.ToString() ?? string.Empty
			),

			IndentWithTabs: layout.IndentWithTabs
		);
	}
}
