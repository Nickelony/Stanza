using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Cli;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupCommandLineTests
{
	[TestMethod]
	public void ASecondCommandWordIsAPath()
	{
		MarkupCommandLine line = Parse(["format", "check", "a.xml"]);

		Assert.AreEqual(MarkupCommand.Format, line.Command);
		Assert.AreSequenceEqual(["check", "a.xml"], line.Paths);
	}

	[TestMethod]
	public void AFlagWrittenWithAValueStaysUnknown()
	{
		Assert.AreEqual(
			"unknown option '--strict=x'.",
			ReadError(["check", "--strict=x", "a.xml"])
		);
	}

	[TestMethod]
	[DataRow("--profile")]
	[DataRow("--profile=")]
	[DataRow("--exclude")]
	[DataRow("--exclude=")]
	[DataRow("--format")]
	[DataRow("--format=")]
	public void AnEmptyValueIsAMissingValue(string option)
	{
		string[] args = option.EndsWith('=')
			? ["check", option, "a.xml"]
			: ["check", option, string.Empty, "a.xml"];

		Assert.AreEqual($"the {option.TrimEnd('=')} option needs a value.", ReadError(args));
	}

	[TestMethod]
	public void AValueWrittenAfterAnEqualsSignIsTheValue()
	{
		MarkupCommandLine line = Parse([
			"check",
			"--profile=xml",
			"--exclude=bin/**",
			"--format=msbuild",
			"a.xml",
		]);

		Assert.AreEqual("xml", line.Profile);
		Assert.AreEqual(MarkupReportFormat.MsBuild, line.ReportFormat);
		Assert.AreSequenceEqual(["bin/**"], line.Excludes);
	}

	[TestMethod]
	public void AValueThatHoldsAnEqualsSignIsTakenWhole()
	{
		MarkupCommandLine line = Parse(["check", "--profile=a=b", "a.xml"]);
		Assert.AreEqual("a=b", line.Profile);
	}

	[TestMethod]
	[DataRow("--profile", "xml", "msbuild")]
	[DataRow("--format", "human", "msbuild")]
	public void ARepeatedValueOptionIsAnErrorWhateverItsSpelling(
		string option,
		string first,
		string second
	)
	{
		Assert.AreEqual(
			$"the {option} option was given more than once.",
			ReadError(["check", option, first, $"{option}={second}", "a.xml"])
		);
	}

	[TestMethod]
	[DataRow("human", "Human")]
	[DataRow("MSBuild", "MsBuild")]
	public void AReportFormatIsReadWhateverItsCase(string value, string format)
	{
		MarkupCommandLine line = Parse(["check", "--format", value, "a.xml"]);
		Assert.AreEqual(format, line.ReportFormat.ToString());
	}

	[TestMethod]
	public void AnUnknownReportFormatIsAnError()
	{
		Assert.AreEqual(
			"unknown report format 'json'. Known formats: human, msbuild.",
			ReadError(["check", "--format", "json", "a.xml"])
		);
	}

	[TestMethod]
	public void TheHumanFormatAppliesToFormat()
	{
		MarkupCommandLine line = Parse(["format", "--format", "human", "a.xml"]);
		Assert.AreEqual(MarkupReportFormat.Human, line.ReportFormat);
	}

	private static MarkupCommandLine Parse(string[] args)
	{
		Assert.IsTrue(
			MarkupCommandLine.TryParse(args, out MarkupCommandLine? line, out string? error),
			error
		);

		return line;
	}

	private static string? ReadError(string[] args)
	{
		Assert.IsFalse(MarkupCommandLine.TryParse(args, out _, out string? error));
		return error;
	}
}
