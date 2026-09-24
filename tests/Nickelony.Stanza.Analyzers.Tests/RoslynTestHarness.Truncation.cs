using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

internal static partial class RoslynTestHarness
{
	/// <summary>Runs the analyzers over any sources, then builds the fix of each report.</summary>
	internal static async Task AssertSurvivesAsync(IReadOnlyList<string> sources)
	{
		using var workspace = new AdhocWorkspace();

		Project project = CreateProject(workspace, sources, TestRuleSets.AllRulesEnabled);

		Compilation? compilation = await project
			.GetCompilationAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		Assert.IsNotNull(compilation, "The project compiled to nothing, so no source was read.");

		ImmutableArray<Diagnostic> diagnostics =
		[
			.. await compilation
				.WithAnalyzers(AllAnalyzers, project.AnalyzerOptions)
				.GetAnalyzerDiagnosticsAsync(HarnessBudget.Token)
				.ConfigureAwait(false),
		];

		if (diagnostics.Any(diagnostic => diagnostic.Id == "AD0001"))
		{
			int crashing = await FindCrashingSourceAsync(sources).ConfigureAwait(false);

			Assert.Fail(
				$"An analyzer threw on this source:\n{sources[crashing]}\n{string.Join("\n", diagnostics.Where(diagnostic => diagnostic.Id == "AD0001"))}"
			);
		}

		foreach (Diagnostic diagnostic in diagnostics.Where(HasLocation))
		{
			Document? document = project.GetDocument(diagnostic.Location.SourceTree);

			if (document is null || document.Name == SupportName)
				continue;

			string? failure = await TryBuildFixAsync(document, diagnostic).ConfigureAwait(false);

			string text = (
				await document.GetTextAsync(HarnessBudget.Token).ConfigureAwait(false)
			).ToString();

			Assert.IsNull(
				failure,
				$"The {diagnostic.Id} fix threw on this source:\n{text}\n{failure}"
			);
		}

		_ = Recorded(AllAnalyzers, diagnostics, TestRuleSets.AllRulesEnabled);
	}

	// Each source runs alone until one crashes, which names the source a crash of the whole project cannot.
	private static async Task<int> FindCrashingSourceAsync(IReadOnlyList<string> sources)
	{
		for (int index = 0; index < sources.Count; index++)
		{
			using var workspace = new AdhocWorkspace();

			Project project = CreateProject(
				workspace,
				[sources[index]],
				TestRuleSets.AllRulesEnabled
			);

			Compilation compilation = (
				await project.GetCompilationAsync(HarnessBudget.Token).ConfigureAwait(false)
			)!;

			ImmutableArray<Diagnostic> diagnostics = await compilation
				.WithAnalyzers(AllAnalyzers, project.AnalyzerOptions)
				.GetAnalyzerDiagnosticsAsync(HarnessBudget.Token)
				.ConfigureAwait(false);

			if (diagnostics.Any(diagnostic => diagnostic.Id == "AD0001"))
				return index;
		}

		return 0;
	}

	// Returns what a fix threw, so the caller can name the source outside a catch block.
	private static async Task<string?> TryBuildFixAsync(Document document, Diagnostic diagnostic)
	{
		CodeFixProvider? provider = AllCodeFixProviders.SingleOrDefault(candidate =>
			candidate.FixableDiagnosticIds.Contains(diagnostic.Id)
		);

		if (provider is null)
			return null;

		try
		{
			List<CodeAction> actions = [];

			var context = new CodeFixContext(
				document,
				diagnostic,
				registerCodeFix: (action, _) => actions.Add(action),
				cancellationToken: HarnessBudget.Token
			);

			await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

			foreach (CodeAction action in actions)
				_ = await action.GetOperationsAsync(HarnessBudget.Token).ConfigureAwait(false);

			return null;
		}
		catch (System.Exception exception) when (exception is not System.OperationCanceledException)
		{
			return exception.ToString();
		}
	}
}
