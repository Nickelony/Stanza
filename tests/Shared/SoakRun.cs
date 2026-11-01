using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>Reads the seed count of a soak test, and limits its run by that count.</summary>
internal static class SoakRun
{
	internal const string Variable = "STANZA_SOAK_SEEDS";
	internal const string OffsetVariable = "STANZA_SOAK_SEED_OFFSET";

	/// <summary>The timeout of a soak test, which MSTest reads as none.</summary>
	internal const int NoRunnerTimeout = int.MaxValue;

	// A soak test carries [Timeout(NoRunnerTimeout)], since the attribute takes a constant and the count is not one.
	// The limit of its own below stands in for it, so a seed that never ends still fails the test and not the job.
	private static readonly TimeSpan s_base = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan s_perSeed = TimeSpan.FromMinutes(1);

	/// <summary>Reads the seed count of the run, and skips the test when there is none.</summary>
	internal static int ReadCountOrSkip(string reason)
	{
		bool isCount = int.TryParse(
			s: Environment.GetEnvironmentVariable(Variable),
			style: NumberStyles.None,
			provider: CultureInfo.InvariantCulture,
			result: out int count
		);

		if (!isCount || count < 1)
			ExpectedSkip.Skip(reason);

		return count;
	}

	/// <summary>Reads the first seed: the test's base moved by the offset.</summary>
	internal static int ReadFirstSeed(int baseSeed) =>
		ResolveFirstSeed(baseSeed, Environment.GetEnvironmentVariable(OffsetVariable));

	/// <summary>Moves the base seed by the offset, and fails when it is no number.</summary>
	internal static int ResolveFirstSeed(int baseSeed, string? offset)
	{
		if (string.IsNullOrEmpty(offset))
			return baseSeed;

		bool isOffset = int.TryParse(
			s: offset,
			style: NumberStyles.None,
			provider: CultureInfo.InvariantCulture,
			result: out int moved
		);

		if (!isOffset)
		{
			throw new AssertFailedException(
				$"{OffsetVariable} must be a whole number of seeds to skip, not '{offset}'."
			);
		}

		return checked(baseSeed + moved);
	}

	/// <summary>Names the seed of a run that threw other than a failed assertion.</summary>
	internal static string DescribeCrash(int seed, Exception exception)
	{
		string report =
			$"seed {seed}: {exception.GetType().Name}: {exception.Message.Split('\n')[0]}";

		// The frame that threw names the code at fault, so a crash in CI needs no local rerun to find it.
		string? frame = exception.StackTrace?.Split('\n')[0].Trim();

		return frame is null or "" ? report : $"{report} {frame}";
	}

	/// <summary>The time a soak of that many seeds may take: a base, and a minute a seed.</summary>
	internal static TimeSpan LimitFor(int count) => s_base + (s_perSeed * count);

	/// <summary>Runs the soak, and fails the test if it outlasts the limit of its count.</summary>
	internal static Task RunAsync(int count, Func<Task> soak) => RunAsync(LimitFor(count), soak);

	/// <summary>The soak tests of the assembly that keep the runner's default timeout.</summary>
	internal static string[] FindUntimed(Assembly assembly)
	{
		return
		[
			.. assembly
				.GetTypes()
				.SelectMany(type => type.GetMethods())
				.Where(method =>
					method
						.GetCustomAttributes<TestCategoryAttribute>()
						.Any(category => category.TestCategories.Contains("Soak"))
					&& method.GetCustomAttribute<TimeoutAttribute>() is null
				)
				.Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
				.Order(StringComparer.Ordinal),
		];
	}

	/// <summary>Runs the soak, and fails the test if it outlasts the limit.</summary>
	internal static async Task RunAsync(TimeSpan limit, Func<Task> soak)
	{
		var running = Task.Run(soak);

		if (await Task.WhenAny(running, Task.Delay(limit)).ConfigureAwait(false) != running)
		{
			// The soak may fail after the test has ended, and nothing reads that failure.
			_ = running.ContinueWith(static task => _ = task.Exception, TaskScheduler.Default);
			throw new AssertFailedException($"The soak did not end within {limit}.");
		}

		await running.ConfigureAwait(false);
	}
}
