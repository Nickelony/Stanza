using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class DocCleanupConfigurationTests
{
	[TestMethod]
	public void TheSeverityLinesDriveTheCleanUpsOfAFile()
	{
		string source = Member("<summary>alpha beta gamma</summary>");
		string period = "dotnet_diagnostic.ZA0210.severity = warning\n";

		Assert.Contains("gamma.", FormatFromConfiguration(source, period).Text);
		Assert.DoesNotContain("gamma.", FormatFromConfiguration(source, string.Empty).Text);
	}

	[TestMethod]
	public void TheTextAfterAParagraphMarkerOpensAParagraphOfItsOwn()
	{
		string source = Member("<remarks>\n    <para>First.</para>\n    Second.\n  </remarks>");

		Assert.AreEqual(
			Member(
				"<remarks>\n  <para>\n  First.\n  </para>\n  <para>\n  Second.\n  </para>\n  </remarks>"
			),

			FormatFromConfiguration(source, string.Empty).Text
		);
	}

	[TestMethod]
	public void ARealBreakIsMarkedThoughABlankLineTrailsTheText()
	{
		string source = Member("<remarks>\n    First.\n\n    Second.\n\n  </remarks>");

		Assert.AreEqual(
			Member(
				"<remarks>\n  <para>\n  First.\n  </para>\n  <para>\n  Second.\n  </para>\n  </remarks>"
			),

			FormatFromConfiguration(source, string.Empty).Text
		);
	}

	[TestMethod]
	public void AnElementWithAnEndTagAndNoContentDoesNotStopTheReprint()
	{
		string text = FormatFromConfiguration(
			Member("<summary>alpha <c></c> beta</summary>\n  <param name=\"x\"></param>"),
			"dotnet_diagnostic.ZA0210.severity = warning\n"
		).Text;

		Assert.Contains("alpha <c></c> beta.", text);
		Assert.Contains("<param name=\"x\"></param>", text);
	}

	[TestMethod]
	public void ANonBreakingSpaceIsTextRatherThanWhitespace()
	{
		const char space = (char)0xA0;
		string source = Member($"<remarks>Use <b>a{space}b</b> now{space}</remarks>");

		string text = FormatFromConfiguration(
			source,
			"dotnet_diagnostic.ZA0210.severity = warning\n"
		).Text;

		Assert.Contains($"<b>a{space}b</b>", text);
		Assert.DoesNotContain("now.", text);
	}

	private static MarkupFormatResult FormatFromConfiguration(string source, string severity)
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", source);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig($"[*.xml]\nstanza_markup_doc_reprint = members\n{severity}")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		MarkupProfile profile = configuration.FindProfile(path, null) ?? MarkupProfiles.Xml;

		return MarkupFormatter.Format(
			source,
			configuration.GetOptions(path, profile),
			configuration.ResolveReprintLayout(path)
		);
	}
}
