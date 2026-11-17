using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Cli;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Notices;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupReporterTests
{
	[TestMethod]
	public void TheMsBuildShapeKeepsAMultiLineMessageOnOneLine()
	{
		using StringWriter output = new();
		using StringWriter error = new();

		MarkupEdit edit = new(
			Change: new MarkupChange(default, string.Empty),
			Line: 3,
			Message: "first\nsecond",
			Id: MarkupDiagnostics.SeparateMultiLineMarkupElementsId
		);

		new MarkupMsBuildReporter(output, error).ReportChanged("sample.xml", [edit], write: false);

		Assert.AreEqual(
			$"sample.xml(3,1): warning ZA2001: first second{System.Environment.NewLine}",
			output.ToString()
		);
	}

	[TestMethod]
	[DataRow("Warning", null, null, "stanza-markup : warning : first second")]
	[DataRow("Error", "sub/.editorconfig", null, "sub/.editorconfig : error : first second")]
	[DataRow("Warning", "sub/.editorconfig", 4, "sub/.editorconfig(4): warning : first second")]
	[DataRow("Note", null, null, "note: first second")]
	[DataRow("Note", "sub/b.xml", null, "note: sub/b.xml: first second")]
	public void TheMsBuildShapeWritesANoticeAsADiagnostic(
		string level,
		string? displayPath,
		int? line,
		string expected
	)
	{
		using StringWriter output = new();
		using StringWriter error = new();

		new MarkupMsBuildReporter(output, error).ReportNotice(
			new MarkupNotice(
				Message: "first\nsecond",
				Level: Enum.Parse<MarkupNoticeLevel>(level),
				DisplayPath: displayPath,
				Line: line
			)
		);

		Assert.AreEqual(expected + System.Environment.NewLine, output.ToString());
		Assert.AreEqual(string.Empty, error.ToString());
	}

	[TestMethod]
	[DataRow(null, null, "warning: first second")]
	[DataRow("sub", null, "warning: sub: first second")]
	[DataRow("sub/.editorconfig", 4, "warning: sub/.editorconfig(4): first second")]
	public void TheHumanShapeWritesANoticeOnOneLineToTheErrorStream(
		string? displayPath,
		int? line,
		string expected
	)
	{
		using StringWriter output = new();
		using StringWriter error = new();

		new MarkupHumanReporter(output, error).ReportNotice(
			new MarkupNotice(
				Message: "first\nsecond",
				Level: MarkupNoticeLevel.Warning,
				DisplayPath: displayPath,
				Line: line
			)
		);

		Assert.AreEqual(string.Empty, output.ToString());
		Assert.AreEqual(expected + System.Environment.NewLine, error.ToString());
	}

	[TestMethod]
	[DataRow(null, null, "a.xml")]
	[DataRow(4, null, "a.xml(4)")]
	[DataRow(4, 2, "a.xml(4,2)")]
	[DataRow(null, 2, "a.xml")]
	public void ALocationNamesTheLineAndTheColumnOnlyAfterALine(
		int? line,
		int? column,
		string expected
	)
	{
		Assert.AreEqual(expected, MarkupLocation.Format("a.xml", line, column));
	}

	[TestMethod]
	public void BothShapesWriteAFailureAtTheSameLocation()
	{
		using StringWriter humanOutput = new();
		using StringWriter humanError = new();
		using StringWriter msbuildOutput = new();
		using StringWriter msbuildError = new();

		new MarkupHumanReporter(humanOutput, humanError).ReportFailure(
			displayPath: "a.xml",
			message: "broke",
			line: 2,
			column: 3,
			isError: true
		);

		new MarkupMsBuildReporter(msbuildOutput, msbuildError).ReportFailure(
			displayPath: "a.xml",
			message: "broke",
			line: 2,
			column: 3,
			isError: true
		);

		Assert.StartsWith("error: a.xml(2,3): broke", humanError.ToString());
		Assert.StartsWith("a.xml(2,3): error ", msbuildOutput.ToString());
	}
}
