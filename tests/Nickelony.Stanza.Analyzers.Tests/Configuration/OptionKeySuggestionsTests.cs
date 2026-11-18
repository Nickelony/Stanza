using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Options;

namespace Nickelony.Stanza.Analyzers.Tests.Configuration;

[TestClass]
public sealed class OptionKeySuggestionsTests
{
	[TestMethod]
	public void ASwapOfTwoNeighborsIsOneEdit()
	{
		Assert.AreEqual(
			OptionKeys.MaxLineLength,
			OptionKeySuggestions.FindNearest("stanza_max_line_lenght")
		);
	}

	[TestMethod]
	public void AKeyThreeEditsFromAKnownKeyIsSuggestedAndOneFourEditsAwayIsNot()
	{
		Assert.AreEqual(
			OptionKeys.MaxLineLength,
			OptionKeySuggestions.FindNearest("stanza_yyy_line_length")
		);

		Assert.AreEqual(
			OptionKeys.MaxLineLength,
			OptionKeySuggestions.FindNearest(OptionKeys.MaxLineLength + "xxx")
		);

		Assert.IsNull(OptionKeySuggestions.FindNearest("stanza_yyyy_line_length"));
		Assert.IsNull(OptionKeySuggestions.FindNearest(OptionKeys.MaxLineLength + "xxxx"));
	}

	[TestMethod]
	public void TwoKnownKeysAtTheSameDistanceTieToTheOneThatSortsFirst()
	{
		// Two letters of the line key and two of the tail key, so each is two edits away.
		string key = "stanza_doc_min_liil_words";

		Assert.AreEqual(OptionKeys.DocMinLineWords, OptionKeySuggestions.FindNearest(key));
		Assert.IsLessThan(0, string.CompareOrdinal(OptionKeys.DocMinLineWords, OptionKeys.DocMinTailWords));
	}

	[TestMethod]
	public void AKeyFarLongerThanAnyKnownKeyIsRefusedAtOnceWithoutATable()
	{
		Assert.IsNull(OptionKeySuggestions.FindNearest(new string('x', 100_000)));
		Assert.IsNull(OptionKeySuggestions.FindNearest(string.Empty));
	}
}
