using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class FixOptionsTests
{
	private static readonly DocLayoutOptions s_layout = new(
		ContentWidth: 80,
		TabColumns: 4,
		MinTailWords: 2,
		MinLineWords: 2,
		MaxOverflowColumns: 4,
		IndentSize: 0,
		IndentText: true,
		Expansion: DocExpansion.None,
		WeakTailWords: DocNameSet.Parse("a,the")
	);

	[TestMethod]
	public void EveryOptionSurvivesTheTripThroughADiagnostic()
	{
		FixOptions written = new(
			TabColumns: 8,
			IndentStepColumns: 2,
			Placement: ArrowPlacement.Leading,
			OpensOwnLine: true,
			ArgumentNames: [(0, "count"), (2, "smooth")],
			DocLayout: s_layout,
			SpacedSlash: true,
			KeptBlankLines: 0,
			Change: new TextChange(new TextSpan(3, 2), "text")
		);

		FixOptions read = Parse(written.ToProperties());

		// An immutable array compares by reference, so the names are compared on their own.
		FixOptions writtenRest = written with
		{
			ArgumentNames = default,
		};

		FixOptions readRest = read with { ArgumentNames = default };

		Assert.AreEqual(writtenRest, readRest);
		Assert.AreSequenceEqual(written.ArgumentNames, read.ArgumentNames);
	}

	[TestMethod]
	public void AMissingOrUnreadablePropertyParsesAsTheDefault()
	{
		ImmutableDictionary<string, string?> properties = ImmutableDictionary<
			string,
			string?
		>.Empty.Add("tabColumns", "wide");

		Assert.AreEqual(new FixOptions(), Parse(properties));
	}

	[TestMethod]
	[DataRow("0=width;x=height")]
	[DataRow("0=")]
	[DataRow("=width")]
	public void AnUnreadableArgumentListParsesAsNoNames(string value)
	{
		ImmutableDictionary<string, string?> properties = ImmutableDictionary<
			string,
			string?
		>.Empty.Add("argumentNames", value);

		Assert.IsTrue(Parse(properties).ArgumentNames.IsDefault);
	}

	[TestMethod]
	[DataRow(0, MarkupLimits.MaxCount)]
	[DataRow(1, MarkupLimits.MaxIndentColumns)]
	[DataRow(2, MarkupLimits.MaxCount)]
	[DataRow(3, MarkupLimits.MaxCount)]
	[DataRow(4, MarkupLimits.MaxCount)]
	[DataRow(5, MarkupLimits.MaxIndentColumns)]
	public void ALayoutCountAboveItsCapParsesAsNoLayout(int field, int cap)
	{
		ImmutableDictionary<string, string?> properties = new FixOptions(
			DocLayout: s_layout
		).ToProperties();

		string[] parts = properties["docLayout"]!.Split(';');

		parts[field] = cap.ToString(CultureInfo.InvariantCulture);
		Assert.IsNotNull(Parse(properties.SetItem("docLayout", string.Join(";", parts))).DocLayout);

		parts[field] = (cap + 1).ToString(CultureInfo.InvariantCulture);
		Assert.IsNull(Parse(properties.SetItem("docLayout", string.Join(";", parts))).DocLayout);
	}

	private static FixOptions Parse(ImmutableDictionary<string, string?> properties)
	{
		var diagnostic = Diagnostic.Create(
			DiagnosticDescriptors.RemoveRedundantBraces,
			Location.None,
			properties
		);

		return FixOptions.Parse(diagnostic);
	}
}
