using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
			#pragma warning disable 1591, ZA0001
			class C
			{
			}
			""";

		SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
		int position = source.IndexOf("class", StringComparison.Ordinal);

		Assert.IsTrue(IsSilenced(tree, "1591", position));
		Assert.IsTrue(IsSilenced(tree, "ZA0001", position));
		Assert.IsFalse(IsSilenced(tree, "ZA0017", position));
	}

	// The compiler ignores a directive in a branch the build leaves out, so it silences nothing.
	[TestMethod]
	public void APragmaInABranchTheBuildLeavesOutSilencesNothing()
	{
		const string source = """
			#if NEVER
			#pragma warning disable ZA0001
			#endif
			class C
			{
			}
			""";

		SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
		int position = source.IndexOf("class", StringComparison.Ordinal);

		Assert.IsFalse(IsSilenced(tree, "ZA0001", position));
		Assert.IsFalse(StanzaPragmas.HasPragmaWarningDirectives(tree, CancellationToken.None));
	}

	// A blanket directive and a specific one change the same rule, so the last of either before the position decides.
	[TestMethod]
	public void ABlanketAndASpecificDirectiveInterleaveInDocumentOrder()
	{
		const string source = """
			class C
			{
			#pragma warning disable ZA0001
				int a;
			#pragma warning restore
				int b;
			#pragma warning disable
				int c;
			#pragma warning restore ZA0001
				int d;
			#pragma warning restore
				int e;
			}
			""";

		SyntaxTree tree = CSharpSyntaxTree.ParseText(source);

		// The line of each member, and whether ZA0001 and ZA0002 are silenced there.
		(string Member, bool First, bool Second)[] expected =
		[
			("int a;", true, false),
			("int b;", false, false),
			("int c;", true, true),
			("int d;", false, true),
			("int e;", false, false),
		];

		foreach ((string member, bool first, bool second) in expected)
		{
			int position = source.IndexOf(member, StringComparison.Ordinal);

			Assert.AreEqual(first, IsSilenced(tree, "ZA0001", position), $"ZA0001 at {member}");
			Assert.AreEqual(second, IsSilenced(tree, "ZA0002", position), $"ZA0002 at {member}");
		}
	}

	[TestMethod]
	public void ADirectiveAtThePositionIsNotInForceThereButIsAfterIt()
	{
		const string source = """
			#pragma warning disable ZA0001
			#pragma warning restore ZA0001
			class C
			{
			}
			""";

		SyntaxTree tree = CSharpSyntaxTree.ParseText(source);

		int disable = source.IndexOf("#pragma warning disable", StringComparison.Ordinal);
		int restore = source.IndexOf("#pragma warning restore", StringComparison.Ordinal);

		Assert.IsFalse(IsSilenced(tree, "ZA0001", disable), "the directive at its own start");
		Assert.IsTrue(IsSilenced(tree, "ZA0001", disable + 1), "just after the disable");
		Assert.IsTrue(IsSilenced(tree, "ZA0001", restore), "the restore at its own start");
		Assert.IsFalse(IsSilenced(tree, "ZA0001", restore + 1), "just after the restore");
	}

	// The lookup reads each rule's transitions once, so every answer is checked against the plain walk of the directives.
	[TestMethod]
	public void EveryAnswerMatchesTheWalkOverAllDirectivesInAManyDirectiveFile()
	{
		string[] rules = ["ZA0001", "ZA0002", "ZA0003"];

		string[] directives =
		[
			"#pragma warning disable ZA0001",
			"#pragma warning disable",
			"#pragma warning restore ZA0002",
			"#pragma warning restore",
			"#pragma warning disable ZA0002, ZA0003",
			"#pragma warning restore ZA0001, ZA0003",
			"#pragma warning disable 1591",
		];

		StringBuilder builder = new();
		List<(int Start, string Text)> written = [];

		for (int index = 0; index < 120; index++)
		{
			string directive = directives[((index * 5) + (index / 7)) % directives.Length];

			written.Add((builder.Length, directive));
			_ = builder.Append(directive).Append('\n').Append("// member ").Append(index).Append('\n');
		}

		string source = builder.ToString();
		SyntaxTree tree = CSharpSyntaxTree.ParseText(source);

		List<int> positions = [0, source.Length];

		foreach ((int start, _) in written)
			positions.AddRange([start - 1, start, start + 1]);

		foreach (int position in positions.Where(position => position >= 0 && position <= source.Length))
		{
			foreach (string rule in rules.Append("ZA9999"))
			{
				Assert.AreEqual(
					WalkSilenced(written, rule, position),
					IsSilenced(tree, rule, position),
					$"{rule} at {position}"
				);
			}
		}
	}

	private static bool WalkSilenced(List<(int Start, string Text)> directives, string ruleId, int position)
	{
		bool silenced = false;

		foreach ((int start, string text) in directives)
		{
			if (start >= position)
				break;

			string[] codes = text
				.Split(' ', 4)
				.Skip(3)
				.SelectMany(part => part.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
				.ToArray();

			if (codes.Length == 0 || codes.Contains(ruleId))
				silenced = text.StartsWith("#pragma warning disable", StringComparison.Ordinal);
		}

		return silenced;
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
