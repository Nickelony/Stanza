using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Formatting;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupFormatterCommentTests
{
	[TestMethod]
	public void AProcessingInstructionMovesWithTheElementBelowIt()
	{
		AssertFormatsTo(
			"""
			<root>
			  <a>1</a>

			  <?pi target?>
			  <b>
			    2
			  </b>
			</root>
			""",

			"""
			<root>
			  <a>1</a>
			  <?pi target?>
			  <b>
			    2
			  </b>
			</root>
			"""
		);
	}

	[TestMethod]
	public void AMultiLineTrailingCommentStillSeparatesTheElements()
	{
		AssertFormatsTo(
			"""
			<root>
			  <a>
			    one
			  </a> <!-- tail
			  continues -->

			  <b>
			    two
			  </b>
			</root>
			""",

			"""
			<root>
			  <a>
			    one
			  </a> <!-- tail
			  continues -->
			  <b>
			    two
			  </b>
			</root>
			"""
		);
	}

	[TestMethod]
	public void ACommentDirectlyAboveAnElementMovesWithIt()
	{
		AssertFormatsTo(
			"""
			<root>
			  <a>
			    one
			  </a>

			  <!-- note -->
			  <b>
			    two
			  </b>
			</root>
			""",

			"""
			<root>
			  <a>
			    one
			  </a>
			  <!-- note -->
			  <b>
			    two
			  </b>
			</root>
			"""
		);
	}

	[TestMethod]
	public void ACommentThatIsAlreadySeparatedStaysWhereItIs()
	{
		AssertChangesNothing(
			"""
			<root>
			  <a>
			    one
			  </a>

			  <!-- section -->
			  <b>
			    two
			  </b>
			</root>
			"""
		);
	}

	[TestMethod]
	public void AMarkerThatTouchesItsTextIsSeparated()
	{
		AssertFormatsTo(
			"""
			<root>
			  <!-- a note -->
			</root>
			""",

			"""
			<root>
			  <!--a note-->
			</root>
			"""
		);
	}

	[TestMethod]
	public void AMarkerWithWhitespaceAfterItIsLeftAlone()
	{
		AssertChangesNothing(
			"""
			<root>
			  <!--  a padded note  -->
			  <!-- -->
			</root>
			"""
		);
	}

	[TestMethod]
	public void AClosingMarkerOnALineOfItsOwnIsLeftAlone()
	{
		AssertChangesNothing(
			"""
			<root>
			  <!--
			    a note
			  -->
			</root>
			"""
		);
	}

	[TestMethod]
	public void AMultiLineCommentIsJudgedAtItsTwoEnds()
	{
		AssertFormatsTo(
			"""
			<root>
			  <!--
			    a note -->
			</root>
			""",

			"""
			<root>
			  <!--
			    a note-->
			</root>
			"""
		);
	}

	[TestMethod]
	public void ACommentOutsideTheRootElementIsLaidOutAsWell()
	{
		AssertFormatsTo(
			"""
			<!-- a note -->
			<root/>
			""",

			"""
			<!--a note-->
			<root/>
			"""
		);
	}

	[TestMethod]
	public void ACommentWhoseTextStartsWithAnAngleBracketIsLaidOut() =>
		AssertFormatsTo("<root>\n  <!-- > x -->\n</root>\n", "<root>\n  <!--> x -->\n</root>\n");

	[TestMethod]
	public void AMarkerFindingCarriesItsOwnIdAndColumn()
	{
		MarkupFormatResult result = Format("<root>\n  <!--a note-->\n</root>\n");

		Assert.IsNull(result.Failure);
		Assert.HasCount(2, result.Edits);

		MarkupEdit first = result.Edits[0];

		Assert.AreEqual(MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId, first.Id);
		Assert.AreEqual(MarkupDiagnostics.AddSpaceMessage, first.Message);
		Assert.AreEqual(2, first.Line);
		Assert.AreEqual(3, first.Column);
	}

	[TestMethod]
	public void ADeclarationAndProcessingInstructionAreIgnored()
	{
		AssertFormatsTo(
			"""
			<?xml version="1.0" encoding="utf-8"?>
			<?xml-stylesheet href="style.xsl"?>
			<root>
			  <a>
			    one
			  </a>

			  <b>
			    two
			  </b>
			</root>
			""",

			"""
			<?xml version="1.0" encoding="utf-8"?>
			<?xml-stylesheet href="style.xsl"?>
			<root>
			  <a>
			    one
			  </a>
			  <b>
			    two
			  </b>
			</root>
			"""
		);
	}

	[TestMethod]
	public void ANonBreakingSpaceAfterAMarkerIsLeftAlone() =>
		AssertChangesNothing("<root>\n  <!--\u00a0text -->\n  <a>1</a>\n</root>\n");
}
