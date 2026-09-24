using System.IO;
using System.Text;
using System.Threading;
using Nickelony.Stanza.Markup.Cli;
using Nickelony.Stanza.Markup.Collection;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Xml;
using Nickelony.Stanza.Markup.Files;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Markup.Paths;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

internal static class MarkupFixtures
{
	public static string EditorConfig(string body) => $"root = true\n\n{body}";

	internal sealed class FailingWriter(int failures) : StringWriter
	{
		private int _remainingFailures = failures;

		public override void WriteLine(string? value)
		{
			if (_remainingFailures <= 0)
			{
				base.WriteLine(value);
				return;
			}

			_remainingFailures--;

			throw new IOException("The channel went away.");
		}
	}

	internal static string WriteUndecodable(TempDirectory directory, string name)
	{
		string path = Path.Combine(directory.Root, name);

		File.WriteAllBytes(path, Encoding.Latin1.GetBytes("<root>café</root>\n"));

		return path;
	}

	internal static MarkupConfiguration CreateConfiguration(
		string workingDirectory,
		out MarkupNotices notices,
		bool reprintForced = false
	)
	{
		notices = new MarkupNotices(strict: false);
		return new MarkupConfiguration(new MarkupPaths(workingDirectory), notices, reprintForced);
	}

	internal static MarkupCollectionContext CreateCollectionContext(
		string workingDirectory,
		MarkupNotices notices,
		MarkupExcludeSet? excludes = null,
		CancellationToken cancellationToken = default
	)
	{
		MarkupPaths paths = new(workingDirectory);

		return new MarkupCollectionContext(
			paths,
			forced: null,
			configuration: new MarkupConfiguration(paths, notices),
			excludes: excludes ?? MarkupExcludeSet.Empty,
			notices,
			cancellationToken
		);
	}

	internal static RunResult Run(TempDirectory directory, params string[] args) =>
		RunIn(directory.Root, args);

	internal static RunResult RunIn(string workingDirectory, params string[] args)
	{
		using StringWriter output = new();
		using StringWriter error = new();

		int exitCode = MarkupTool.Run(args, output, error, workingDirectory);

		return new(exitCode, output.ToString(), error.ToString());
	}

	internal static string[] GetTemporaryFiles(TempDirectory directory)
	{
		return Directory.GetFiles(
			directory.Root,
			$"*{MarkupAtomicWrite.TemporarySuffix}",
			SearchOption.AllDirectories
		);
	}

	internal static int RunWithWriters(
		TempDirectory directory,
		TextWriter output,
		TextWriter error,
		string[] args
	) => MarkupTool.Run(args, output, error, workingDirectory: directory.Root);

	internal static MarkupFormatResult Format(string source, MarkupProfile profile) =>
		MarkupFormatFixtures.Format(source, new MarkupOptions(profile.Policy));

	internal static void AssertFormatsTo(string expected, string source, MarkupProfile profile) =>
		MarkupFormatFixtures.AssertFormatsTo(expected, source, profile.Policy);

	internal static void AssertChangesNothing(string source, MarkupProfile profile) =>
		MarkupFormatFixtures.AssertChangesNothing(source, new MarkupOptions(profile.Policy));
}
