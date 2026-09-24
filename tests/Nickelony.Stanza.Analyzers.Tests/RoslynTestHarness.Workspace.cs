using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Tests;

internal static partial class RoslynTestHarness
{
	private const string DocumentName = "Test.cs";

	private const string DocumentPath = "/Test.cs";

	private const string SupportName = "TestSupport.cs";

	private const string SupportPath = "/TestSupport.cs";

	private const string SupportSource = """
		global using System;
		global using System.Collections.Generic;

		global using static Helpers;

		// A task type an alias names from outside every snippet: the one shape a declaration cannot
		// answer on its own, so the body shape fix binds the return type to find its type arguments.
		global using OutsideWork = global::System.Threading.Tasks.Task<int>;

		internal static class Helpers
		{
			public static void Call(int first, int second)
			{
				return;
			}

			public static int Build(int value) => value;

			public static void DoThing()
			{
				return;
			}
		}

		internal sealed class Item
		{
			public string Name { get; set; } = string.Empty;

			public List<string> Tags { get; set; } = [];

			public int Count { get; set; }
		}

		internal sealed class Point
		{
			public int X { get; set; }
			public int Y { get; set; }
		}

		internal record Rec
		{
			public string Name { get; set; } = string.Empty;
			public List<string> Tags { get; set; } = [];
		}

		internal static class Assert
		{
			public static void That(int value)
			{
				return;
			}

			public static void AreEqual(int expected, int actual)
			{
				return;
			}
		}

		internal static class Verify
		{
			public static void That(int value)
			{
				return;
			}

			public static void AreEqual(int expected, int actual)
			{
				return;
			}
		}

		[AttributeUsage(AttributeTargets.Method)]
		internal sealed class InlineDataAttribute : Attribute
		{
			public InlineDataAttribute(int first, string second)
			{
				return;
			}
		}
		""";

	// A test that needs an older language sets the version for its own async flow, so parallel tests keep the latest.
	private static readonly AsyncLocal<LanguageVersion?> s_languageVersion = new();

	// The shared support code needs the latest language, so a run at an older version carries none.
	private static string GetSupportSource() =>
		s_languageVersion.Value is null ? SupportSource : string.Empty;

	// A build that writes no documentation file parses documentation comments as plain ones, and a test can parse that way too.
	private static readonly AsyncLocal<DocumentationMode?> s_documentationMode = new();

	// Parses with the newest language version the Roslyn under test knows,
	// which on the default leg is older than the LangVersion the projects build with.
	private static CSharpParseOptions GetParseOptions()
	{
		return new(
			s_languageVersion.Value ?? LanguageVersion.Latest,
			s_documentationMode.Value ?? DocumentationMode.Parse
		);
	}

	private static Project CreateProject(
		AdhocWorkspace workspace,
		IReadOnlyList<string> sources,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions
	)
	{
		var projectId = ProjectId.CreateNewId();

		CSharpCompilationOptions compilationOptions = new(
			OutputKind.DynamicallyLinkedLibrary,
			allowUnsafe: true
		);

		if (diagnosticOptions is not null)
		{
			compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
				diagnosticOptions
			);
		}

		List<DocumentInfo> documents =
		[
			CreateDocumentInfo(
				projectId,
				name: SupportName,
				filePath: SupportPath,
				source: SupportSource
			),
		];

		for (int index = 0; index < sources.Count; index++)
		{
			documents.Add(
				CreateDocumentInfo(
					projectId,
					name: $"Test{index}.cs",
					filePath: $"/Test{index}.cs",
					source: sources[index]
				)
			);
		}

		var projectInfo = ProjectInfo.Create(
			id: projectId,
			version: VersionStamp.Create(),
			name: "Nickelony.Stanza.Tests",
			assemblyName: "Nickelony.Stanza.Tests",
			language: LanguageNames.CSharp,
			parseOptions: GetParseOptions(),
			compilationOptions: compilationOptions,
			metadataReferences: s_metadataReferences,
			documents: documents
		);

