using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

internal static partial class RoslynTestHarness
{
	public static Task<ImmutableArray<CodeAction>> GetActionsAsync(
		string source,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null
	)
	{
		return GetActionsCoreAsync(
			source,
			analyzerConfigOptions,
			diagnosticOptions,
			cancellationToken: HarnessBudget.Token
		);
	}

	public static Task<ImmutableArray<CodeAction>> GetActionsWithCancellationAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions,
		CancellationToken cancellationToken
	)
	{
		return GetActionsCoreAsync(
			source,
			analyzerConfigOptions: null,
			diagnosticOptions,
			cancellationToken
		);
	}

	private static async Task<ImmutableArray<CodeAction>> GetActionsCoreAsync(
		string source,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions,
		CancellationToken cancellationToken
	)
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(
			workspace,
			source,
			diagnosticOptions,
			analyzerConfigOptions: analyzerConfigOptions
		);

		ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
				document,
				analyzers: AllAnalyzers,
				cancellationToken: HarnessBudget.Token
			)
			.ConfigureAwait(false);

		List<CodeAction> actions = [];

		foreach (
			Diagnostic diagnostic in Recorded(AllAnalyzers, diagnostics, diagnosticOptions)
				.Where(HasLocation)
		)
		{
			var context = new CodeFixContext(
				document,
				diagnostic,
				registerCodeFix: (action, _) => actions.Add(action),
				cancellationToken
			);

			await CreateProvider(diagnostic.Id)
				.RegisterCodeFixesAsync(context)
				.ConfigureAwait(false);
		}

		return [.. actions];
	}

	/// <summary>Applies each fix of the rule alone, as a lightbulb does.</summary>
	public static async Task<string[]> ApplyEachFixAloneAsync(
		string source,
		DiagnosticDescriptor rule,
		IReadOnlyDictionary<string, ReportDiagnostic> diagnosticOptions,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null
	)
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(
			workspace,
			source,
			diagnosticOptions,
			analyzerConfigOptions: analyzerConfigOptions
		);

		ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
				document,
				analyzers: AllAnalyzers,
				cancellationToken: HarnessBudget.Token
			)
			.ConfigureAwait(false);

		List<string> texts = [];

		foreach (
			Diagnostic diagnostic in Recorded(AllAnalyzers, diagnostics, diagnosticOptions)
				.Where(diagnostic => diagnostic.Id == rule.Id)
		)
		{
			List<CodeAction> actions = [];

			var context = new CodeFixContext(
				document,
				diagnostic,
				registerCodeFix: (action, _) => actions.Add(action),
				cancellationToken: HarnessBudget.Token
			);

			await CreateProvider(diagnostic.Id)
				.RegisterCodeFixesAsync(context)
				.ConfigureAwait(false);

			CodeAction fix = actions.Single();

			Document fixedDocument = (
				await ApplyChangesAsync(document, fix, cancellationToken: HarnessBudget.Token)
					.ConfigureAwait(false)
			)!;

			Compilation? compilation = await fixedDocument
				.Project.GetCompilationAsync(HarnessBudget.Token)
				.ConfigureAwait(false);

			// The fix is applied to the text as reported, so its result has to compile on its own.
			AssertSnippetCompiles(compilation!, HarnessBudget.Token);

			texts.Add(
				(
					await fixedDocument.GetTextAsync(HarnessBudget.Token).ConfigureAwait(false)
				).ToString()
			);
		}

		return [.. texts];
	}

	public static Task<AnalyzerRunResult> RunFixAllAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null,
		bool assertFixedPoint = true,
		DiagnosticDescriptor? foreign = null
	)
	{
		RunOptions options = new(AllAnalyzers)
		{
			DiagnosticOptions = diagnosticOptions,
			AnalyzerConfigOptions = analyzerConfigOptions,
			AssertFixedPoint = assertFixedPoint,
		};

		return RunFixAllAsync(source, options, foreign);
	}

	/// <summary>Applies one Fix All to the first reported id.</summary>
	internal static async Task<AnalyzerRunResult> RunFixAllAsync(
		string source,
		RunOptions options,
		DiagnosticDescriptor? foreign = null
	)
	{
		AnalyzerRunResult result = await RunOnBothEndingsAsync(
				source,
				run: text => FixAllOnceAsync(source: text, options, foreign)
			)
			.ConfigureAwait(false);

		return Recorded(options, source, result);
	}

	/// <summary>Asserts that a Fix All per ID settles on the text in either ID order.</summary>
	internal static async Task AssertFixAllPerIdSettlesAsync(
		string expected,
		string source,
		RunOptions? options = null,
		string? message = null
	)
	{
		string prefix = message is null ? string.Empty : $"{message} ";

		options ??= new RunOptions(AllAnalyzers);

		// A batch formatter applies one Fix All per ID, and it has to settle where one fix at a time does.
		foreach (bool highestFirst in new[] { false, true })
		{
			string fixedPerId = await RunFixAllPerIdAsync(source, options, highestFirst)
				.ConfigureAwait(false);

			Assert.AreEqual(
				expected,
				fixedPerId,
				$"{prefix}One fix at a time and a Fix All per id, highest first {highestFirst}, settle on different text."
			);
		}
	}

	// Applies one Fix All per diagnostic ID until nothing reports, as a batch formatter does.
	// Each round fixes the lowest ID that reports, or the highest when highestFirst is set.
	private static async Task<string> RunFixAllPerIdAsync(
		string source,
		RunOptions options,
		bool highestFirst
	)
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(workspace, source, options);

		for (int round = 0; round < MaxFixRounds; round++)
		{
			ImmutableArray<Diagnostic> all = await GetDiagnosticsAsync(
					document,
					analyzers: options.Analyzers,
					cancellationToken: HarnessBudget.Token
				)
				.ConfigureAwait(false);

			if (round == 0)
				_ = Recorded(options.Analyzers, all, options.DiagnosticOptions);

			ImmutableArray<Diagnostic> located = GetLocated(all);

			if (located.Length == 0)
			{
				SourceText text = await document
					.GetTextAsync(HarnessBudget.Token)
					.ConfigureAwait(false);

				return text.ToString();
			}

			string[] ids =
			[
				.. located.Select(diagnostic => diagnostic.Id).Order(StringComparer.Ordinal),
			];

			string id = highestFirst ? ids[^1] : ids[0];
			ImmutableArray<Diagnostic> ofId = [.. located.Where(diagnostic => diagnostic.Id == id)];
			CodeFixProvider provider = CreateProvider(id, options.Providers);

			string? equivalenceKey = await GetEquivalenceKeyAsync(document, ofId, provider)
				.ConfigureAwait(false);

			var fixAllContext = new FixAllContext(
				document,
				codeFixProvider: provider,
				scope: FixAllScope.Document,
				codeActionEquivalenceKey: equivalenceKey,
				diagnosticIds: [id],
				fixAllDiagnosticProvider: new FixAllDiagnosticProvider(ofId),
				cancellationToken: HarnessBudget.Token
			);

			FixAllProvider? fixAllProvider = provider.GetFixAllProvider();
			Assert.IsNotNull(fixAllProvider, $"{id} has no Fix All provider.");

			CodeAction? action = await fixAllProvider
				.GetFixAsync(fixAllContext)
				.ConfigureAwait(false);

			Assert.IsNotNull(action, $"{id} offers no Fix All.");

			Document fixedDocument =
				await ApplyChangesAsync(document, action, HarnessBudget.Token).ConfigureAwait(false)
				?? document;

			await AssertTokensKeptAsync(document, fixedDocument, id).ConfigureAwait(false);
			document = fixedDocument;
		}

		Assert.Fail($"A Fix All per id did not settle in {MaxFixRounds} rounds.");
		return source;
	}

	private static async Task<AnalyzerRunResult> FixAllOnceAsync(
		string source,
		RunOptions options,
		DiagnosticDescriptor? foreign
	)
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(workspace, source, options);

		ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
				document,
				analyzers: options.Analyzers,
				cancellationToken: HarnessBudget.Token
			)
			.ConfigureAwait(false);

		SourceText text = await document.GetTextAsync(HarnessBudget.Token).ConfigureAwait(false);
		ImmutableArray<Diagnostic> located = [.. diagnostics.Where(HasLocation)];

		if (located.Length == 0)
			return CreateUnfixedResult(diagnostics, text);

		CodeFixProvider provider = CreateProvider(located[0].Id, options.Providers);

		string? equivalenceKey = await GetEquivalenceKeyAsync(document, located, provider)
			.ConfigureAwait(false);

		var fixAllContext = new FixAllContext(
			document,
			codeFixProvider: provider,
			scope: FixAllScope.Document,
			codeActionEquivalenceKey: equivalenceKey,

			diagnosticIds: foreign is null
				? provider.FixableDiagnosticIds
				: provider.FixableDiagnosticIds.Add(foreign.Id),
			// A foreign diagnostic stands where the first one does, as one another provider owns would.

			fixAllDiagnosticProvider: new FixAllDiagnosticProvider(
				foreign is null
					? located
					: located.Add(Diagnostic.Create(foreign, located[0].Location))
			),

			cancellationToken: HarnessBudget.Token
		);

		FixAllProvider? fixAllProvider = provider.GetFixAllProvider();
		Assert.IsNotNull(fixAllProvider, $"{located[0].Id} has no Fix All provider.");

		CodeAction? action = await fixAllProvider.GetFixAsync(fixAllContext).ConfigureAwait(false);

		// A Fix All that offers nothing would leave the text as it was and pass for a fix that settled.
		Assert.IsNotNull(action, $"{located[0].Id} offers no Fix All.");

		Document? changedDocument = await ApplyChangesAsync(document, action, HarnessBudget.Token)
			.ConfigureAwait(false);

		Assert.IsNotNull(changedDocument, $"The {located[0].Id} Fix All changes no document.");

		await AssertTokensKeptAsync(document, changedDocument, located[0].Id).ConfigureAwait(false);

		SourceText fixedText = await changedDocument
			.GetTextAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		return await CompleteAsync(
				fixedDocument: changedDocument,
				diagnostics,
				analyzers: options.Analyzers,
				fixedText: fixedText.ToString(),
				assertFixedPoint: options.AssertFixedPoint
			)
			.ConfigureAwait(false);
	}

	public static async Task<bool> OffersFixWithoutPropertiesAsync(
		string source,
		DiagnosticAnalyzer analyzer,
		IReadOnlyDictionary<string, ReportDiagnostic> diagnosticOptions,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null
	)
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(
			workspace,
			source,
			diagnosticOptions,
			analyzerConfigOptions: analyzerConfigOptions
		);

		ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
				document,
				analyzers: [analyzer],
				cancellationToken: HarnessBudget.Token
			)
			.ConfigureAwait(false);

		DiagnosticDescriptor descriptor = analyzer.SupportedDiagnostics.Single();

		Diagnostic? located = Recorded([analyzer], diagnostics, diagnosticOptions)
			.Where(HasLocation)
			.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
			.FirstOrDefault();

		Assert.IsNotNull(
			located,
			$"{analyzer.GetType().Name} reported nothing, so there is no bare diagnostic to ask about."
		);

		bool offered = false;

		var context = new CodeFixContext(
			document,
			diagnostic: Diagnostic.Create(descriptor, located.Location),
			registerCodeFix: (_, _) => offered = true,
			cancellationToken: HarnessBudget.Token
		);

		await CreateProvider(descriptor.Id).RegisterCodeFixesAsync(context).ConfigureAwait(false);

		return offered;
	}

	/// <summary>Counts the fixes offered for a diagnostic built by hand.</summary>
	public static async Task<int> CountOfferedFixesAsync(
		string source,
		DiagnosticDescriptor descriptor,
		TextSpan span,
		ImmutableDictionary<string, string?>? properties = null
	)
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(workspace, source);

		SyntaxTree tree = (
			await document.GetSyntaxTreeAsync(HarnessBudget.Token).ConfigureAwait(false)
		)!;

		var diagnostic = Diagnostic.Create(descriptor, Location.Create(tree, span), properties);

		var actions = new List<CodeAction>();

		var context = new CodeFixContext(
			document,
			diagnostic,
			registerCodeFix: (action, _) => actions.Add(action),
			cancellationToken: HarnessBudget.Token
		);

		await CreateProvider(descriptor.Id).RegisterCodeFixesAsync(context).ConfigureAwait(false);

		_ = Recorded(AllAnalyzers, [diagnostic]);

		return actions.Count;
	}

	public static async Task<AnalyzerRunResult> RunFixWithoutAdditionalLocationsAsync<TAnalyzer>(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null
	)
		where TAnalyzer : DiagnosticAnalyzer, new()
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(workspace, source, diagnosticOptions);

		var analyzer = new TAnalyzer();
		AssertAnalyzerRuns(analyzer, diagnosticOptions);

		ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
				document,
				analyzers: [analyzer],
				cancellationToken: HarnessBudget.Token
			)
			.ConfigureAwait(false);

		Diagnostic? located = diagnostics
			.Where(HasLocation)
			.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
			.FirstOrDefault();

		if (located is null)
		{
			return Recorded(
				new RunOptions([analyzer]) { DiagnosticOptions = diagnosticOptions },
				source,
				CreateUnfixedResult(diagnostics, SourceText.From(source))
			);
		}

		Document fixedDocument = await ApplyFirstFixAsync(
				document,
				Diagnostic.Create(analyzer.SupportedDiagnostics.Single(), located.Location),
				HarnessBudget.Token
			)
			.ConfigureAwait(false);

		SourceText fixedText = await fixedDocument
			.GetTextAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		AnalyzerRunResult result = await CompleteAsync(
				fixedDocument,
				diagnostics,
				analyzers: [analyzer],
				fixedText: fixedText.ToString()
			)
			.ConfigureAwait(false);

		if (!s_skipsDefaultRules.Value)
		{
			await AssertNoNewDefaultDiagnosticsAsync(original: document, fixedDocument)
				.ConfigureAwait(false);
		}

		return Recorded(
			new RunOptions([analyzer]) { DiagnosticOptions = diagnosticOptions },
			source,
			result
		);
	}

	private static async Task<string?> GetEquivalenceKeyAsync(
		Document document,
		IEnumerable<Diagnostic> diagnostics,
		CodeFixProvider provider
	)
	{
		foreach (Diagnostic diagnostic in diagnostics)
		{
			var actions = new List<CodeAction>();

			var context = new CodeFixContext(
				document,
				diagnostic,
				registerCodeFix: (action, _) => actions.Add(action),
				cancellationToken: HarnessBudget.Token
			);

			await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

			if (actions.Count > 0)
				return actions[0].EquivalenceKey;
		}

		return null;
	}

	private static async Task<Document> ApplyFirstFixAsync(
		Document document,
		Diagnostic diagnostic,
		CancellationToken cancellationToken,
		ImmutableArray<CodeFixProvider>? providers = null
	)
	{
		SyntaxNode? root = await document
			.GetSyntaxRootAsync(cancellationToken)
			.ConfigureAwait(false);

		// A fix that removed a line can leave a diagnostic pointing past the document end.
		if (root is null || diagnostic.Location.SourceSpan.End > root.FullSpan.End)
			return document;

		var actions = new List<CodeAction>();

		var context = new CodeFixContext(
			document,
			diagnostic,
			registerCodeFix: (action, _) => actions.Add(action),
			cancellationToken
		);

		CodeFixProvider provider = CreateProvider(diagnostic.Id, providers);
		await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

		Assert.IsNotEmpty(actions, $"{diagnostic.Id} offers no code fix.");

		Document fixedDocument =
			await ApplyChangesAsync(document, actions[0], cancellationToken).ConfigureAwait(false)
			?? document;

		await AssertTokensKeptAsync(document, fixedDocument, diagnostic.Id).ConfigureAwait(false);

		return fixedDocument;
	}

	// A fix of a rule outside the token rules edits only whitespace and comments, so every token has to stay as it was.
	// SyntaxFactory.AreEquivalent sees a difference when a CRLF file gains a blank line, so the tokens are compared instead.
	private static async Task AssertTokensKeptAsync(Document before, Document after, string ruleId)
	{
		if (TestRuleSets.TokenRules.Contains(ruleId))
			return;

		string[] beforeTokens = await ReadTokensAsync(before).ConfigureAwait(false);
		string[] afterTokens = await ReadTokensAsync(after).ConfigureAwait(false);

		Assert.AreSequenceEqual(
			beforeTokens,
			afterTokens,
			$"The {ruleId} fix changed a token, though the rule edits only whitespace and comments."
		);
	}

	private static async Task<string[]> ReadTokensAsync(Document document)
	{
		SyntaxNode? root = await document
			.GetSyntaxRootAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		return [.. root!.DescendantTokens().Select(token => $"{token.Kind()} {token.Text}")];
	}

	private static CodeFixProvider CreateProvider(
		string diagnosticId,
		ImmutableArray<CodeFixProvider>? providers = null
	)
	{
		return (providers ?? AllCodeFixProviders).SingleOrDefault(provider =>
				provider.FixableDiagnosticIds.Contains(diagnosticId)
			) ?? throw new InvalidOperationException($"{diagnosticId} is claimed by no provider.");
	}

	private static async Task<Document?> ApplyChangesAsync(
		Document document,
		CodeAction action,
		CancellationToken cancellationToken
	)
	{
		ImmutableArray<CodeActionOperation> operations = await action
			.GetOperationsAsync(cancellationToken)
			.ConfigureAwait(false);

		ApplyChangesOperation[] applyChanges = [.. operations.OfType<ApplyChangesOperation>()];

		// A fix that carries no change would leave the text as it was and pass for a fix that settled.
		Assert.HasCount(1, applyChanges, "The fix carries no single ApplyChangesOperation.");

		return applyChanges[0].ChangedSolution.GetDocument(document.Id);
	}

	private sealed class FixAllDiagnosticProvider(ImmutableArray<Diagnostic> diagnostics)
		: FixAllContext.DiagnosticProvider
	{
		public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
			Document document,
			CancellationToken cancellationToken
		)
		{
			SyntaxTree? tree = await document
				.GetSyntaxTreeAsync(cancellationToken)
				.ConfigureAwait(false);

			return diagnostics.Where(diagnostic => diagnostic.Location.SourceTree == tree);
		}

		public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
			Project project,
			CancellationToken cancellationToken
		) => Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);

		public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
			Project project,
			CancellationToken cancellationToken
		) => Task.FromResult<IEnumerable<Diagnostic>>([]);
	}
}
