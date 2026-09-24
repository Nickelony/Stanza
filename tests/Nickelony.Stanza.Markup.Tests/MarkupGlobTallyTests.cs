using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Collection;
using Nickelony.Stanza.Markup.Notices;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupGlobTallyTests
{
	[TestMethod]
	[DataRow(
		2,
		0,
		1,
		1,
		"matched 2 file(s), but 1 of them is not claimed by a profile, and 1 of them is disabled in its .editorconfig"
	)]
	[DataRow(
		3,
		0,
		2,
		1,
		"matched 3 file(s), but 2 of them are not claimed by a profile, and 1 of them is disabled in its .editorconfig"
	)]
	[DataRow(1, 0, 0, 1, "matched 1 file(s), but 1 of them is disabled in its .editorconfig")]
	[DataRow(2, 0, 0, 2, "matched 2 file(s), but 2 of them are disabled in their .editorconfig")]
	[DataRow(
		3,
		1,
		2,
		0,
		"matched 2 file(s), but none of them is claimed by a profile or by their .editorconfig"
	)]
	public void EachReasonAMatchWasLeftOutIsAClauseOfItsOwn(
		int matched,
		int excluded,
		int unclaimed,
		int disabled,
		string message
	)
	{
		MarkupNotices notices = new(strict: false);

		MarkupGlobTally tally = new()
		{
			Matched = matched,
			Excluded = excluded,
			Unclaimed = unclaimed,
			Disabled = disabled,
		};

		tally.Report("*.xml", collected: 0, notices);

		Assert.HasCount(1, notices.Items);
		Assert.AreEqual($"'*.xml' {message}, so nothing was inspected.", notices.Items[0].Message);
	}
}
