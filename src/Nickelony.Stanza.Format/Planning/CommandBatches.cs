using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Nickelony.Stanza.Format.Planning;

/// <summary>Splits the paths of one command into batches that each fit the command line.</summary>
internal static class CommandBatches
{
	/// <summary>
	/// Windows caps a command line near 32,000 characters, and <c>dotnet format</c> passes it on to a second process.
	/// </summary>
	public const int ArgumentBudget = 24_000;

	/// <summary>Each batch fits the argument budget, and a path longer than the budget takes a batch alone.</summary>
	public static IEnumerable<ImmutableArray<string>> Split(IEnumerable<string> paths)
	{
		List<string> batch = [];
		int length = 0;

		foreach (string path in paths)
		{
			int cost = MeasureArgument(path);

			if (batch.Count > 0 && length + cost > ArgumentBudget)
			{
				yield return [.. batch];

				batch.Clear();
				length = 0;
			}

			batch.Add(path);
			length += cost;
		}

		if (batch.Count > 0)
			yield return [.. batch];
	}

	public static int Measure(ImmutableArray<string> arguments) => arguments.Sum(MeasureArgument);

	/// <summary>Counts an argument with the space before it and the quotes around it.</summary>
	private static int MeasureArgument(string argument) => argument.Length + 3;
}
