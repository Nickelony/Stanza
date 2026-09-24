using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Format.Cli;
using Nickelony.Stanza.Format.Planning;

namespace Nickelony.Stanza.Format.Tests;

[TestClass]
public sealed class FormatCommandLineTests
{
	[TestMethod]
	public void ARunWithNoArgumentFormatsTheChangedFilesOfTheWorkingDirectory()
	{
		FormatCommandLine line = Parse([]);

		Assert.AreEqual(
			new FormatOptions(All: false, Check: false, Path: ".", Layout: null),
			line.Options
		);
	}

	[TestMethod]
	public void APositionalArgumentIsThePath()
	{
		FormatCommandLine line = Parse(["--all", "src/App.slnx"]);

		Assert.IsTrue(line.Options.All);
		Assert.AreEqual("src/App.slnx", line.Options.Path);
	}

	[TestMethod]
	public void ASecondPathIsRefused()
	{
		Assert.AreEqual(
			"only one path can be formatted, and 'a' was named before 'b'.",
			ReadError(["a", "b"])
		);
	}

	[TestMethod]
	public void ABareDashIsRefused() => Assert.AreEqual("unknown option '-'.", ReadError(["-"]));

	[TestMethod]
	public void TheEndOfTheOptionsLetsAPathStartWithADash() =>
		Assert.AreEqual("-app", Parse(["--", "-app"]).Options.Path);

	[TestMethod]
	[DataRow("csharpier", "CSharpier")]
	[DataRow("Whitespace", "Whitespace")]
	[DataRow("NONE", "None")]
	public void ALayoutIsReadWhateverItsCase(string value, string layout)
	{
		Assert.AreEqual(layout, Parse(["--layout", value]).Options.Layout.ToString());
		Assert.AreEqual(layout, Parse([$"--layout={value}"]).Options.Layout.ToString());
	}

	[TestMethod]
	public void AnUnknownLayoutNamesTheKnownOnes()
	{
		Assert.AreEqual(
			"unknown layout 'prettier'. Known layouts: csharpier, whitespace, none.",
			ReadError(["--layout", "prettier"])
		);
	}

	[TestMethod]
	[DataRow("--layout")]
	[DataRow("--layout=")]
	public void AnEmptyLayoutIsAMissingValue(string option) =>
		Assert.AreEqual("the --layout option needs a value.", ReadError([option]));

	[TestMethod]
	public void AnOptionIsNeverTakenAsTheLayout() =>
		Assert.AreEqual("the --layout option needs a value.", ReadError(["--layout", "--all"]));

	[TestMethod]
	public void AnUnknownOptionIsRefused() =>
		Assert.AreEqual("unknown option '--solution'.", ReadError(["--solution", "a.slnx"]));

	private static FormatCommandLine Parse(string[] args)
	{
		Assert.IsTrue(
			FormatCommandLine.TryParse(args, out FormatCommandLine? line, out string? error),
			error
		);

		return line;
	}

	private static string? ReadError(string[] args)
	{
		Assert.IsFalse(FormatCommandLine.TryParse(args, out _, out string? error));
		return error;
	}
}
