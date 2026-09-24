using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Options;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupOptionReaderTests
{
	// The tab width comes from the first key that holds a positive count, and the default stands when none does.
	[TestMethod]
	[DataRow("stanza_tab_width=2;tab_width=3;indent_size=5", 2)]
	[DataRow("tab_width=3;indent_size=5", 3)]
	[DataRow("indent_size=5", 5)]
	[DataRow("stanza_tab_width=0;tab_width=3", 3)]
	[DataRow("indent_size=tab", 4)]
	[DataRow("", 4)]
	public void TheTabWidthFallsBackThroughItsKeys(string keys, int expected)
	{
		Dictionary<string, string> values = new(StringComparer.Ordinal);

		foreach (string pair in keys.Split(';', StringSplitOptions.RemoveEmptyEntries))
		{
			string[] parts = pair.Split('=');
			values[parts[0]] = parts[1];
		}

		int columns = MarkupOptionReader.ResolveTabColumns(key =>
			values.TryGetValue(key, out string? value) ? value : null
		);

		Assert.AreEqual(expected, columns, keys);
	}
}
