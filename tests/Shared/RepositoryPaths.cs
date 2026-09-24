using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Tests.Shared;

internal static class RepositoryPaths
{
	private const string SolutionFile = "Nickelony.Stanza.slnx";

	// A missing root fails rather than skipping the guards.
	internal static string Root()
	{
		string? root = null;
		DirectoryInfo? directory = new(AppContext.BaseDirectory);

		while (root is null && directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
				root = directory.FullName;
			else
				directory = directory.Parent;
		}

		if (root is null)
		{
			Assert.Fail(
				$"The repository root could not be found above '{AppContext.BaseDirectory}', so the "
					+ "repository guards cannot run and this build proves nothing about them. The full "
					+ "repository checkout is needed; a copied or trimmed test output cannot stand in."
			);
		}

		return root!;
	}

	internal static string Below(params string[] segments) => Path.Combine([Root(), .. segments]);
}
