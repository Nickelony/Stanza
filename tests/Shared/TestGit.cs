using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Format.Running;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>Runs git in a fixture repository and fails the test when git fails.</summary>
internal static class TestGit
{
	/// <summary>Runs git with the arguments in the repository.</summary>
	internal static void Run(string root, params string[] arguments) =>
		_ = Execute(root, arguments);

	/// <summary>Runs git in the repository and returns its trimmed output.</summary>
	internal static string Read(string root, params string[] arguments) =>
		Execute(root, arguments).Output.Trim();

	/// <summary>Creates a repository at the root.</summary>
	internal static void Init(string root) => Run(root, "init", "--quiet");

	/// <summary>Commits what the index holds, under an identity of the tests.</summary>
	internal static void Commit(string root, string message = "Commit the files the test changes")
	{
		Run(
			root,
			"-c",
			"user.name=Stanza Tests",
			"-c",
			"user.email=tests@stanza.invalid",
			"commit",
			"--quiet",
			"-m",
			message
		);
	}

	/// <summary>Adds every file and commits it.</summary>
	internal static void CommitAll(
		string root,
		string message = "Commit the files the test changes"
	)
	{
		Run(root, "add", ".");
		Commit(root, message);
	}

	private static ProcessResult Execute(string root, string[] arguments)
	{
		ProcessResult result = ProcessRunner.Run(
			fileName: "git",
			arguments,
			workingDirectory: root,
			echo: null,
			cancellationToken: CancellationToken.None
		);

		Assert.AreEqual(0, result.ExitCode, result.Error);

		return result;
	}
}
