using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

/// <summary>Pins that the cost of a walk over a tree grows in step with its size.</summary>
internal static class LinearCost
{
	/// <summary>The growth bound at four times the input, which a quadratic walk exceeds.</summary>
	private const int AllowedGrowth = 6;

	/// <summary>The walks a timed measure makes, of which the fastest counts.</summary>
	private const int MeasuredRuns = 3;

	/// <summary>Asserts that four times the size allocates under six times the bytes.</summary>
	public static void AssertLinear(string small, string large, Action<SyntaxNode> walk)
	{
		// The bytes a walk allocates stand for the work it does, and unlike its time they do not depend on the machine.
		// The first walk of a tree builds its nodes, so each size is measured on a tree of its own after a warm-up.
		Measure(CSharpSyntaxTree.ParseText(small).GetRoot(), walk);

		long smallBytes = MeasureLowest(small, walk);
		long largeBytes = MeasureLowest(large, walk);

		Assert.IsGreaterThan(0L, smallBytes);

		Assert.IsLessThan(
			smallBytes * AllowedGrowth,
			largeBytes,
			$"{smallBytes:N0} bytes grew to {largeBytes:N0}."
		);
	}

	/// <summary>Asserts that four times the units cost a query under six times the bytes.</summary>
	public static async Task<(int Small, int Large)> AssertQueryLinearAsync(
		StanzaRule rule,
		Func<int, string> buildSource,
		int units,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		Func<ContainerContext, int> query,
		IReadOnlyDictionary<string, string>? options = null
	)
	{
		// A query that reads the whole container once per unit, where a cache reads it once,
		// grows with the square of the container.
		(long smallBytes, int smallCount) = await MeasureQueryAsync(
			rule,
			source: buildSource(units),
			rules,
			query,
			options
		);

		(long largeBytes, int largeCount) = await MeasureQueryAsync(
			rule,
			source: buildSource(units * 4),
			rules,
			query,
			options
		);

		Assert.IsGreaterThan(0L, smallBytes);

		Assert.IsLessThan(
			smallBytes * AllowedGrowth,
			largeBytes,
			$"{smallBytes:N0} bytes grew to {largeBytes:N0}."
		);

		return (smallCount, largeCount);
	}

	/// <summary>Counts the gaps of the container the rule claims.</summary>
	public static int CountClaims(ContainerContext container, StanzaRule rule)
	{
		int claimed = 0;

		for (int boundary = 1; boundary < container.Siblings.Length; boundary++)
		{
			if (BlankLineKeptPairs.Claims(container, boundary, rule))
				claimed++;
		}

		return claimed;
	}

	/// <summary>Asserts that four times the units allocate under six times the bytes.</summary>
	public static async Task AssertAnalysisLinearAsync(Func<int, string> buildSource, int units, RunOptions options, string message)
	{
		// The analyzers run on threads of their own, so the bytes the whole process allocates stand for them,
		// and a caller carries [DoNotParallelize], so no other test allocates during the run.
		long small = await MeasureAnalysisAsync(buildSource(units), options);
		long large = await MeasureAnalysisAsync(buildSource(units * 4), options);

		Assert.IsLessThan(small * AllowedGrowth, large, $"{message}: {small:N0} bytes grew to {large:N0}.");
	}

	/// <summary>Asserts that a run allocates under a multiple of its baseline.</summary>
	public static async Task AssertAnalysisCostWithinAsync(string source, RunOptions baseline, RunOptions measured, int factor, string message)
	{
		// A cost that grows with the rules on, and not with the source, passes a test of growth, so the rules are measured against a baseline.
		long cheap = await MeasureAnalysisAsync(source, baseline);
		long dear = await MeasureAnalysisAsync(source, measured);

		Assert.IsLessThan(cheap * factor, dear, $"{message}: {cheap:N0} bytes grew to {dear:N0}.");
	}

	private static async Task<long> MeasureAnalysisAsync(string source, RunOptions options)
	{
		long lowest = long.MaxValue;

		for (int run = 0; run < MeasuredRuns; run++)
		{
			long before = GC.GetTotalAllocatedBytes(precise: true);

			_ = await RoslynTestHarness.ReportAsync(source, options);
			lowest = Math.Min(lowest, GC.GetTotalAllocatedBytes(precise: true) - before);
		}

		return lowest;
	}

	// The analyzer runs the query on its own thread, so the thread's bytes count the query alone,
	// and the lowest run of the largest container stands for it.
	private static async Task<(long Bytes, int Count)> MeasureQueryAsync(
		StanzaRule rule,
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		Func<ContainerContext, int> query,
		IReadOnlyDictionary<string, string>? options
	)
	{
		ConcurrentBag<(int Units, long Bytes, int Count)> measures = [];

		var analyzer = new ContainerReadingAnalyzer(
			rule,

			container =>
			{
				long before = GC.GetAllocatedBytesForCurrentThread();

				int count = query(container);

				long bytes = GC.GetAllocatedBytesForCurrentThread() - before;

				measures.Add((container.Siblings.Length, bytes, count));
			}
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunOneAsync(
			source,
			analyzer,
			diagnosticOptions: rules,
			analyzerConfigOptions: options
		);

		Assert.IsEmpty(result.Diagnostics);
		Assert.IsFalse(measures.IsEmpty);

		int largest = measures.Max(static measure => measure.Units);

		(int _, long bytes, int count) = measures
			.Where(measure => measure.Units == largest)
			.MinBy(static measure => measure.Bytes);

		return (bytes, count);
	}

	/// <summary>Asserts that the fastest of a few walks ends within the limit.</summary>
	public static void AssertWalkEndsWithin(int milliseconds, string text, Action<SyntaxNode> walk)
	{
		// A walk of a long list that finds each neighbor by a pass over the list allocates the same bytes as one that does not.
		// So its time tells them apart, and the limit stands far above a linear walk.
		// A caller carries [DoNotParallelize], so no other test shares the machine with the walk.
		double fastest = MeasureMilliseconds(text, walk, runs: MeasuredRuns);
		Assert.IsLessThan(milliseconds, fastest, $"The walk took {fastest:N0} ms.");
	}

	private static double MeasureMilliseconds(string text, Action<SyntaxNode> walk, int runs)
	{
		double fastest = double.MaxValue;

		for (int run = 0; run < runs; run++)
		{
			SyntaxNode root = CSharpSyntaxTree.ParseText(text).GetRoot();
			var stopwatch = Stopwatch.StartNew();

			walk(root);

			fastest = Math.Min(fastest, stopwatch.Elapsed.TotalMilliseconds);
		}

		return fastest;
	}

	// A collection during a walk adds to its bytes, as a body's statements are held weakly and read again, so the lowest of a few stands for the walk.
	private static long MeasureLowest(string text, Action<SyntaxNode> walk)
	{
		long lowest = long.MaxValue;

		for (int run = 0; run < MeasuredRuns; run++)
			lowest = Math.Min(lowest, Measure(CSharpSyntaxTree.ParseText(text).GetRoot(), walk));

		return lowest;
	}

	private static long Measure(SyntaxNode root, Action<SyntaxNode> walk)
	{
		long before = GC.GetAllocatedBytesForCurrentThread();

		walk(root);

		return GC.GetAllocatedBytesForCurrentThread() - before;
	}
}
