using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class StanzaTriviaTests
{
	// A build that writes no documentation file parses each comment as a plain one,
	// and the text has to name the same documentation comments the compiler finds when it parses them.
	[TestMethod]
	public void ThePlainParseFindsTheDocumentationCommentsTheCompilerFinds()
	{
		const string source = """
			class C
			{
				/// a
				int a;
				//// b
				int b;
				/** c */
				int c;
				/*** d */
				int d;
				/**/
				int e;
				/* f */
				int f;
				// g
				int g;
				///
				int h;
			}
			""";

		int[] parsed = FindStarts(
			source,
			DocumentationMode.Parse,
			static trivia => trivia.HasStructure
		);

		int[] plain = FindStarts(
			source,
			DocumentationMode.None,
			StanzaTrivia.IsDocumentationComment
		);

		Assert.HasCount(3, parsed);
		Assert.AreSequenceEqual(parsed, plain);
	}

	private static int[] FindStarts(
		string source,
		DocumentationMode mode,
		Func<SyntaxTrivia, bool> isDocumentation
	)
	{
		SyntaxTree tree = CSharpSyntaxTree.ParseText(
			source,
			new CSharpParseOptions(documentationMode: mode)
		);

		return
		[
			.. tree.GetRoot()
				.DescendantTrivia()
				.Where(isDocumentation)
				.Select(static trivia => trivia.FullSpan.Start),
		];
	}
}
