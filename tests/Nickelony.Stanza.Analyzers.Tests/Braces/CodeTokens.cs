using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests.Braces;

/// <summary>Reads the names of a source outside its comments.</summary>
internal static class CodeTokens
{
	/// <summary>Asserts that the fixed text parses and keeps each name in order.</summary>
	public static void AssertNamesKept(string before, string after, string message)
	{
		SyntaxNode root = CSharpSyntaxTree.ParseText(after).GetRoot();

		Assert.IsEmpty(
			root.GetDiagnostics().ToList(),
			$"{message}: the fixed text does not parse.\n{after}"
		);

		Assert.AreSequenceEqual(
			ReadNames(before),
			ReadNames(after),
			$"{message}: a statement left the code.\n{after}"
		);
	}

	private static List<string> ReadNames(string source)
	{
		return
		[
			.. CSharpSyntaxTree
				.ParseText(source)
				.GetRoot()
				.DescendantTokens()
				.Where(static token => token.IsKind(SyntaxKind.IdentifierToken))
				.Select(static token => token.ValueText),
		];
	}
}
