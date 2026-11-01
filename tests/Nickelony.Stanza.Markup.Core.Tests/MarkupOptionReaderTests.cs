using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Options;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupOptionReaderTests
{
	// The tab width comes from the first key that holds a positive count, and the default stands when none does.
	[TestMethod]
	[DataRow("tab_width=3;indent_size=5", 3)]
	[DataRow("tab_width=0;indent_size=5", 5)]
	[DataRow("tab_width=wide;indent_size=5", 5)]
	[DataRow("indent_size=5", 5)]
	[DataRow("indent_size=tab", 4)]
	[DataRow("", 4)]
	public void TheTabWidthFallsBackThroughItsKeys(string keys, int expected)
	{
		int columns = MarkupOptionReader.ResolveTabColumns(Parse(keys));
		Assert.AreEqual(expected, columns, keys);
	}

	// A value that is not a count in range is unset, so the next key applies, and the default stands when none does.
	[TestMethod]
	[DataRow("", 100)]
	[DataRow("max_line_length=120", 120)]
	[DataRow("max_line_length=off", 100)]
	[DataRow("max_line_length=wide", 100)]
	[DataRow("max_line_length=5000", 100)]
	[DataRow("max_line_length=-1", 100)]
	[DataRow("max_line_length=0", 100)]
	[DataRow("stanza_max_line_length=90;max_line_length=120", 90)]
	[DataRow("stanza_max_line_length=wide;max_line_length=120", 120)]
	[DataRow("stanza_max_line_length=5000;max_line_length=120", 120)]
	[DataRow("stanza_max_line_length=wide;max_line_length=off", 100)]
	[DataRow("stanza_max_line_length=0;max_line_length=120", 0)]
	public void TheCodeWidthFallsBackThroughItsKeys(string keys, int expected)
	{
		int width = MarkupOptionReader.ResolveMaxLineLength(Parse(keys));
		Assert.AreEqual(expected, width, keys);
	}

	// The documentation key comes first, then the chain of the code width.
	[TestMethod]
	[DataRow("", 100)]
	[DataRow("max_line_length=120", 120)]
	[DataRow("max_line_length=off", 100)]
	[DataRow("max_line_length=0", 100)]
	[DataRow("stanza_max_line_length=90;max_line_length=120", 90)]
	[DataRow("stanza_doc_max_line_length=70;stanza_max_line_length=90;max_line_length=120", 70)]
	[DataRow("stanza_doc_max_line_length=wide;stanza_max_line_length=90;max_line_length=120", 90)]
	[DataRow("stanza_doc_max_line_length=wide;max_line_length=120", 120)]
	[DataRow("stanza_doc_max_line_length=wide;max_line_length=off", 100)]
	[DataRow("stanza_doc_max_line_length=0;max_line_length=120", 0)]
	public void TheDocumentationWidthFallsBackThroughTheCodeWidth(string keys, int expected)
	{
		DocLayoutOptions? layout = MarkupOptionReader.ResolveDocLayout(Parse(keys));
		Assert.AreEqual(expected, layout?.ContentWidth ?? 0, keys);
	}

	private static Func<string, string?> Parse(string keys)
	{
		Dictionary<string, string> values = new(StringComparer.Ordinal);

		foreach (string pair in keys.Split(';', StringSplitOptions.RemoveEmptyEntries))
		{
			string[] parts = pair.Split('=');
			values[parts[0]] = parts[1];
		}

		return key => values.TryGetValue(key, out string? value) ? value : null;
	}
}
