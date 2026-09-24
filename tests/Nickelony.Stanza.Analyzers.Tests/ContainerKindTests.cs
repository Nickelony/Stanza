using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Braces;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Runs the rules over the container kinds besides a class.</summary>
[TestClass]
public sealed class ContainerKindTests
{
	public static IEnumerable<object[]> Kinds { get; } =
	[
		["struct"],
		["record"],
		["record struct"],
		["interface"],
	];

	[TestMethod]
	[DynamicData(
		nameof(Kinds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	[CrossCutting]
	public async Task MultiLineConstructsGetSeparated(string kind)
	{
		string source = $$"""
			{{kind}} C
			{
				void M()
				{
					DoThing();
				}
				void DoThing()
				{
				}
			}
			""";

		string fixedSource = $$"""
			{{kind}} C
			{
				void M()
				{
					DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DynamicData(
		nameof(Kinds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	[CrossCutting]
	public async Task MemberGroupsGetSeparated(string kind)
	{
		string source = $$"""
			{{kind}} C
			{
				int Count => 1;
				int Total => 2;
				void Reset() { }
			}
			""";

		string fixedSource = $$"""
			{{kind}} C
			{
				int Count => 1;
				int Total => 2;

				void Reset() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DynamicData(
		nameof(Kinds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	[CrossCutting]
	public async Task ARunOfDelegatesIsSeparatedFromTheMethodAfterIt(string kind)
	{
		string source = $$"""
			{{kind}} C
			{
				delegate void First();
				delegate void Second();
				void Reset() { }
			}
			""";

		string fixedSource = $$"""
			{{kind}} C
			{
				delegate void First();
				delegate void Second();

				void Reset() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("struct")]
	[DataRow("record")]
	[DataRow("record struct")]
	[CrossCutting]
	public async Task ARunOfConstructorsIsSeparatedFromTheMethodAfterIt(string kind)
	{
		string source = $$"""
			{{kind}} C
			{
				C(int first) { }
				C(string second) { }
				void Reset() { }
			}
			""";

		string fixedSource = $$"""
			{{kind}} C
			{
				C(int first) { }
				C(string second) { }

				void Reset() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DynamicData(
		nameof(Kinds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	[CrossCutting]
	public async Task BracePaddingIsRemoved(string kind)
	{
		string source = $$"""
			{{kind}} C
			{

				void M()
				{

					DoThing();

				}

				void DoThing()
				{
				}
			}
			""";

		string fixedSource = $$"""
			{{kind}} C
			{
				void M()
				{
					DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source
		);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DynamicData(
		nameof(Kinds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	[CrossCutting]
	public async Task AnEmptyContainerBodyCollapses(string kind)
	{
		string source = $$"""
			{{kind}} C
			{
			}
			""";

		string fixedSource = $$"""
			{{kind}} C
			{ }
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<CollapseEmptyBlocksAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnExtensionBlockIsAMemberContainer()
	{
		RoslynTestHarness.RequireRoslynWithCSharp14();

		const string source = """
			static class E
			{
				extension(string s)
				{
					public int Twice()
					{
						var two = 2;
						return s.Length * two;
					}
					public int Count => 1;
					public int Total => 2;
					public void Reset() { }
				}

				extension(int i)
				{
				}
			}
			""";

		const string fixedSource = """
			static class E
			{
				extension(string s)
				{
					public int Twice()
					{
						var two = 2;
						return s.Length * two;
					}

					public int Count => 1;
					public int Total => 2;

					public void Reset() { }
				}

				extension(int i)
				{ }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
