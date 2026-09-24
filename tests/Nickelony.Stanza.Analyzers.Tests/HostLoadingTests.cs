using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Finds the analyzers and fix providers the way a host does.</summary>
[TestClass]
public sealed class HostLoadingTests
{
	private static readonly Assembly s_analyzerAssembly =
		typeof(Documentation.SeparateSlashOfSelfClosingTagAnalyzer).Assembly;

	private static readonly Assembly s_fixAssembly =
		typeof(CodeFixes.BraceCodeFixProvider).Assembly;

	[TestMethod]
	public void AHostFindsEveryAnalyzerThroughTheAssemblyReference()
	{
		List<string> failures = [];
		AnalyzerFileReference reference = new(s_analyzerAssembly.Location, new PathLoader());

		reference.AnalyzerLoadFailed += (_, e) => failures.Add(e.Message);

		ImmutableArray<DiagnosticAnalyzer> found = reference.GetAnalyzers(LanguageNames.CSharp);

		Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));

		Assert.AreSequenceEqual(
			RoslynTestHarness
				.AllAnalyzers.Select(analyzer => analyzer.GetType().FullName)
				.Order(StringComparer.Ordinal),

			found.Select(analyzer => analyzer.GetType().FullName).Order(StringComparer.Ordinal),
			"The host finds a different set of analyzers than the harness runs."
		);
	}

	[TestMethod]
	public void AHostFindsEveryFixProviderByItsExportAttribute()
	{
		Type[] exported =
		[
			.. s_fixAssembly
				.GetTypes()
				.Where(type =>
					type.GetCustomAttribute<ExportCodeFixProviderAttribute>() is not null
				)
				.OrderBy(type => type.FullName, StringComparer.Ordinal),
		];

		Assert.AreSequenceEqual(
			RoslynTestHarness
				.AllCodeFixProviders.Select(provider => provider.GetType())
				.OrderBy(type => type.FullName, StringComparer.Ordinal),

			exported,
			"The fix providers a host finds are not the ones the harness runs."
		);

		foreach (Type type in exported)
		{
			object? instance = Activator.CreateInstance(type, nonPublic: true);
			Assert.IsInstanceOfType<CodeFixProvider>(instance, type.Name);
		}
	}

	[TestMethod]
	public void TheAnalyzerAssemblyReferencesOnlyWhatEveryCompilerHostProvides()
	{
		string[] failures = [.. FindUnexpectedReferences(s_analyzerAssembly, [])];
		Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
	}

	[TestMethod]
	public void TheFixAssemblyAddsOnlyTheWorkspacesAndTheAnalyzerAssembly()
	{
		string[] failures =
		[
			.. FindUnexpectedReferences(
				s_fixAssembly,

				[
					"Microsoft.CodeAnalysis.Workspaces",
					"System.Composition.AttributedModel",
					s_analyzerAssembly.GetName().Name!,
				]
			),
		];

		Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
	}

	private static IEnumerable<string> FindUnexpectedReferences(Assembly assembly, string[] extra)
	{
		// Older hosts ship older copies of the two framework assemblies, so a newer reference fails to bind.
		Dictionary<string, Version> ceilings = new(StringComparer.Ordinal)
		{
			["netstandard"] = Version.Parse("2.0.0.0"),
			["Microsoft.CodeAnalysis"] = Version.Parse("65535.0.0.0"),
			["Microsoft.CodeAnalysis.CSharp"] = Version.Parse("65535.0.0.0"),
			["System.Collections.Immutable"] = Version.Parse("8.0.0.0"),
			["System.Memory"] = Version.Parse("4.0.1.2"),
		};

		foreach (string name in extra)
			ceilings[name] = Version.Parse("65535.0.0.0");

		foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
		{
			if (!ceilings.TryGetValue(reference.Name!, out Version? ceiling))
				yield return $"{assembly.GetName().Name} references {reference.Name}, which a host may not provide.";
			else if (reference.Version > ceiling)
				yield return $"{assembly.GetName().Name} references {reference.Name} {reference.Version}, above {ceiling}.";
		}
	}

	private sealed class PathLoader : IAnalyzerAssemblyLoader
	{
		public void AddDependencyLocation(string fullPath) { }
		public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
	}
}
