using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Cli;
using Nickelony.Stanza.Markup.Collection;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Shared;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Markup.Tests.MarkupFixtures;
using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupToolCancellationTests
{
	// The tool turns a cancel into this code on any system, and a shell reads 130 as the end of a run by SIGINT.
	[TestMethod]
	public void ACanceledRunExitsWithTheCodeAShellReadsAsSigint()
	{
		int exitCode = ToolHost.Run([], (_, _, _, _, _) => throw new OperationCanceledException());
		Assert.AreEqual(130, exitCode);
	}

	[TestMethod]
	public void ACanceledRunRaisesForAFile()
	{
		using TempDirectory directory = new();

		_ = directory.Write("sample.xml", LineFeed(Source));

		_ = Assert.ThrowsExactly<OperationCanceledException>(() =>
			RunCanceled(directory, "check", "sample.xml")
		);
	}

	// The report of the first file cancels the run, so the second is the one the next check stops before.
	[TestMethod]
	public void ACancelPartWayThroughAFormatRunKeepsTheFirstFileAndLeavesTheRest()
	{
		using TempDirectory directory = new();

		_ = directory.Write("a.xml", LineFeed(Source));
		_ = directory.Write("b.xml", LineFeed(Source));
		_ = directory.Write("c.xml", LineFeed(Source));

		using CancellationTokenSource cancellation = new();
		using CancelingWriter output = new(cancellation);
		using StringWriter error = new();

		string[] args = ["format", "."];

		_ = Assert.ThrowsExactly<OperationCanceledException>(() =>
			MarkupTool.Run(
				args,
				output,
				error,
				workingDirectory: directory.Root,
				cancellationToken: cancellation.Token
			)
		);

		Assert.AreEqual(LineFeed(Formatted), directory.Read("a.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("b.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("c.xml"));
	}

	// A cancel that comes while a scan walks the tree stops the walk at the entry after it.
	[TestMethod]
	public void ACancelDuringAScanStopsTheWalk()
	{
		using TempDirectory directory = new();

		_ = directory.Write("a.xml", LineFeed(Source));
		_ = directory.Write("b.xml", LineFeed(Source));
		_ = directory.Write("sub/c.xml", LineFeed(Source));

		using CancellationTokenSource cancellation = new();
		string root = directory.Root;

		MarkupCollectionContext context = CreateCollectionContext(
			root,
			new MarkupNotices(strict: false),
			cancellationToken: cancellation.Token
		);

		IEnumerable<FileSystemInfo> walk = MarkupDirectoryWalker.EnumerateEntries(root, context);
		List<string> walked = [];

		_ = Assert.ThrowsExactly<OperationCanceledException>(() =>
		{
			foreach (FileSystemInfo entry in walk)
			{
				walked.Add(entry.Name);
				cancellation.Cancel();
			}
		});

		Assert.HasCount(1, walked, string.Join(", ", walked));
	}

	// A directory lists whole before the walk yields from it, so a cancel has to stop the listing of a large one.
	[TestMethod]
	public void ACancelInsideTheListingOfADirectoryStopsTheListing()
	{
		using TempDirectory directory = new();
		using CancellationTokenSource cancellation = new();

		MarkupCollectionContext context = CreateCollectionContext(
			directory.Root,
			new MarkupNotices(strict: false),
			cancellationToken: cancellation.Token
		);

		List<FileSystemInfo> entries = [];

		_ = Assert.ThrowsExactly<OperationCanceledException>(() =>
			MarkupDirectoryWalker.CollectEntries(
				CancelWhileListing(cancellation, directory.Root),
				entries,
				context
			)
		);

		Assert.IsEmpty(entries);
	}

	private static IEnumerable<FileSystemInfo> CancelWhileListing(
		CancellationTokenSource cancellation,
		string root
	)
	{
		cancellation.Cancel();

		yield return new FileInfo(Path.Combine(root, "a.xml"));
		yield return new FileInfo(Path.Combine(root, "b.xml"));
	}

	private static void RunCanceled(TempDirectory directory, params string[] args)
	{
		using CancellationTokenSource cancellation = new();

		cancellation.Cancel();

		using StringWriter output = new();
		using StringWriter error = new();

		_ = MarkupTool.Run(
			args,
			output,
			error,
			workingDirectory: directory.Root,
			cancellationToken: cancellation.Token
		);
	}

	// Writing the line that reports a file cancels the run, as a person pressing Ctrl+C would between two files.
	private sealed class CancelingWriter(CancellationTokenSource cancellation) : StringWriter
	{
		public override void WriteLine(string? value)
		{
			base.WriteLine(value);
			cancellation.Cancel();
		}
	}
}
