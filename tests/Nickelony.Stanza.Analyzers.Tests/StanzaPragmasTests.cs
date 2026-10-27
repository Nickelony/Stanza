using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class StanzaPragmasTests
{
	// A compiler warning is named by its number, which is no identifier, and the list goes on to the rule.
	[TestMethod]
	public void ACodeWrittenAsANumberNamesItselfAndLeavesTheRuleBesideItSilenced()
	{
		const string source = """
			#pragma warning disable 1591, ZA0005
			class C
			{
			}
			""";

		SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
		int position = source.IndexOf("class", StringComparison.Ordinal);

		Assert.IsTrue(IsSilenced(tree, "1591", position));
		Assert.IsTrue(IsSilenced(tree, "ZA0005", position));
		Assert.IsFalse(IsSilenced(tree, "ZA0002", position));
	}

	// The compiler ignores a directive in a branch the build leaves out, so it silences nothing.
	[TestMethod]
	public void APragmaInABranchTheBuildLeavesOutSilencesNothing()
	{
		const string source = """
			#if NEVER
			#pragma warning disable ZA0005
			#endif
			class C
			{
			}
			""";

		SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
		int position = source.IndexOf("class", StringComparison.Ordinal);

		Assert.IsFalse(IsSilenced(tree, "ZA0005", position));
		Assert.IsFalse(StanzaPragmas.HasPragmaWarningDirectives(tree, CancellationToken.None));
	}

	private static bool IsSilenced(SyntaxTree syntaxTree, string ruleId, int position)
	{
		return StanzaPragmas.IsSilencedByPragma(
			syntaxTree,
			ruleId,
			position,
			cancellationToken: CancellationToken.None
		);
	}
}