		return workspace.CurrentSolution.AddProject(projectInfo).GetProject(projectId)!;
	}

	private static Document CreateDocument(
		AdhocWorkspace workspace,
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
		string? fileName = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? treeOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? globalOptions = null,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null
	)
	{
		CSharpCompilationOptions compilationOptions = new(GetOutputKind(source), allowUnsafe: true);

		if (diagnosticOptions is not null)
		{
			compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
				diagnosticOptions
			);
		}

		var projectId = ProjectId.CreateNewId();

		DocumentInfo supportDocument = CreateDocumentInfo(
			projectId,
			name: SupportName,
			filePath: SupportPath,
			source: GetSupportSource()
		);

		DocumentInfo snippetDocument = CreateDocumentInfo(
			projectId,
			name: fileName ?? DocumentName,
			filePath: fileName is null ? DocumentPath : "/" + fileName,
			source
		);

		var projectInfo = ProjectInfo.Create(
			id: projectId,
			version: VersionStamp.Create(),
			name: "Nickelony.Stanza.Tests",
			assemblyName: "Nickelony.Stanza.Tests",
			language: LanguageNames.CSharp,
			parseOptions: GetParseOptions(),
			compilationOptions: compilationOptions,
			metadataReferences: s_metadataReferences,
			documents: [supportDocument, snippetDocument]
		);

		Solution solution = workspace.CurrentSolution.AddProject(projectInfo);

		// One config holds the severities and the keys, so the analyzers read both from the file a build would.
		if (treeOptions is not null || analyzerConfigOptions is not null)
		{
			solution = AddConfig(
				solution,
				projectId,
				name: ".editorconfig",
				text: EditorConfigFor(treeOptions, analyzerConfigOptions)
			);
		}

		if (globalOptions is not null)
		{
			solution = AddConfig(
				solution,
				projectId,
				name: ".globalconfig",
				text: GlobalConfigFor(globalOptions)
			);
		}

		return solution.GetDocument(snippetDocument.Id)!;
	}

	private static Document CreateDocument(
		AdhocWorkspace workspace,
		string source,
		RunOptions options
	)
	{
		return CreateDocument(
			workspace,
			source,
			diagnosticOptions: options.DiagnosticOptions,
			fileName: null,
			treeOptions: options.TreeOptions,
			globalOptions: options.GlobalOptions,
			analyzerConfigOptions: options.AnalyzerConfigOptions
		);
	}

	private static DocumentInfo CreateDocumentInfo(
		ProjectId projectId,
		string name,
		string filePath,
		string source
	)
	{
		return DocumentInfo.Create(
			id: DocumentId.CreateNewId(projectId),
			name,

			loader: TextLoader.From(
				TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())
			),

			filePath: filePath
		);
	}

	private static OutputKind GetOutputKind(string source)
	{
		SyntaxNode root = CSharpSyntaxTree.ParseText(source, GetParseOptions()).GetRoot();

		return root.DescendantNodes().OfType<GlobalStatementSyntax>().Any()
			? OutputKind.ConsoleApplication
			: OutputKind.DynamicallyLinkedLibrary;
	}

	// The references are read once, so each project shares the metadata the first one loaded.
	private static readonly ImmutableArray<MetadataReference> s_metadataReferences =
	[
		MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
		MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
		MetadataReference.CreateFromFile(typeof(Expression).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(Queryable).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location),
	];

	private static Solution AddConfig(
		Solution solution,
		ProjectId projectId,
		string name,
		string text
	)
	{
		return solution.AddAnalyzerConfigDocuments([
			DocumentInfo.Create(
				id: DocumentId.CreateNewId(projectId),
				name,

				loader: TextLoader.From(
					TextAndVersion.Create(SourceText.From(text), VersionStamp.Create())
				),

				filePath: "/" + name
			),
		]);
	}

	// A global config sets a severity for every tree without a section, as a .globalconfig beside a project does.
	private static string GlobalConfigFor(
		IReadOnlyDictionary<string, ReportDiagnostic> severities
	) => "is_global = true\n" + SeverityLines(severities);

	private static string EditorConfigFor(
		IReadOnlyDictionary<string, ReportDiagnostic>? severities,
		IReadOnlyDictionary<string, string>? keys
	)
	{
		return "root = true\n\n[*.cs]\n"
			+ (severities is null ? string.Empty : SeverityLines(severities))
			+ string.Concat(
				(keys ?? ImmutableDictionary<string, string>.Empty).Select(pair =>
					$"{pair.Key} = {pair.Value}\n"
				)
			);
	}

	// A key that starts with `dotnet_`, such as a bulk severity key, is written as it stands; any other key is a rule id.
	private static string SeverityLines(IReadOnlyDictionary<string, ReportDiagnostic> severities)
	{
		return string.Concat(
			severities.Select(pair =>
				pair.Key.StartsWith("dotnet_", StringComparison.Ordinal)
					? $"{pair.Key} = {EditorConfigSeverity(pair.Value)}\n"
					: $"dotnet_diagnostic.{pair.Key}.severity = {EditorConfigSeverity(pair.Value)}\n"
			)
		);
	}

	private static string EditorConfigSeverity(ReportDiagnostic severity)
	{
		return severity switch
		{
			ReportDiagnostic.Error => "error",
			ReportDiagnostic.Warn => "warning",
			ReportDiagnostic.Info => "suggestion",
			ReportDiagnostic.Hidden => "silent",
			ReportDiagnostic.Suppress => "none",
			_ => "default",
		};
	}

	private sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
		: AnalyzerConfigOptions
	{
		public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
			values.TryGetValue(key, out value);
	}

	private sealed class TestAnalyzerConfigOptionsProvider(
		IReadOnlyDictionary<string, string> values
	) : AnalyzerConfigOptionsProvider
	{
		private readonly AnalyzerConfigOptions _options = new TestAnalyzerConfigOptions(values);

		public override AnalyzerConfigOptions GlobalOptions => _options;

		public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

		public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
	}
}
