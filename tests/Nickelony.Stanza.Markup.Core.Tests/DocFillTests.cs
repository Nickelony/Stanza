using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.DocComments;

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
			Fill(text, width: 20, weak: new DocNameSet("the", "of", "and"))
		);

		Assert.AreEqual(
			"Returns the value of|the option and the|other value when the|section is missing",
			Fill(text, width: 20, weak: null)
		);
	}

	private static string Fill(
		string text,
		int width,
		int minTailWords = 2,
		int minLineWords = 2,
		int maxOverflowColumns = 4,
		DocNameSet? weak = null
	)
	{
		List<DocAtom> atoms = [];

		DocFill.AddWords(atoms, text, attached: false);

		var layout = new DocLayoutOptions(
			ContentWidth: width,
			TabColumns: 4,
			MinTailWords: minTailWords,
			MinLineWords: minLineWords,
			MaxOverflowColumns: maxOverflowColumns,
			IndentSize: 0,
			IndentText: true,
			Expansion: DocExpansion.None,
			WeakTailWords: weak
		);

		return string.Join("|", DocFill.LayOut(atoms, layout));
	}
}
