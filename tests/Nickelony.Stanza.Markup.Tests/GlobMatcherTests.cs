using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Paths;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class GlobMatcherTests
{
	[TestMethod]
	public void AQuestionMarkMatchesOneCharacterInsideASegment()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("a?.xml", "ab.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("a?.xml", "a.xml"), "one character is required.");
		Assert.IsFalse(GlobMatcher.IsMatch("a?.xml", "abc.xml"), "a single character only.");
	}

	[TestMethod]
	public void ASegmentWildcardDoesNotCrossASeparator()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("src/*.xml", "src/a.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("src/*.xml", "src/nested/a.xml"));
	}

	[TestMethod]
	public void ACrossingWildcardAlsoMatchesNoDirectoryAtAll()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("src/**/*.xml", "src/a/b/c.xml"));

		Assert.IsTrue(
			GlobMatcher.IsMatch("src/**/*.xml", "src/a.xml"),
			"**/ also matches nothing."
		);

		Assert.IsFalse(GlobMatcher.IsMatch("src/**/*.xml", "elsewhere/a.xml"));
	}

	// Linux allows a line break in a name, and a crossing wildcard still reaches the files below it.
	[TestMethod]
	public void ACrossingWildcardCrossesADirectoryNameThatHoldsALineBreak()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("src/**/*.xml", "src/a\nb/c.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("**", "a\nb/c.xml"));
	}

	[TestMethod]
	public void APatternDoesNotMatchANameThatOnlyAddsAFinalLineBreak()
	{
		Assert.IsFalse(GlobMatcher.IsMatch("*.xml", "a.xml\n"));
		Assert.IsFalse(GlobMatcher.IsMatch("src", "src\n"));
	}

	[TestMethod]
	public void ACharacterClassMatchesOneOfItsCharacters()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("file[0-9].xml", "file1.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("file[a-f].xml", "fileA.xml"), "matching ignores case.");
		Assert.IsFalse(GlobMatcher.IsMatch("file[0-9].xml", "filea.xml"));

		// A pattern that names a separator matches the whole path, so the class is matched inside its segment.
		Assert.IsTrue(GlobMatcher.IsMatch("src/file[0-9].xml", "src/file1.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("src/file[0-9].xml", "src/filea.xml"));
	}

	[TestMethod]
	public void ANegatedCharacterClassMatchesWhatIsNotListed()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("file[!0-9].xml", "filea.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("file[!0-9].xml", "file1.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("src/file[!0-9].xml", "src/filea.xml"));
	}

	[TestMethod]
	public void ACharacterClassNeverMatchesASeparator()
	{
		// Each pattern names a separator, so it matches the whole path and the class stands where the separator does.
		Assert.IsFalse(GlobMatcher.IsMatch("dir/a[!x]b", "dir/a/b"));

		Assert.IsFalse(
			GlobMatcher.IsMatch("dir/a[/]b", "dir/a/b"),
			"a class may not name the separator."
		);

		Assert.IsFalse(GlobMatcher.IsMatch("dir/a[.-0]b", "dir/a/b"), "a range may not cross it.");
		Assert.IsTrue(GlobMatcher.IsMatch("dir/a[!x]b", "dir/acb"));

		Assert.IsFalse(
			GlobMatcher.TryValidate("a[/]b", out string? namesTheSeparator),
			"a class that names the separator is refused."
		);

		Assert.IsNotNull(namesTheSeparator);

		Assert.IsFalse(
			GlobMatcher.TryValidate("a[.-0]b", out string? crossesTheSeparator),
			"a range that crosses the separator is refused."
		);

		Assert.IsNotNull(crossesTheSeparator);
	}

	[TestMethod]
	public void APatternTooLongForTheMatcherIsRefusedInPlainWords()
	{
		Assert.IsTrue(GlobMatcher.TryValidate(new string('a', 150), out _));
		Assert.IsFalse(GlobMatcher.TryValidate(new string('a', 250), out string? refusal));

		Assert.AreEqual(
			"it is longer than the matcher's limit of about 200 literal characters or wildcards",
			refusal
		);
	}

	// The length is refused before the translation, which reads the rest of the pattern at each brace.
	[TestMethod]
	[DoNotParallelize]
	public void APatternOfThousandsOfBracesIsRefusedByLengthAtOnce()
	{
		var stopwatch = Stopwatch.StartNew();

		Assert.IsFalse(GlobMatcher.TryValidate(Repeat("{", 100_000), out string? refusal));
		Assert.IsLessThan(2_000, stopwatch.ElapsedMilliseconds, "the refusal took too long.");
		Assert.AreEqual("it is longer than 1024 characters", refusal);
	}

	[TestMethod]
	public void ThePatternLengthLimitIsOneThousandAndTwentyFourCharacters()
	{
		Assert.IsFalse(GlobMatcher.TryValidate(Repeat("a", 1_024), out string? atTheLimit));
		Assert.DoesNotContain("1024 characters", atTheLimit);

		Assert.IsFalse(GlobMatcher.TryValidate(Repeat("a", 1_025), out string? pastIt));
		Assert.AreEqual("it is longer than 1024 characters", pastIt);
	}

	// The limit is the size of the compiled matcher, which grows by one node for each literal or wildcard.
	[TestMethod]
	public void TheMatcherLimitCountsLiteralsAndWildcardsNotCharacters()
	{
		Assert.IsTrue(GlobMatcher.TryValidate(Repeat("[a-c]", 150), out _));
		Assert.IsFalse(GlobMatcher.TryValidate(Repeat("[a-c]", 250), out _));
		Assert.IsTrue(GlobMatcher.TryValidate(Repeat("?", 150), out _));
		Assert.IsFalse(GlobMatcher.TryValidate(Repeat("?", 250), out _));
	}

	[TestMethod]
	[DataRow(@"src\*.xml", 4)]
	[DataRow(@"a\?b", 2)]
	[DataRow(@"a\{b,c}", 2)]
	[DataRow(@"a\[b]", 2)]
	[DataRow(@"a\\*", -1)]
	[DataRow(@"a\.b", -1)]
	[DataRow("a/*.xml", -1)]
	public void AnEscapedWildcardIsFoundWhereTheBackslashEscapesIt(string pattern, int index) =>
		Assert.AreEqual(index, GlobMatcher.FindEscapedWildcard(pattern));

	[TestMethod]
	public void AnAlternationMatchesEitherAlternative()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("*.{xml,config}", "a.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("*.{xml,config}", "a.config"));
		Assert.IsFalse(GlobMatcher.IsMatch("*.{xml,config}", "a.txt"));
	}

	[TestMethod]
	public void ANestedAlternationKeepsTheInnerOneTogether()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("{a,{b,c}}.xml", "a.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("{a,{b,c}}.xml", "b.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("{a,{b,c}}.xml", "c.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("{a,{b,c}}.xml", "d.xml"));
	}

	[TestMethod]
	public void ALeadingSlashRootsThePattern()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("/src/*.xml", "src/a.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("/a.xml", "a.xml"));

		Assert.IsFalse(
			GlobMatcher.IsMatch("/a.xml", "nested/a.xml"),
			"a rooted pattern is not a file name."
		);
	}

	[TestMethod]
	public void ARootedPatternAlsoMatchesAnAbsoluteCandidate()
	{
		Assert.IsTrue(
			GlobMatcher.IsMatch("/home/u/p/*.xml", "/home/u/p/a.xml"),
			"an absolute candidate is read without its leading separator."
		);

		Assert.IsFalse(
			GlobMatcher.IsMatch("/home/u/p/*.xml", "/home/u/other/a.xml"),
			"the whole rooted path still has to match."
		);
	}

	[TestMethod]
	public void ARootedPatternReadsTheAbsoluteCandidateTheWalkHandsIt()
	{
		Assert.IsTrue(
			GlobMatcher.IsMatch(
				"/tmp/stanza\\[1]/inner\\{2}/sub/*.xml",
				"/tmp/stanza[1]/inner{2}/sub/a.xml"
			)
		);
	}

	[TestMethod]
	public void AWorkingDirectoryPrefixIsDropped()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("./bin/**", "bin/x.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("./bin/**", "other/x.xml"));

		Assert.IsTrue(GlobMatcher.IsMatch("./*.xml", "a.xml"));

		Assert.IsFalse(
			GlobMatcher.IsMatch("./*.xml", "nested/a.xml"),
			"the dropped prefix still named a directory."
		);
	}

	[TestMethod]
	public void APatternWithoutASeparatorMatchesTheFileNameAlone()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("*.xml", "nested/deeper/a.xml"));

		Assert.IsTrue(
			GlobMatcher.IsMatch("a.xml", "nested/a.xml"),
			"it matches in every directory."
		);

		Assert.IsFalse(
			GlobMatcher.IsMatch("a.xml", "nested/b.xml"),
			"the name must be the one asked for."
		);
	}

	// The EditorConfig reference and Roslyn read a backslash as an escape, so a section reaches the same files in both.
	[TestMethod]
	public void ABackslashBeforeAnOrdinaryCharacterEscapesItToo()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("sub\\s.xml", "nested/subs.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("sub\\s.xml", "sub/s.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("src/a.xml", "src/a.xml"));
	}

	[TestMethod]
	public void ABackslashEscapesEachMetacharacterAfterIt()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("file\\,x.xml", "file,x.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("file\\,x.xml", "file/,x.xml"));

		Assert.IsTrue(GlobMatcher.IsMatch("file\\]x.xml", "file]x.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("file\\}x.xml", "file}x.xml"));

		Assert.IsTrue(
			GlobMatcher.TryValidate("file\\\\x.xml", out _),
			"an escaped backslash is not a separator."
		);

		Assert.IsFalse(GlobMatcher.IsMatch("file\\\\x.xml", "file/x.xml"));
	}

	[TestMethod]
	[DataRow("a\\#b.xml", "a#b.xml")]
	[DataRow("a\\;b.xml", "a;b.xml")]
	public void ABackslashEscapesACommentCharacter(string pattern, string name)
	{
		Assert.IsTrue(GlobMatcher.IsMatch(pattern, name));

		Assert.IsFalse(
			GlobMatcher.IsMatch(pattern, $"a/{name[2..]}"),
			"an escaped comment character is not a separator."
		);
	}

	[TestMethod]
	public void ABracketClassInsideAnAlternationHoldsItsClosingBrace()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("{[}],b}.xml", "}.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("{[}],b}.xml", "b.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("{[}],b}.xml", "{},b}.xml"));
	}

	[TestMethod]
	public void AnEscapedCharacterDoesNotSplitOrCloseAnAlternation()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("{a\\,b,c}.xml", "a,b.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("{a\\,b,c}.xml", "c.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("{a\\,b,c}.xml", "a.xml"));

		Assert.IsTrue(GlobMatcher.IsMatch("file{a\\}b,c}.xml", "filea}b.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("file{a\\}b,c}.xml", "filea.xml"));

		Assert.IsTrue(GlobMatcher.IsMatch("file{a\\{b,c}.xml", "filea{b.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("file{a\\[b,c}.xml", "filea[b.xml"));
	}

	[TestMethod]
	public void ABackslashEscapesTheWildcardAfterIt()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("file\\*.xml", "file*.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("file\\*.xml", "file/a.xml"));

		Assert.IsTrue(GlobMatcher.IsMatch("file\\?.xml", "file?.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("file\\[0-9].xml", "file[0-9].xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("file\\[0-9].xml", "file1.xml"));

		Assert.IsTrue(GlobMatcher.IsMatch("file\\{a,b}.xml", "file{a,b}.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("file\\{a,b}.xml", "filea.xml"));

		Assert.IsFalse(
			GlobMatcher.TryValidate("file\\", out string? error),
			"a backslash at the end escapes nothing."
		);

		Assert.IsNotNull(error);
	}

	[TestMethod]
	public void AMatchIgnoresCase()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("*.XML", "a.xml"));
		Assert.IsTrue(GlobMatcher.IsMatch("*.xml", "A.XML"));
		Assert.IsTrue(GlobMatcher.IsMatch("src/*.xml", "SRC/A.XML"));
	}

	[TestMethod]
	public void AnAlternationNestedPastTheLimitIsRefused()
	{
		// Nesting past the limit is refused, so the translation cannot overflow the stack.
		string pattern = string.Concat(Enumerable.Repeat("{a,", 200)) + "a" + new string('}', 200);

		Assert.IsFalse(
			GlobMatcher.TryValidate(pattern, out string? error),
			"too deep to translate."
		);

		Assert.IsNotNull(error);
		Assert.IsFalse(GlobMatcher.IsMatch(pattern, "a"), "a refused pattern matches nothing.");

		Assert.IsTrue(GlobMatcher.IsMatch("{a,{b,c}}", "b"));
	}

	[TestMethod]
	public void AnUndocumentedCharacterClassIsRejected()
	{
		Assert.IsFalse(GlobMatcher.TryValidate("[^abc].xml", out string? caret), "no ^ negation.");
		Assert.IsNotNull(caret);

		Assert.IsFalse(
			GlobMatcher.TryValidate("[a[b]].xml", out string? nested),
			"no nested bracket."
		);

		Assert.IsNotNull(nested);

		Assert.IsFalse(
			GlobMatcher.TryValidate("[z-a].xml", out string? range),
			"no reversed range."
		);

		Assert.IsNotNull(range);

		Assert.IsTrue(
			GlobMatcher.TryValidate("file[0-9].xml", out string? valid),
			"the subset is accepted."
		);

		Assert.IsNull(valid);
	}

	[TestMethod]
	public void APatternWithManyCrossingsGetsTheSameAnswerAsAShortOne()
	{
		// Many wildcard crossings must still finish and give the answer of the short control.
		string candidate = new string('a', 40) + "b";

		// The miss ends without the final letter, the shape that makes a backtracking matcher run forever.
		string miss = new('a', 40);

		string control = "*" + string.Concat(Enumerable.Repeat("**a", 4)) + "b";
		string pattern = "*" + string.Concat(Enumerable.Repeat("**a", 40)) + "b";

		Assert.IsTrue(
			GlobMatcher.IsMatch(control, candidate),
			"the control proves the candidate is one this shape selects."
		);

		Assert.IsFalse(
			GlobMatcher.IsMatch(control, miss),
			"the control proves the miss is one this shape rejects."
		);

		// The matcher takes no cancellation token, so a bounded wait fails a backtracking regression instead of hanging the run.
		Task<(bool Hit, bool Miss)> crossings = Task.Run(() =>
			(GlobMatcher.IsMatch(pattern, candidate), GlobMatcher.IsMatch(pattern, miss))
		);

		Assert.IsTrue(
			crossings.Wait(TimeSpan.FromSeconds(10)),
			"forty crossings did not finish within ten seconds."
		);

		Assert.IsTrue(crossings.Result.Hit, "forty crossings are decided like four of them.");
		Assert.IsFalse(crossings.Result.Miss, "forty crossings reject the miss like four of them.");
	}

	[TestMethod]
	[DataRow("file{1..3}.xml")]
	[DataRow("file{-1..4}.xml")]
	[DataRow("file{1..-4}.xml")]
	[DataRow("file{-3..-1}.xml")]
	public void ANumericRangeIsRefusedWithAReasonWhateverTheSignOfItsBounds(string pattern)
	{
		Assert.IsFalse(GlobMatcher.TryValidate(pattern, out string? error));
		Assert.IsNotNull(error);

		Assert.Contains(
			"num1..num2",
			error,
			$"the refusal has to name the form it refused, but said '{error}'."
		);
	}

	[TestMethod]
	public void ABraceThatIsNotAnAlternationKeepsItsLiteralTranslation()
	{
		Assert.IsTrue(GlobMatcher.TryValidate("file{x}.xml", out string? error));
		Assert.IsNull(error);
		Assert.IsTrue(GlobMatcher.IsMatch("file{x}.xml", "file{x}.xml"));
	}

	[TestMethod]
	[DataRow("file[a.xml", "the character class is not closed")]
	[DataRow("file[].xml", "the character class is not closed")]
	[DataRow("file[!].xml", "the character class is empty")]
	[DataRow("file[a\\b].xml", "the character class holds a character the dialect does not accept")]
	public void AnInvalidPatternIsRefusedForTheReasonItsFormGives(string pattern, string reason)
	{
		Assert.IsFalse(GlobMatcher.TryValidate(pattern, out string? error), pattern);
		Assert.AreEqual(reason, error, pattern);
	}

	[TestMethod]
	public void AnUnclosedBraceIsALiteralCharacter()
	{
		Assert.IsTrue(GlobMatcher.TryValidate("file{a,b.xml", out string? error));
		Assert.IsNull(error);
		Assert.IsTrue(GlobMatcher.IsMatch("file{a,b.xml", "file{a,b.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("file{a,b.xml", "filea.xml"));
	}

	// The translation recurses once for each level of braces, so it refuses the level past the sixteenth.
	[TestMethod]
	public void AlternationsNestedSixteenLevelsAreTranslatedAndSeventeenAreRefused()
	{
		Assert.IsTrue(GlobMatcher.TryValidate(Nest(16), out string? sixteen), sixteen);
		Assert.IsTrue(GlobMatcher.IsMatch(Nest(16), "x"));

		Assert.IsFalse(GlobMatcher.TryValidate(Nest(17), out string? seventeen));
		Assert.AreEqual("the alternation nests more than 16 levels deep", seventeen);
	}

	[TestMethod]
	public void ABraceHoldingTwoDotsAroundLettersIsNotARangeAndStaysLiteral()
	{
		Assert.IsTrue(GlobMatcher.TryValidate("file{a..b}.xml", out string? error), error);
		Assert.IsTrue(GlobMatcher.IsMatch("file{a..b}.xml", "file{a..b}.xml"));
		Assert.IsFalse(GlobMatcher.IsMatch("file{a..b}.xml", "filea.xml"));
	}

	// A closing bracket with nothing open is a literal, so the comma after it still splits the alternatives.
	[TestMethod]
	public void AClosingBracketWithNothingOpenDoesNotHideTheNextComma()
	{
		Assert.IsTrue(GlobMatcher.IsMatch("{a],b}", "a]"));
		Assert.IsTrue(GlobMatcher.IsMatch("{a],b}", "b"));
		Assert.IsFalse(GlobMatcher.IsMatch("{a],b}", "a],b"));
	}

	[TestMethod]
	[DataRow("{1..3}x[]", "the {num1..num2} range form is not supported")]
	[DataRow("[]x{1..3}", "the character class is not closed")]
	[DataRow("x\\", "a backslash at the end of a pattern escapes nothing")]
	public void APatternWithSeveralRefusalsReportsTheFirstOneItReaches(
		string pattern,
		string reason
	)
	{
		Assert.IsFalse(GlobMatcher.TryValidate(pattern, out string? error), pattern);
		Assert.AreEqual(reason, error, pattern);
	}

	private static string Nest(int levels) =>
		string.Concat(Enumerable.Repeat("{x,", levels)) + "x" + new string('}', levels);

	private static string Repeat(string part, int count) =>
		string.Concat(Enumerable.Repeat(part, count));
}
