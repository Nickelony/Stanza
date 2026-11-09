using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class StanzaOptionsTests
{
	[TestMethod]
	[DataRow("99999999999999999999")]
	[DataRow("-4")]
	[DataRow("not a number")]
	public void ACountTheReaderCannotTakeLeavesTheDefault(string value)
	{
		int fallback = StanzaOptions.From(FixedConfigOptions.Empty).MaxLineLength;

		Dictionary<string, string> values = new() { ["stanza_max_line_length"] = value };

		Assert.AreEqual(
			fallback,
			StanzaOptions.From(new FixedConfigOptions(values)).MaxLineLength,
			$"The value '{value}' reached the width."
		);
	}

	[TestMethod]
	[DataRow("stanza_max_line_length", 1000)]
	[DataRow("stanza_doc_max_line_length", 1000)]
	[DataRow("max_line_length", 1000)]
	[DataRow("tab_width", 64)]
	[DataRow("stanza_doc_indent_size", 64)]
	[DataRow("stanza_doc_min_tail_words", 1000)]
	[DataRow("stanza_doc_min_line_words", 1000)]
	[DataRow("stanza_doc_max_overflow_columns", 1000)]
	[DataRow("stanza_named_argument_min_arguments", 1000)]
	[DataRow("stanza_max_loose_run_size", 1000)]
	[DataRow("stanza_max_blank_lines", 1000)]
	[DataRow("stanza_comment_banner_min_run", 1000)]
	[DataRow("stanza_min_lines_to_name_endif", 1000)]
	[DataRow("stanza_markup_max_blank_lines", 1000)]
	public void ACountAboveItsCapLeavesTheDefault(string key, int cap)
	{
		int fallback = ReadNumber(key, FixedConfigOptions.Empty);
		int atCap = ReadNumber(key, WithKey(key, $"{cap}"));
		int aboveCap = ReadNumber(key, WithKey(key, $"{cap + 1}"));

		Assert.AreEqual(cap, atCap, "The cap itself was refused.");
		Assert.AreEqual(fallback, aboveCap, "A value above the cap was read.");
	}

	[TestMethod]
	[DataRow(null, null, null, 100, 100)]
	[DataRow(null, null, "120", 120, 120)]
	[DataRow(null, null, "off", 100, 100)]
	[DataRow(null, null, "wide", 100, 100)]
	[DataRow(null, "90", "120", 90, 90)]
	[DataRow(null, "wide", "120", 120, 120)]
	[DataRow(null, "wide", "off", 100, 100)]
	[DataRow("70", "90", "120", 90, 70)]
	[DataRow("wide", "90", "120", 90, 90)]
	[DataRow("wide", null, "120", 120, 120)]
	[DataRow("wide", "wide", "off", 100, 100)]
	public void TheWidthsFallBackThroughTheStandardKeyToOneHundred(
		string? docWidth,
		string? width,
		string? standardWidth,
		int expectedWidth,
		int expectedDocWidth
	)
	{
		Dictionary<string, string> values = [];

		if (docWidth is not null)
			values["stanza_doc_max_line_length"] = docWidth;

		if (width is not null)
			values["stanza_max_line_length"] = width;

		if (standardWidth is not null)
			values["max_line_length"] = standardWidth;

		var read = StanzaOptions.From(new FixedConfigOptions(values));

		Assert.AreEqual(expectedWidth, read.MaxLineLength, "the width of the code.");

		Assert.AreEqual(
			expectedDocWidth,
			read.DocLayout!.Value.ContentWidth,
			"the width of the comments."
		);
	}

	[TestMethod]
	public void ATabWidthComesFromTabWidthThenIndentSizeThenFour()
	{
		var neither = StanzaOptions.From(FixedConfigOptions.Empty);

		var indentSize = StanzaOptions.From(
			new FixedConfigOptions(new Dictionary<string, string> { ["indent_size"] = "5" })
		);

		var both = StanzaOptions.From(
			new FixedConfigOptions(
				new Dictionary<string, string> { ["tab_width"] = "3", ["indent_size"] = "5" }
			)
		);

		Assert.AreEqual(4, neither.TabColumns);
		Assert.AreEqual(5, indentSize.TabColumns);
		Assert.AreEqual(3, both.TabColumns);
	}

	[TestMethod]
	[DataRow(null, "Assert,Expect,Should", DisplayName = "an absent key")]
	[DataRow("", "Assert,Expect,Should", DisplayName = "an empty value")]
	[DataRow(" , ,", "Assert,Expect,Should", DisplayName = "a value of separators only")]
	[DataRow("none", "", DisplayName = "none")]
	[DataRow(" none ", "", DisplayName = "none with spaces around it")]
	[DataRow("none # no roots", "", DisplayName = "none before a comment")]
	[DataRow("None", "None", DisplayName = "None in another case")]
	[DataRow("none,", "none", DisplayName = "none with a separator")]
	[DataRow("none, Verify", "Verify,none", DisplayName = "none beside another name")]
	[DataRow("Verify, Check", "Check,Verify", DisplayName = "a list")]
	public void OnlyTheWordNoneListsNoAssertionRoots(string? value, string expected)
	{
		Assert.AreEqual(
			expected,
			ReadRoots("stanza_assertion_roots", value, static options => options.AssertionRoots)
		);
	}

	[TestMethod]
	[DataRow(null, "", DisplayName = "an absent key")]
	[DataRow("", "", DisplayName = "an empty value")]
	[DataRow(" , ,", "", DisplayName = "a value of separators only")]
	[DataRow("none", "", DisplayName = "none")]
	[DataRow(" none ", "", DisplayName = "none with spaces around it")]
	[DataRow("None", "None", DisplayName = "None in another case")]
	[DataRow("none, Guard", "Guard,none", DisplayName = "none beside another name")]
	[DataRow("Guard, Check", "Check,Guard", DisplayName = "a list")]
	public void OnlyTheWordNoneListsNoGuardRoots(string? value, string expected)
	{
		Assert.AreEqual(
			expected,
			ReadRoots("stanza_guard_roots", value, static options => options.GuardRoots)
		);
	}

	private static string ReadRoots(
		string key,
		string? value,
		Func<StanzaOptions, ImmutableHashSet<string>> select
	)
	{
		Dictionary<string, string> values = value is null ? [] : new() { [key] = value };
		ImmutableHashSet<string> roots = select(StanzaOptions.From(new FixedConfigOptions(values)));

		return string.Join(",", roots.OrderBy(static root => root, StringComparer.Ordinal));
	}

	private static FixedConfigOptions WithKey(string key, string value) =>
		new(new Dictionary<string, string> { [key] = value });

	private static int ReadNumber(string key, FixedConfigOptions options)
	{
		var read = StanzaOptions.From(options);

		return key switch
		{
			"stanza_max_line_length" => read.MaxLineLength,
			"max_line_length" => read.MaxLineLength,
			"tab_width" => read.TabColumns,
			"stanza_named_argument_min_arguments" => read.NamedArgumentMinArguments,
			"stanza_max_loose_run_size" => read.MaxLooseRunSize,
			"stanza_max_blank_lines" => read.MaxBlankLines,
			"stanza_comment_banner_min_run" => read.MinBannerRun,
			"stanza_min_lines_to_name_endif" => read.MinLinesToNameEndif,
			"stanza_markup_max_blank_lines" => read.Markup.MaxBlankLines,
			"stanza_doc_max_line_length" => read.DocLayout!.Value.ContentWidth,
			"stanza_doc_indent_size" => read.DocLayout!.Value.IndentSize,
			"stanza_doc_min_tail_words" => read.DocLayout!.Value.MinTailWords,
			"stanza_doc_min_line_words" => read.DocLayout!.Value.MinLineWords,
			"stanza_doc_max_overflow_columns" => read.DocLayout!.Value.MaxOverflowColumns,
			_ => throw new AssertFailedException($"No reader for {key}."),
		};
	}
}
