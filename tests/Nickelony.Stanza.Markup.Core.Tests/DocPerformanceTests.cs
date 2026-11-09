using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Core.Tests;

// A quadratic pass makes billions of steps on each input, so a bound on the steps catches it without a clock.
// The steps are counted for the thread that runs the test, so the other tests' load cannot move the count.
// The two reads that count no steps are timed by a clock instead, and they run alone.
[TestClass]
public sealed class DocPerformanceTests
{
	// A linear pass makes a few steps for each item, where the quadratic one makes as many as the input is long.
	private const int StepsPerItem = 100;

	// A linear read of these inputs takes milliseconds, and the quadratic one tens of seconds.
	private const int MaxSeconds = 2;

	private const string Sentence =
		"Binds the options from the configuration section and returns the value when it is missing";

	private static readonly DocLayoutOptions s_layout = MarkupFormatFixtures.ReprintLayout(
		contentWidth: 60
	);

	[TestMethod]
	public void ALongParagraphIsFilledInLinearWork()
	{
		List<DocAtom> atoms = [];

		for (int line = 0; line < 2_000; line++)
			DocFill.AddWords(atoms, Sentence + " ", attached: false);

		DocFill.LayOutSteps = 0;

		List<string> lines = DocFill.LayOut(atoms, s_layout);

		AssertLinear(atoms.Count, DocFill.LayOutSteps);

		Assert.AreEqual(atoms.Count, lines.Sum(static line => line.Split(' ').Length));
	}

	// No break may fall inside a glued run, so the run stands on one line however far past the width it reaches.
	[TestMethod]
	public void ALongGluedRunIsFilledInLinearWork()
	{
		List<DocAtom> atoms = [];

		for (int index = 0; index < 40_000; index++)
			atoms.Add(new DocAtom("<c>a</c>", IsGlued: index > 0));

		DocFill.LayOutSteps = 0;

		List<string> lines = DocFill.LayOut(atoms, s_layout);

		AssertLinear(atoms.Count, DocFill.LayOutSteps);

		Assert.HasCount(1, lines);
	}

	[TestMethod]
	public void ManyKeywordsAreFoundInLinearWork()
	{
		StringBuilder text = new();

		for (int index = 0; index < 100_000; index++)
			text.Append("null ");

		DocTextRules.KeywordScanSteps = 0;

		List<DocKeyword> keywords = DocTextRules.FindKeywords(
			text.ToString(),
			DocTextRules.DefaultKeywords,
			startsElement: false
		);

		AssertLinear(text.Length, DocTextRules.KeywordScanSteps);

		Assert.HasCount(100_000, keywords);
	}

	// Every keyword of one token asks about the same word, so the token is read once, not once for each keyword.
	[TestMethod]
	public void ManyKeywordsInOneLongTokenAreSkippedInLinearWork()
	{
		StringBuilder text = new();

		for (int index = 0; index < 20_000; index++)
			text.Append("null/");

		DocTextRules.KeywordScanSteps = 0;

		List<DocKeyword> keywords = DocTextRules.FindKeywords(
			text.ToString(),
			DocTextRules.DefaultKeywords,
			startsElement: false
		);

		AssertLinear(text.Length, DocTextRules.KeywordScanSteps);

		Assert.IsEmpty(keywords);
	}

	// A string joined onto a growing string copies it each time, so the time alone tells these two passes apart.
	[TestMethod]
	[DoNotParallelize]
	public void ALongBacktickSpanOverManyLinesIsReadInLinearTime()
	{
		StringBuilder text = new("`a");

		for (int index = 0; index < 150_000; index++)
			text.Append("\nb");

		text.Append('`');

		List<DocAtom> atoms = [];
		DocWordReader reader = new();

		var clock = Stopwatch.StartNew();

		Assert.IsTrue(
			reader.TryRead(text.ToString(), atoms: () => atoms, readBreak: static _ => true)
		);

		Assert.IsLessThan(MaxSeconds, clock.Elapsed.TotalSeconds, "the read took too long.");
		Assert.HasCount(1, atoms);
		Assert.AreEqual(2 + (150_000 * 2) + 1, atoms[0].Text.Length);
	}

	[TestMethod]
	[DoNotParallelize]
	public void ALongRunOfClosingMarksIsReadInLinearTime()
	{
		StringBuilder text = new("a");

		for (int index = 0; index < 200_000; index++)
			text.Append(" )");

		List<DocAtom> atoms = [];

		var clock = Stopwatch.StartNew();

		DocFill.AddWords(atoms, text.ToString(), attached: false);

		Assert.IsLessThan(MaxSeconds, clock.Elapsed.TotalSeconds, "the read took too long.");
		Assert.HasCount(1, atoms);
		Assert.AreEqual(1 + 200_000, atoms[0].Text.Length);
	}

	// A step count above zero shows the code counts, and one under the bound shows it counts a linear pass.
	private static void AssertLinear(int items, long steps)
	{
		Assert.IsGreaterThan(0, steps, "the pass counted no steps.");
		Assert.IsLessThan((long)items * StepsPerItem, steps, "the pass took too many steps.");
	}
}
