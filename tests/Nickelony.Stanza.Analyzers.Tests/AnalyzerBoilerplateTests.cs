using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.CodeFixes;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class AnalyzerBoilerplateTests
{
	// The bases seal Initialize and the provider members, so one check of each base covers the analyzers built on it.
	// That each analyzer derives from a base is the A4 check of RuleRegistryTests.
	[TestMethod]
	public void EveryAnalyzerAndProviderRunsThroughAStanzaBase()
	{
		Type[] bases =
		[
			typeof(StanzaNodeAnalyzer),
			typeof(StanzaTreeAnalyzer),
			typeof(StanzaCompilationTreeAnalyzer),
		];

		List<string> offenders = [];

		foreach (Type baseType in bases)
		{
			DiagnosticAnalyzer sample = RoslynTestHarness.AllAnalyzers.First(
				baseType.IsInstanceOfType
			);

			InitializeSpy spy = new();

			sample.Initialize(spy);

			if (
				!spy.ConfiguredGeneratedCode
				|| spy.GeneratedCodeFlags != GeneratedCodeAnalysisFlags.None
			)
			{
				offenders.Add($"{baseType.Name} does not turn generated code off.");
			}

			if (!spy.EnabledConcurrency)
				offenders.Add($"{baseType.Name} does not enable concurrent execution.");
		}

		foreach (CodeFixProvider provider in RoslynTestHarness.AllCodeFixProviders)
		{
			if (provider is not StanzaCodeFixProvider)
			{
				offenders.Add(
					$"{provider.GetType().Name} does not derive from the shared provider base."
				);
			}
		}

		GuardAssert.None(offenders, "The analyzers and providers do not run through a Stanza base");
	}

	private sealed class InitializeSpy : AnalysisContext
	{
		public bool ConfiguredGeneratedCode { get; private set; }
		public bool EnabledConcurrency { get; private set; }

		public GeneratedCodeAnalysisFlags GeneratedCodeFlags { get; private set; }

		public override void ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags analysisMode)
		{
			ConfiguredGeneratedCode = true;
			GeneratedCodeFlags = analysisMode;
		}

		public override void EnableConcurrentExecution() => EnabledConcurrency = true;

		public override void RegisterAdditionalFileAction(
			Action<AdditionalFileAnalysisContext> action
		) => _ = action;

		public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action) =>
			_ = action;

		public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(
			Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action
		) => _ = action;

		public override void RegisterCompilationAction(Action<CompilationAnalysisContext> action) =>
			_ = action;

		public override void RegisterCompilationStartAction(
			Action<CompilationStartAnalysisContext> action
		) => _ = action;

		public override void RegisterOperationAction(
			Action<OperationAnalysisContext> action,
			ImmutableArray<OperationKind> operationKinds
		)
		{
			_ = action;
			_ = operationKinds;
		}

		public override void RegisterOperationBlockAction(
			Action<OperationBlockAnalysisContext> action
		) => _ = action;

		public override void RegisterOperationBlockStartAction(
			Action<OperationBlockStartAnalysisContext> action
		) => _ = action;

		public override void RegisterSemanticModelAction(
			Action<SemanticModelAnalysisContext> action
		) => _ = action;

		public override void RegisterSymbolAction(
			Action<SymbolAnalysisContext> action,
			ImmutableArray<SymbolKind> symbolKinds
		)
		{
			_ = action;
			_ = symbolKinds;
		}

		public override void RegisterSymbolStartAction(
			Action<SymbolStartAnalysisContext> action,
			SymbolKind symbolKind
		)
		{
			_ = action;
			_ = symbolKind;
		}

		public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(
			Action<SyntaxNodeAnalysisContext> action,
			ImmutableArray<TLanguageKindEnum> syntaxKinds
		)
		{
			_ = action;
			_ = syntaxKinds;
		}

		public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action) =>
			_ = action;
	}
}
