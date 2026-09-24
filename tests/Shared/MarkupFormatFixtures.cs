using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>The engine's sample texts and formatting helpers for both test projects.</summary>
internal static class MarkupFormatFixtures
{
	/// <summary>The xml profile's policy, which the tool's profile table holds too.</summary>
	public static readonly MarkupPolicy XmlPolicy = new(ContainersOnly: false);

	internal const string MultiLineSiblings = """
		<root>
		  <a>
		    one
		  </a>
		  <b>
		    two
		  </b>
		</root>
		""";

	internal const string Source = MultiLineSiblings + "\n";

	internal const string Formatted = """
		<root>
		  <a>
		    one
		  </a>

		  <b>
		    two
		  </b>
		</root>

		""";

	public static string LineFeed(string text) => text.Replace("\r\n", "\n");

	public static MarkupFormatResult Format(string source) =>
		MarkupFormatter.Format(source, new MarkupOptions(XmlPolicy));

	public static string Member(string body) => $"<member name=\"X\">\n  {body}\n</member>";

	public static MarkupRuleSet Rules(params string[] ids) => MarkupRuleSet.From(ids);

	internal static MarkupFormatResult FormatReprint(
		string source,
		MarkupRuleSet rules = default,
		int contentWidth = 12,
		int indentSize = DocLayoutOptions.DefaultIndentSize,
		bool indentWithTabs = false
	)
	{
		return MarkupFormatter.Format(
			source,
			new MarkupOptions(XmlPolicy, Rules: rules),
			reprintLayout: ReprintLayout(contentWidth, indentSize, indentWithTabs)
		);
	}

	internal static DocLayoutOptions ReprintLayout(
		int contentWidth,
		int indentSize = DocLayoutOptions.DefaultIndentSize,
		bool indentWithTabs = false
	)
	{
		return new DocLayoutOptions(
			ContentWidth: contentWidth,
			TabColumns: 4,
			MinTailWords: DocLayoutOptions.DefaultMinTailWords,
			MinLineWords: DocLayoutOptions.DefaultMinLineWords,
			MaxOverflowColumns: DocLayoutOptions.DefaultMaxOverflowColumns,
			IndentSize: indentSize,
			IndentText: true,
			Expansion: DocExpansion.None,
			IndentWithTabs: indentWithTabs
		);
	}

	internal static MarkupFormatResult Format(
		string source,
		MarkupOptions options,
		long maxDocumentCharacters = MarkupLimits.DefaultMaxDocumentCharacters
	)
	{
		return MarkupFormatter.Format(
			source,
			options,
			maxDocumentCharacters: maxDocumentCharacters,
			cancellationToken: CancellationToken.None
		);
	}

	internal static void AssertFormatsTo(string expected, string source) =>
		AssertFormatsTo(expected, source, XmlPolicy);

	internal static void AssertFormatsTo(string expected, string source, MarkupPolicy policy)
	{
		// A source that is already the expected text pins nothing, which AssertChangesNothing states.
		Assert.AreNotEqual(LineFeed(expected), LineFeed(source));

		MarkupFormatResult result = Format(LineFeed(source), new MarkupOptions(policy));

		Assert.IsNull(result.Failure);
		Assert.AreEqual(LineFeed(expected), result.Text);
	}

	internal static void AssertChangesNothing(string source) =>
		AssertChangesNothing(source, new MarkupOptions(XmlPolicy));

	internal static void AssertChangesNothing(string source, MarkupOptions options)
	{
		MarkupFormatResult result = Format(LineFeed(source), options);

		Assert.IsNull(result.Failure);
		Assert.IsEmpty(result.Edits);
	}
}
