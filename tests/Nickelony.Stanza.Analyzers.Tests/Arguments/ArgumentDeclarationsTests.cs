using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Arguments;

namespace Nickelony.Stanza.Analyzers.Tests.Arguments;

[TestClass]
public sealed class ArgumentDeclarationsTests
{
	[TestMethod]
	public void ABlockThatClosesBeforeTheDeclarationDoesNotHoldIt()
	{
		const string source = """
			#define A
			class C
			{
			#if A
				int x;
			#endif
				void M(int a) { }
			}
			""";

		Assert.IsFalse(IsUnderAConditional(source));
	}

	[TestMethod]
	public void ABlockAroundTheDeclarationHoldsIt()
	{
		const string source = """
			#define A
			class C
			{
			#if A
				void M(int a) { }
			#endif
			}
			""";

		Assert.IsTrue(IsUnderAConditional(source));
	}

	[TestMethod]
	public void AnOuterBlockStillHoldsTheDeclarationAfterAnInnerBlockCloses()
	{
		const string source = """
			#define A
			class C
			{
			#if A
			#if A
				int x;
			#endif
				void M(int a) { }
			#endif
			}
			""";

		Assert.IsTrue(IsUnderAConditional(source));
	}

	[TestMethod]
	public void AnOuterBlockStillHoldsADeclarationWithNoParameterListAfterAnInnerBlockCloses()
	{
		const string source = """
			#define A
			class C
			{
			#if A
			#if A
				int x;
			#endif
				int P { get; }
			#endif
			}
			""";

		Assert.IsTrue(IsUnderAConditional(source, "P"));
	}

	[TestMethod]
	public void NestedBlocksThatBothCloseBeforeTheDeclarationDoNotHoldIt()
	{
		const string source = """
			#define A
			class C
			{
			#if A
			#if A
				int x;
			#endif
			#endif
				void M(int a) { }
			}
			""";

		Assert.IsFalse(IsUnderAConditional(source));
	}

	[TestMethod]
	public void ABlockThatNoEndifClosesHoldsTheDeclaration()
	{
		const string source = """
			#define A
			class C
			{
			#if A
				void M(int a) { }
			}
			""";

		Assert.IsTrue(IsUnderAConditional(source));
	}

	[TestMethod]
	public void AnEndifWithNoIfBeforeItClosesNothing()
	{
		const string source = """
			#define A
			class C
			{
			#endif
				void M(int a) { }
			}
			""";

		Assert.IsFalse(IsUnderAConditional(source));
	}

	[TestMethod]
	public void ABlockInTheBodyDoesNotHoldTheDeclaration()
	{
		const string source = """
			#define A
			class C
			{
				void M(int a)
				{
			#if A
					a++;
			#endif
				}
			}
			""";

		Assert.IsFalse(IsUnderAConditional(source));
	}

	[TestMethod]
	public void ABlockInTheParameterListCountsAfterAClosedBlockBeforeTheDeclaration()
	{
		const string source = """
			#define A
			class C
			{
			#if A
				int x;
			#endif
				void M(
					int a,
			#if A
					int b,
			#endif
					int c
				) { }
			}
			""";

		Assert.IsTrue(IsUnderAConditional(source));
	}

	[TestMethod]
	public void EachMarkerOfATreeIsReadOnceWhateverTheNumberOfCalls()
	{
		const int methodCount = 300;

		StringBuilder builder = new("#define A\nclass C\n{\n");

		for (int index = 0; index < methodCount; index++)
			builder.Append("#if A\n\tvoid M").Append(index).Append("(int a) { }\n#endif\n");

		builder.Append("}\n");

		SyntaxTree tree = CSharpSyntaxTree.ParseText(builder.ToString());
		SemanticModel model = CreateModel(tree);

		ConditionalBlockTree.StepsTaken = 0;

		foreach (
			MethodDeclarationSyntax method in tree.GetRoot()
				.DescendantNodes()
				.OfType<MethodDeclarationSyntax>()
		)
		{
			Assert.IsTrue(
				ArgumentDeclarations.IsUnderAConditional(
					model.GetDeclaredSymbol(method)!,
					CancellationToken.None
				)
			);

			Assert.IsTrue(
				ArgumentDeclarations.IsUnderAConditional(
					model.GetDeclaredSymbol(method)!,
					CancellationToken.None
				)
			);
		}

		Assert.AreEqual(
			2L * methodCount,
			ConditionalBlockTree.StepsTaken,
			"Each of the 2 markers of a block should be read once."
		);
	}

	private static bool IsUnderAConditional(string source, string memberName = "M")
	{
		SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
		SemanticModel model = CreateModel(tree);

		MemberDeclarationSyntax member = tree.GetRoot()
			.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.Single()
			.Members.Single(candidate => model.GetDeclaredSymbol(candidate)?.Name == memberName);

		return ArgumentDeclarations.IsUnderAConditional(
			model.GetDeclaredSymbol(member)!,
			CancellationToken.None
		);
	}

	private static SemanticModel CreateModel(SyntaxTree syntaxTree)
	{
		var compilation = CSharpCompilation.Create(
			"Declarations",
			[syntaxTree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]
		);

		return compilation.GetSemanticModel(syntaxTree);
	}
}
