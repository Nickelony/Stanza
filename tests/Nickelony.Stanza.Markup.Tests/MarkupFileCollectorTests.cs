using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Collection;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Markup.Paths;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Markup.Tests.MarkupFixtures;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupFileCollectorTests
{
	// Each file of an excluded directory would be left out one by one anyway, so only the walk's own entry shows the prune.
	[TestMethod]
	[DataRow("src")]
	[DataRow("src/**/*.xml")]
	public void AnExcludedDirectoryIsNeverEnteredByAScanOrAGlob(string argument)
	{
		using TempDirectory directory = new();
		_ = directory.Write("src/kept.xml", "<root><a/><b/></root>\n");
		_ = directory.Write("src/gen/dropped.xml", "<root><a/><b/></root>\n");

		using var denied = DeniedDirectory.DenyOrSkip(Path.Combine(directory.Root, "src", "gen"));

		MarkupNotices notices = new(strict: false);

		var excludes = MarkupExcludeSet.Resolve(
			["src/gen"],
			new MarkupPaths(directory.Root),
			notices
		);

		MarkupCollectionContext context = CreateCollectionContext(
			directory.Root,
			notices,
			excludes
		);

		List<MarkupInput> inputs = new MarkupFileCollector(context).Collect(argument);

		Assert.IsEmpty(
			notices.Items,
			string.Join(" | ", notices.Items.Select(static notice => notice.Message))
		);

		Assert.HasCount(1, inputs);
	}

	[TestMethod]
	public void ADirectoryWhoseNameHoldsAWildcardCharacterIsStillSelectable()
	{
		using TempDirectory directory = new();

		// The name holds wildcard characters and must reach the matcher literally.
		_ = directory.Write("stanza[1]/inner{2}/sub/a.xml", "<root><a/><b/></root>\n");

		MarkupNotices notices = new(strict: false);

		MarkupCollectionContext context = CreateCollectionContext(
			Path.Combine(directory.Root, "stanza[1]", "inner{2}"),
			notices
		);

		List<MarkupInput> inputs = new MarkupFileCollector(context).Collect("sub/*.xml");

		Assert.IsEmpty(
			notices.Items,
			string.Join(" | ", notices.Items.Select(static notice => notice.Message))
		);

		Assert.HasCount(1, inputs);
	}
}
