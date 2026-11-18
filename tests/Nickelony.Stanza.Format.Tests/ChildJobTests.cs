using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Format.Tests;

[TestClass]
public sealed class ChildJobTests
{
	private static readonly TimeSpan s_startLimit = TimeSpan.FromSeconds(90);
	private static readonly TimeSpan s_endLimit = TimeSpan.FromSeconds(30);

	[TestMethod]
	public void ProcessesTheToolStartedEndWhenTheToolIsKilledHard()
	{
		if (!OperatingSystem.IsWindows())
			Assert.Inconclusive("Only Windows has the job that ties a child to the life of its parent.");

		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("App.slnx", "<Solution />\n");
		_ = directory.Write("A.cs", "class A { }\n");

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

		// Git runs this hook before it reads the files of the working tree, so a run of the tool waits in git.
		// The hook writes its own Windows process ID, so the test finds the process the tool's git started.
		File.WriteAllText(
			Path.Combine(directory.Root, "hook.sh"),
			"cat /proc/$$/winpid > pid.tmp\nsleep 600\n"
		);

		TestGit.Run(directory.Root, "config", "core.fsmonitor", "sh ./hook.sh");

		ProcessStartInfo start = new(DotnetHost.Executable)
		{
			WorkingDirectory = directory.Root,
			RedirectStandardError = true,
			RedirectStandardOutput = true,
			UseShellExecute = false,
		};

		start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Nickelony.Stanza.Format.dll"));
		start.ArgumentList.Add("--check");

		using Process tool = Process.Start(start)!;

		Process? hook = null;

		try
		{
			hook = WaitForHook(Path.Combine(directory.Root, "pid.tmp"));

			Assert.IsFalse(hook.HasExited, "The hook ended before the tool was killed.");

			// Only the tool itself is killed, so what ends the hook is the job and not the kill of a tree.
			tool.Kill();

			Assert.IsTrue(
				hook.WaitForExit(s_endLimit),
				"A process the tool started outlived the tool."
			);
		}
		finally
		{
			if (hook is { HasExited: false })
				hook.Kill(entireProcessTree: true);

			hook?.Dispose();

			if (!tool.HasExited)
				tool.Kill(entireProcessTree: true);
		}
	}

	private static Process WaitForHook(string pidFile)
	{
		DateTime limit = DateTime.UtcNow + s_startLimit;

		while (DateTime.UtcNow < limit)
		{
			if (File.Exists(pidFile) && TryReadId(pidFile, out int id))
				return Process.GetProcessById(id);

			Thread.Sleep(100);
		}

		throw new AssertFailedException("The tool started no process that waits.");
	}

	private static bool TryReadId(string pidFile, out int id)
	{
		// The hook may be part way through writing the file.
		try
		{
			return int.TryParse(File.ReadAllText(pidFile).Trim(), out id);
		}
		catch (IOException)
		{
			id = 0;
			return false;
		}
	}
}
