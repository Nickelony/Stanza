using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests.Braces;

/// <summary>Reads which if owns each else, so a fix is shown to keep them.</summary>
internal static class IfBindings
{
	/// <summary>Lists the condition of the if that owns each else, in source order.</summary>
	/// <remarks>
	/// A fixture gives each if a condition of its own, so the condition names the if.
	/// </remarks>
	public static List<string> ReadElses(string source)
	{
		return
		[
			.. CSharpSyntaxTree
				.ParseText(source)
				.GetRoot()
				.DescendantNodes()
				.OfType<ElseClauseSyntax>()
				.Select(static elseClause =>
					Strip(((IfStatementSyntax)elseClause.Parent!).Condition.ToString())
				),
		];
	}

	/// <summary>Asserts that each else but the removed ones still belongs to its if.</summary>
	public static void AssertKept(string before, string after, params string[] removed)
	{
		List<string> expected = ReadElses(before);

		foreach (string condition in removed)
			Assert.IsTrue(expected.Remove(condition), $"The source holds no else of {condition}.");

		Assert.AreSequenceEqual(expected, ReadElses(after), "The fix moved an else to another if.");
	}

	private static string Strip(string text) =>
		new([.. text.Where(static character => !char.IsWhiteSpace(character))]);
}
