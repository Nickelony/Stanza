using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
