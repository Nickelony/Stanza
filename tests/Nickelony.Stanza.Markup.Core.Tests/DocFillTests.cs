using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Core.Tests;

/// <summary>Lays out words directly, so each term of a line cost has a row of its own.</summary>
[TestClass]
public sealed class DocFillTests
{
	[TestMethod]
	public void ATailShorterThanTheMinimumMovesTheBreakEarlier()
	{
		const string text = "alpha beta gamma delta, epsilon zeta eta theta iota kappa";

		Assert.AreEqual(
			"alpha beta gamma delta,|epsilon zeta eta theta iota kappa",
			Fill(text, width: 34, minTailWords: 2)
		);

		// With no minimum, the line may end on the one word of the clause that began after the comma.
		Assert.AreEqual(
			"alpha beta gamma delta, epsilon|zeta eta theta iota kappa",
			Fill(text, width: 34, minTailWords: 0)
		);
	}

	[TestMethod]
	public void ALineDoesNotOpenWithTheFewWordsThatCloseTheClauseBeforeIt()
	{
		const string text = "alpha beta gamma delta, epsilon zeta eta theta iota kappa";

		Assert.AreEqual(
			"alpha beta gamma delta,|epsilon zeta eta theta|iota kappa",
			Fill(text, width: 30, minTailWords: 2)
		);

		Assert.AreEqual(
			"alpha beta gamma|delta, epsilon zeta eta theta|iota kappa",
			Fill(text, width: 30, minTailWords: 0)
		);
	}

	[TestMethod]
	public void AFewerWordsThanTheMinimumReadAsALeftoverAndAreAvoided()
	{
		const string text = "one two three four five six seven eight nine ten eleven twelve";

		Assert.AreEqual(
			"one two|three four five six|seven eight nine ten|eleven twelve",
			Fill(text, width: 20, minLineWords: 2)
		);

		Assert.AreEqual(
			"one two three four|five six seven eight|nine ten eleven|twelve",
			Fill(text, width: 20, minLineWords: 1)
		);
	}

	[TestMethod]
	public void ALineMayStandPastTheWidthByTheToleratedColumnsWhenThatSavesALeftover()
	{
		const string text = "alpha beta gamma delta epsilon zeta eta";

		Assert.AreEqual(
			"alpha beta gamma|delta epsilon|zeta eta",
			Fill(text, width: 15, maxOverflowColumns: 4)
		);

		Assert.AreEqual(
			"alpha beta|gamma delta|epsilon zeta|eta",
			Fill(text, width: 15, maxOverflowColumns: 0)
		);
	}

	[TestMethod]
	public void ALineDoesNotEndOnAWeakWordWhenAnotherBreakCostsNoMore()
	{
		const string text =
			"Returns the value of the option and the other value when the section is missing";

		Assert.AreEqual(
			"Returns the value|of the option|and the other value|when the section|is missing",
			Fill(text, width: 20, weak: new MarkupNameSet("the", "of", "and"))
		);

		Assert.AreEqual(
			"Returns the value of|the option and the|other value when the|section is missing",
			Fill(text, width: 20, weak: null)
		);
	}

	// A mark that stands alone keeps the space the author wrote, and only a line break is kept away from it.
	[TestMethod]
	[DataRow("#, ?, and %")]
	[DataRow("(* and ?)")]
	[DataRow("[ U+0000 ]")]
	[DataRow("( see above )")]
	[DataRow("Returns the value .")]
	[DataRow("when empty ? returns otherwise the value , and so on ...")]
	[DataRow("an opening { brace and a closing } brace")]
	public void AMarkThatStandsAloneKeepsItsSpacing(string text) =>
		Assert.AreEqual(text, Fill(text, width: 200));

	[TestMethod]
	public void AMarkWithNoSpaceBeforeItStaysWithItsWord()
	{
		Assert.AreEqual("Returns the value. Done (see above).", Fill("Returns the value.   Done (see above).", width: 200));
	}

	[TestMethod]
	public void SpacesAroundALoneMarkStillCollapseToOne()
	{
		Assert.AreEqual("( see above ) and [ a ] .", Fill("(   see\tabove   )  and  [  a  ]   .", width: 200));
	}

	[TestMethod]
	public void NoLineBreakComesBetweenALoneMarkAndItsWord()
	{
		const string text = "alpha beta ( see the notes above ) gamma [ delta epsilon ] zeta eta , theta iota ? kappa lambda";

		for (int width = 5; width <= 60; width++)
		{
			string[] lines = Fill(text, width, minTailWords: 0, minLineWords: 1, maxOverflowColumns: 0).Split('|');

			for (int index = 0; index < lines.Length; index++)
			{
				string[] words = lines[index].Split(' ');

				if (index > 0)
					Assert.IsFalse(words[0] is ")" or "]" or "," or "?", $"Width {width} starts a line with a closing mark: {string.Join("|", lines)}");

				if (index < lines.Length - 1)
					Assert.IsFalse(words[^1] is "(" or "[", $"Width {width} ends a line on an opening mark: {string.Join("|", lines)}");
			}
		}
	}

	private static string Fill(
		string text,
		int width,
		int minTailWords = 2,
		int minLineWords = 2,
		int maxOverflowColumns = 4,
		MarkupNameSet? weak = null
	)
	{
		List<DocAtom> atoms = [];

		DocFill.AddWords(atoms, text, attached: false);

		DocLayoutOptions layout = MarkupFormatFixtures.ReprintLayout(contentWidth: width) with
		{
			MinTailWords = minTailWords,
			MinLineWords = minLineWords,
			MaxOverflowColumns = maxOverflowColumns,
			WeakLineEndWords = weak,
		};

		return string.Join("|", DocFill.LayOut(atoms, layout));
	}
}
