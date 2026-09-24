using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Guards the rules of CONVENTIONS.md that a parse of the sources can check.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class ConventionGuardTests
{
	private const string AnalyzerProject = "Nickelony.Stanza.Analyzers";
	private const string FixProject = "Nickelony.Stanza.Analyzers.CodeFixes";

	// The verbs a method name may start with: a new verb joins the list, and a noun is renamed.
	private static readonly HashSet<string> s_verbs = ToSet(
		"""
		Add Analyze Append Apply Arrange Break Build Can Cannot Carries Check Claims Close Collapse
		Collect Compare Compile Conflicts Contains Copy Count Covers Create Crosses Declares
		Describe Detect Discard Display End Ends Ensure Enumerate Equals Escape Evaluate Fail
		Fails Find Finish Fits Fix Flush Fold For Format From Get Has Holds Is Join Joins Keep
		Keeps Lay Leaves Mark Matches Measure Merge Names Narrow Normalize Open Opens Order Owns
		Pair Parse Peek Plan Pop Process Push Rank Read Record Remove Reorder Repeat Replace
		Report Reports Resolve Returns Run Scan Seal Shape Shares Skip Sniff Spans Split Stands
		Start Starts Steps Stop Store Strip Supports Swap Sync Takes To Transform Translate Trim Try
		Unwrap Validate Walk Warn With Without Write Writes
		"""
	);

	// A method that returns bool and starts with one of these does work, so it is a Try.
	private static readonly HashSet<string> s_actions = ToSet(
		"""
		Add Append Apply Build Collect Create Find Format Get Insert Measure Order Parse Push
		Remove Report Set Store Write
		"""
	);

	// A Roslyn struct whose default value stands for nothing found, as Roslyn's own finders return.
	private static readonly HashSet<string> s_emptyDefaults = new(StringComparer.Ordinal)
	{
		"SyntaxTrivia",
		"SyntaxToken",
		"TextSpan",
		"MarkupSpan",
	};

	private static readonly HashSet<string> s_emptyCollections = new(StringComparer.Ordinal)
	{
		"ImmutableArray",
		"List",
		"IEnumerable",
		"IReadOnlyList",
		"HashSet",
	};

	private static readonly Dictionary<string, string> s_parameterNames = new(
		StringComparer.Ordinal
	)
	{
		["SourceText"] = "sourceText",
		["SyntaxTree"] = "syntaxTree",
		["SemanticModel"] = "semanticModel",
		["CancellationToken"] = "cancellationToken",
		["CompilationOptions"] = "compilationOptions",
	};

	// Each directory of the engine and the tools, as "" for the engine's root, with the directories it may use.
	// The engine's tools and the analyzers use the engine as a whole, so only its own layers are listed.
	private static readonly Dictionary<string, Dictionary<string, string[]>> s_layers = new()
	{
		["Nickelony.Stanza.Markup.Core"] = new()
		{
			[""] = [],
			["DocComments"] = [""],
			["Xml"] = ["", "DocComments"],
			["Options"] = ["", "DocComments", "Xml"],
			["Formatting"] = ["", "DocComments", "Xml", "Options"],
		},

		["Nickelony.Stanza.Markup"] = new()
		{
			["Paths"] = [],
			["Notices"] = [],
			["Files"] = [],
			["Configuration"] = ["Paths", "Notices"],
			["Collection"] = ["Paths", "Notices", "Configuration"],
			["Cli"] = ["Paths", "Notices", "Files", "Configuration", "Collection"],
		},

		["Nickelony.Stanza.Format"] = new()
		{
			["Planning"] = [],
			["Running"] = ["Planning"],
			["Cli"] = ["Planning", "Running"],
		},
	};

	private static readonly Lazy<SourceFile[]> s_sources = new(() => ReadSources("src"));

	private static readonly Lazy<SourceFile[]> s_testSources = new(() => ReadSources("tests"));

	private static readonly string[] s_textFilePatterns =
	[
		"*.cs",
		"*.md",
		"*.yml",
		"*.json",
		"*.csproj",
		"*.props",
		"*.targets",
		"*.slnx",
		"*.ps1",
		"*.ini",
		"*.config",
		"*.editorconfig",
		"*.gitignore",
		"*.gitattributes",
		"CODEOWNERS",
	];

	[GeneratedRegex("^[A-Z][a-z0-9]*")]
	private static partial Regex FirstWord();

	// A change, an edit, or an array, list or task of them.
	[GeneratedRegex(
		@"^(?:Task<)?(?:(?:Immutable)?(?:Array|List)<)?\w*(?:Change|Edit)>?>?(?:\[\])?$"
	)]
	private static partial Regex ChangeShape();

	// A solution's <Folder> element and Microsoft's .editorconfig key keep their word, so each is cut from a line before the search.
	private static readonly string[] s_keptUses =
	[
		"<Folder",
		"</Folder",
		"dotnet_style_namespace_match_folder",
	];

	// The word is split, so the pattern does not find itself.
	[GeneratedRegex("fo" + "lder", RegexOptions.IgnoreCase)]
	private static partial Regex OtherWordForDirectory();

	[TestMethod]
	public void NoFamilyIsReferencedByAnotherFamilyOrTheRoot()
	{
		SourceFile[] analyzers = [.. s_sources.Value.Where(static file => IsAnalyzerFile(file))];

		string[] families =
		[
			.. analyzers.Where(IsAnalyzerFamily).Select(static file => file.Directory).Distinct(),
		];

		Dictionary<string, string> owners = FindFamilyTypes();

		Assert.IsNotEmpty(owners, "No family declares a type, so the check below reads nothing.");

		string[] imports =
		[
			.. from file in analyzers
			from family in families
			where family != file.Directory && file.Text.Contains($"{AnalyzerProject}.{family};")
			select $"{file.Relative} reads {family}",
		];

		// A type name only another family declares reaches into it, however it is qualified.
		// The root is the empty directory, so each family type it names is another family's.
		string[] names =
		[
			.. from file in analyzers
			from name in file.Names
			let owner = owners.GetValueOrDefault(name.Identifier.ValueText)
			where owner is not null && owner != file.Directory
			select $"{file.Relative}:{GetLine(name)} names {name.Identifier.ValueText} of {owner}",
		];

		AssertNone(
			[.. imports, .. names],
			rule: "A1: a family directory or the analyzer root references a family"
		);
	}

	[TestMethod]
	public void EachDirectoryOfTheEngineAndTheToolsUsesOnlyTheDirectoriesBeforeIt()
	{
		List<string> offenders = [];

		foreach ((string project, Dictionary<string, string[]> layers) in s_layers)
		{
			SourceFile[] files = [.. s_sources.Value.Where(file => file.Project == project)];

			Assert.IsNotEmpty(files, $"{project} holds no source, so the check reads nothing.");

			string[] directories = [.. files.Select(static file => file.Directory).Distinct()];

			offenders.AddRange(
				directories
					.Except(layers.Keys)
					.Select(directory => $"{project}/{directory} has no row in the layer table")
			);

			offenders.AddRange(
				layers
					.Keys.Except(directories)
					.Select(directory =>
						$"The layer table names {project}/{directory}, which is gone"
					)
			);

			foreach (SourceFile file in files.Where(file => layers.ContainsKey(file.Directory)))
			{
				// The engine's root namespace holds the primitives, so every directory below it reaches it without a using.
				offenders.AddRange(
					from other in layers.Keys
					where other.Length > 0 && other != file.Directory
					where !layers[file.Directory].Contains(other)
					where Regex.IsMatch(file.Text, Regex.Escape($"{project}.{other}") + @"\b")
					select $"{file.Relative} uses {other}, which is not before {file.Directory}"
				);
			}
		}

		GuardAssert.None(
			offenders,
			"A directory of the engine or a tool uses one that comes after it",
			closing: "docs/Architecture.md states the order of each project, and s_layers holds it."
		);
	}

	[TestMethod]
	public void ARootTypeIsABaseOrServesMoreThanOneFamily()
	{
		List<string> offenders = [];

		foreach (SourceFile file in s_sources.Value.Where(static file => IsAnalyzerRoot(file)))
		{
			foreach (BaseTypeDeclarationSyntax type in GetTopLevelTypes(file))
			{
				if (IsAnalyzerBase(type))
					continue;

				string name = type.Identifier.ValueText;
				string[] directories = FindUserDirectories(name, declaredIn: file);

				// The root is the empty directory; a type another root type uses stays there.
				if (!directories.Contains("") && directories.Length < 2)
				{
					offenders.Add(
						$"{name} ({file.Relative}) serves [{string.Join(", ", directories)}]"
					);
				}
			}
		}

		AssertNone(offenders, rule: "A2: a root type that one family uses belongs in that family");
	}

	[TestMethod]
	public void TheMarkupEngineAndTheToolsReferenceNoAnalyzerAssembly()
	{
		string[] analyzers = [AnalyzerProject + ".csproj", FixProject + ".csproj"];

		(string Project, string[] Forbidden)[] cases =
		[
			(
				"Nickelony.Stanza.Markup.Core",
				[.. analyzers, "Nickelony.Stanza.Markup.csproj", "Nickelony.Stanza.Format.csproj"]
			),

			("Nickelony.Stanza.Markup", analyzers),
			("Nickelony.Stanza.Format", analyzers),
		];

		string[] offenders =
		[
			.. from item in cases
			from reference in ReadAssemblyReferences(item.Project)
			where item.Forbidden.Contains(reference)
			select $"{item.Project} references {reference}",
		];

		AssertNone(offenders, rule: "A3: the engine or a tool references an analyzer assembly");
	}

	[TestMethod]
	public void AnAnalyzerReachesItsDescriptorThroughDescriptor()
	{
		string[] offenders =
		[
			.. from file in s_sources.Value
			where
				file.Project == AnalyzerProject
				&& file.Relative.EndsWith("Analyzer.cs", StringComparison.Ordinal)
			from access in file.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
			where
				access.Expression.ToString() == "DiagnosticDescriptors"
				|| access.ToString() == "Rule.Descriptor"
			where !access.Ancestors().Any(static node => node is BaseListSyntax)
			select $"{file.Relative}:{GetLine(access)} names {access}",
		];

		AssertNone(offenders, rule: "A5: an analyzer names a descriptor outside its base call");
	}

	[TestMethod]
	public void ADiagnosticCarriesFixDataOnlyThroughFixOptions()
	{
		string[] offenders =
		[
			.. from file in s_sources.Value
			where file.Project is AnalyzerProject or FixProject
			where file.Relative != $"{AnalyzerProject}/FixOptions.cs"
			from node in file.Root.DescendantNodes()
			where IsPropertyCodec(node)
			select $"{file.Relative}:{GetLine(node)} handles {node}",
		];

		AssertNone(offenders, rule: "A8: a diagnostic carries fix data outside FixOptions");
	}

	[TestMethod]
	public void AMethodNameStartsWithAVerb()
	{
		// A question that returns bool may start with its subject, such as ArrowPaysForItsLine.
		string[] offenders =
		[
			.. from method in FindMethods()
			where !ReturnsBool(method.Syntax) && !IsGeneratedRegex(method.Syntax)
			where !s_verbs.Contains(GetVerb(method.Name))
			select $"{method.Name} starts with '{GetVerb(method.Name)}' ({method.Where})",
		];

		AssertNone(
			offenders,
			rule: "N1: a method starts with a word s_verbs lacks; rename it or add it"
		);
	}

	[TestMethod]
	public void AMethodReturnsTheShapeItsVerbPromises()
	{
		string[] offenders =
		[
			.. from method in FindMethods()
			let problem = FindShapeProblem(method.Syntax)
			where problem is not null
			select $"{method.Name} returns {method.Syntax.ReturnType}: {problem} ({method.Where})",
		];

		AssertNone(offenders, rule: "N2: a method returns a shape its verb does not promise");
	}

	[TestMethod]
	public void ACreateMethodNamesTheChangeItBuilds()
	{
		string[] offenders =
		[
			.. from method in FindMethods()
			where GetVerb(method.Name) == "Create"
			where
				method.Name.EndsWith("Changes", StringComparison.Ordinal)
				|| method.Name.EndsWith("Change", StringComparison.Ordinal)
			select $"{method.Name} ({method.Where})",
		];

		AssertNone(offenders, rule: "N3: a Create method carries a Change or Changes suffix");
	}

	[TestMethod]
	public void TheStanzaPrefixMarksAProjectRootType()
	{
		string[] offenders =
		[
			.. from file in s_sources.Value
			where !IsProjectRoot(file)
			from type in GetTopLevelTypes(file)
			where type.Identifier.ValueText.StartsWith("Stanza", StringComparison.Ordinal)
			select $"{type.Identifier.ValueText} ({file.Relative})",
		];

		AssertNone(offenders, rule: "N4: a type outside a project root carries the Stanza prefix");
	}

	[TestMethod]
	public void AFixProviderIsNamedForItsFamilyAndNoTypeEndsInBase()
	{
		BaseTypeDeclarationSyntax[] types = [.. s_sources.Value.SelectMany(GetTopLevelTypes)];

		string[] bases =
		[
			.. from type in types
			where type.Identifier.ValueText.EndsWith("Base", StringComparison.Ordinal)
			select $"{type.Identifier.ValueText} ends in Base",
		];

		string[] providers =
		[
			.. from type in types
			where HasAttribute(type.AttributeLists, name: "ExportCodeFixProvider")
			where !type.Identifier.ValueText.EndsWith("CodeFixProvider", StringComparison.Ordinal)
			select $"{type.Identifier.ValueText} is a fix provider not named <Family>CodeFixProvider",
		];

		AssertNone(
			[.. bases, .. providers],
			rule: "N5: a fix provider or a base breaks the naming rule"
		);
	}

	[TestMethod]
	public void ARoslynParameterTakesItsStandardName()
	{
		// A record's positional parameter is a property, so it takes the property's name.
		// An override keeps the name of the Roslyn member it overrides, so it cannot take another.
		// A qualified type counts by its last segment, as the short name does.
		string[] offenders =
		[
			.. from file in s_sources.Value.Concat(s_testSources.Value)
			from parameter in file.Root.DescendantNodes().OfType<ParameterSyntax>()
			where
				parameter.Type is not null
				&& parameter.Parent?.Parent is not RecordDeclarationSyntax
				&& !IsOverride(parameter.Parent?.Parent)
			let type = parameter.Type!.ToString().TrimEnd('?').Split('.')[^1]
			where s_parameterNames.TryGetValue(type, out string? expected)
			where parameter.Identifier.ValueText != s_parameterNames[type]
			select $"{type} {parameter.Identifier.ValueText} ({file.Relative}:{GetLine(parameter)})",
		];

		AssertNone(offenders, rule: "N6: a parameter of a Roslyn type has another name");
	}

	[TestMethod]
	public void APlaceThatHoldsFilesIsADirectory()
	{
		string root = RepositoryPaths.Root();

		string[] files =
		[
			.. s_textFilePatterns.SelectMany(pattern => RepositoryFiles.Under(root, pattern)),
		];

		Assert.IsNotEmpty(files, "No text file is listed, so the check below reads nothing.");

		string[] offenders =
		[
			.. from path in files
			from line in File.ReadLines(path)
				.Select(static (text, index) => (Text: text, Number: index + 1))
			let searched = s_keptUses.Aggregate(
				line.Text,
				static (cut, use) => cut.Replace(use, string.Empty, StringComparison.Ordinal)
			)
			where OtherWordForDirectory().IsMatch(searched)
			select $"{Path.GetRelativePath(root, path).Replace('\\', '/')}:{line.Number}",
		];

		AssertNone(offenders, rule: "N12: a place that holds files is not called a directory");
	}

	[TestMethod]
	public void AnEmptyStringIsStringEmptyWhereAConstantIsNotRequired()
	{
		string[] offenders =
		[
			.. from file in s_sources.Value
			from literal in file.Root.DescendantNodes().OfType<LiteralExpressionSyntax>()
			where literal.IsKind(SyntaxKind.StringLiteralExpression)
			where literal.Token.Text == "\"\"" && !RequiresConstant(literal)
			select $"{file.Relative}:{GetLine(literal)}",
		];

		AssertNone(offenders, rule: "N13: an empty string is written as a literal");
	}

	[TestMethod]
	public void AnAbstractionHasAtLeastTwoImplementations()
	{
		BaseTypeDeclarationSyntax[] types =
		[
			.. s_sources.Value.SelectMany(static file =>
				file.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
			),
		];

		string[] offenders =
		[
			.. from type in types
			where type is InterfaceDeclarationSyntax || IsAbstractClass(type)
			let name = type.Identifier.ValueText
			let implementations = types.Count(other => GetBaseNames(other).Contains(name))
			where implementations < 2
			select $"{name} has {implementations}",
		];

		AssertNone(offenders, rule: "C1: an abstraction has fewer than two implementations");
	}

	[TestMethod]
	public void NoMemberOfTheSourcesGoesUnread()
	{
		string allText = string.Join("\n", s_sources.Value.Select(static file => file.Text));

		// The build reports an unread private member, so this guard reads the rest.
		(string Name, string Where)[] candidates =
		[
			.. from file in s_sources.Value
			from member in file.Root.DescendantNodes().OfType<MemberDeclarationSyntax>()
			let name = FindCheckedName(member)
			where name is not null
			select (name, $"{file.Relative}:{GetLine(member)}"),
		];

		var declarations = candidates
			.GroupBy(static candidate => candidate.Name)
			.ToDictionary(static group => group.Key, static group => group.Count());

		// A name read nowhere but its declarations is unread; a shared name can hide one.
		string[] offenders =
		[
			.. from candidate in candidates
			let uses = Regex.Count(allText, $@"\b{Regex.Escape(candidate.Name)}\b")
			where uses <= declarations[candidate.Name]
			select $"{candidate.Name} ({candidate.Where})",
		];

		AssertNone(
			offenders,
			rule: "C2: a member of the sources is read by nothing in the sources"
		);
	}

	[TestMethod]
	public void NoPositionalRecordParameterGoesUnread()
	{
		// A positional parameter is a property that a named argument sets without reading it.
		HashSet<string> read =
		[
			.. from file in s_sources.Value
			from identifier in file.Root.DescendantNodes().OfType<IdentifierNameSyntax>()
			where identifier.Parent is not NameColonSyntax { Parent: ArgumentSyntax }
			select identifier.Identifier.ValueText,
		];

		string[] offenders =
		[
			.. from file in s_sources.Value
			from record in file.Root.DescendantNodes().OfType<RecordDeclarationSyntax>()
			from parameter in record.ParameterList?.Parameters ?? default
			where !read.Contains(parameter.Identifier.ValueText)
			select $"{record.Identifier.ValueText}.{parameter.Identifier.ValueText} ({file.Relative}:{GetLine(parameter)})",
		];

		AssertNone(
			offenders,
			rule: "C2: a positional record parameter is read by nothing in the sources"
		);
	}

	[TestMethod]
	public void AMemberCommentIsADocOnTheMember()
	{
		string[] offenders =
		[
			.. from file in s_sources.Value
			from member in file.Root.DescendantNodes()
			where IsMember(member) && HasCommentDirectlyAbove(member)
			select $"{file.Relative}:{GetLine(member)}",
		];

		GuardAssert.None(
			offenders,
			"A // comment stands directly above a member, where STYLEGUIDE.md asks for a /// doc"
		);
	}

	// A condition attribute keeps a test from starting, and RuleTestCoverageTests reads only a run in which every test started.
	// An ignored test is already left out of that run, so Ignore is the one condition the rule lets stand.
	// A test whose every row carries an IgnoreMessage starts no run either, so it is found as a condition too.
	[TestMethod]
	public void NoTestOfTheAnalyzerTestsCarriesAConditionAttribute()
	{
		MemberInfo[] members =
		[
			.. typeof(ConventionGuardTests)
				.Assembly.GetTypes()
				.Where(static type => type.GetCustomAttribute<TestClassAttribute>() is not null)
				.SelectMany(static type =>
					type.GetMethods()
						.Where(static method =>
							method.GetCustomAttribute<TestMethodAttribute>() is not null
						)
						.Prepend<MemberInfo>(type)
				),
		];

		Assert.IsNotEmpty(
			members,
			"No test is read from the assembly, so no attribute was checked."
		);

		GuardAssert.None(
			FindConditioned(members),
			"T6: a test ends Inconclusive from its body, never through a condition attribute, as CONVENTIONS.md says"
		);
	}

	[TestMethod]
	public void AConditionAttributeOnAClassOrAMethodIsFoundAndAnIgnoreIsNot()
	{
		Type fake = typeof(ConditionedFake);

		string[] found = FindConditioned([
			fake,
			fake.GetMethod(nameof(ConditionedFake.OnlyOnLinux))!,
			fake.GetMethod(nameof(ConditionedFake.Ignored))!,
			fake.GetMethod(nameof(ConditionedFake.Plain))!,
		]);

		Assert.HasCount(2, found);
		Assert.Contains("ConditionedFake carries [OSCondition]", found[0]);
		Assert.Contains("ConditionedFake.OnlyOnLinux carries [OSCondition]", found[1]);
	}

	[TestMethod]
	public void ATestWhoseEveryRowIsIgnoredIsFoundAndOneWithARowThatRunsIsNot()
	{
		Type fake = typeof(ConditionedFake);

		string[] found = FindConditioned([
			fake.GetMethod(nameof(ConditionedFake.EveryRowIgnored))!,
			fake.GetMethod(nameof(ConditionedFake.OneRowRuns))!,
			fake.GetMethod(nameof(ConditionedFake.Plain))!,
		]);

		Assert.HasCount(1, found);
		Assert.Contains("ConditionedFake.EveryRowIgnored ignores every row", found[0]);
	}

	private static string[] FindConditioned(IEnumerable<MemberInfo> members)
	{
		MemberInfo[] all = [.. members];

		return
		[
			.. from member in all
			from condition in member.GetCustomAttributes<ConditionBaseAttribute>()
			where condition is not IgnoreAttribute
			select $"{(member is Type type ? type.Name : $"{member.DeclaringType!.Name}.{member.Name}")} carries [{condition.GetType().Name.Replace("Attribute", string.Empty, StringComparison.Ordinal)}]",

			.. from method in all.OfType<MethodInfo>()
			let rows = method.GetCustomAttributes<DataRowAttribute>().ToArray()
			where rows.Length > 0 && rows.All(static row => row.IgnoreMessage is not null)
			select $"{method.DeclaringType!.Name}.{method.Name} ignores every row",
		];
	}

	// The MSTest analyzers allow a condition attribute on a test only, which is the shape the check below must find.
#pragma warning disable MSTEST0007, MSTEST0014, MSTEST0041, CA1822, IDE0060
	/// <summary>The shapes the T6 check must find, which no test of the assembly runs.</summary>
	[OSCondition(OperatingSystems.Windows)]
	private sealed class ConditionedFake
	{
		[OSCondition(OperatingSystems.Linux)]
		public void OnlyOnLinux() { }

		[Ignore("A fake is never run.")]
		public void Ignored() { }

		[DataRow(1, IgnoreMessage = "A fake is never run.")]
		[DataRow(2, IgnoreMessage = "A fake is never run.")]
		public void EveryRowIgnored(int value) { }

		[DataRow(1, IgnoreMessage = "A fake is never run.")]
		[DataRow(2)]
		public void OneRowRuns(int value) { }

		public void Plain() { }
	}
#pragma warning restore MSTEST0007, MSTEST0014, MSTEST0041, CA1822, IDE0060

	private static HashSet<string> ToSet(string words) =>
		[.. words.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)];

	private static string? FindShapeProblem(MethodDeclarationSyntax method)
	{
		string verb = GetVerb(method.Identifier.ValueText);
		TypeSyntax returns = method.ReturnType;
		string shape = returns.ToString();

		return verb switch
		{
			"Get" when returns is NullableTypeSyntax => "a Get never returns null; a Find does",
			"Report" when shape != "void" => "a Report returns nothing",
			"Try" when !ReturnsBool(method) => "a Try returns bool",
			"Create" when !IsChange(shape) => "a Create returns a change or a list of changes",
			"Find" when !CanComeBackEmpty(returns) => "a Find returns null, default, empty or -1",
			_ when shape == "bool" && s_actions.Contains(verb) => "a bool action is a Try",
			_ => null,
		};
	}

	/// <summary>The analyzer directories whose other files name the type, the root as "".</summary>
	private static string[] FindUserDirectories(string name, SourceFile declaredIn)
	{
		IEnumerable<string> directories =
			from other in s_sources.Value
			where IsAnalyzerFile(other) && other.Path != declaredIn.Path
			where other.Names.Any(used => used.Identifier.ValueText == name)
			select other.Directory;

		return [.. directories.Distinct()];
	}

	/// <summary>Maps each top-level type that only one family declares to that family.</summary>
	private static Dictionary<string, string> FindFamilyTypes()
	{
		IEnumerable<IGrouping<string, string>> declarations =
			from file in s_sources.Value
			from type in GetTopLevelTypes(file)
			group IsAnalyzerFamily(file) ? file.Directory : "" by type.Identifier.ValueText;

		return declarations
			.Where(static owners => owners.Distinct().Count() == 1 && owners.First().Length > 0)
			.ToDictionary(static owners => owners.Key, static owners => owners.First());
	}

	private static IEnumerable<string> ReadAssemblyReferences(string project)
	{
		string path = RepositoryPaths.Below("src", project, project + ".csproj");

		// A reference that loads the analyzers over the project's own sources carries no assembly.
		return from reference in XDocument.Load(path).Descendants("ProjectReference")
			where (string?)reference.Attribute("ReferenceOutputAssembly") != "false"
			select Path.GetFileName((string?)reference.Attribute("Include") ?? "");
	}

	private static string? FindCheckedName(MemberDeclarationSyntax member)
	{
		bool exempt =
			member.Modifiers.Any(SyntaxKind.OverrideKeyword)
			|| member.Modifiers.Any(SyntaxKind.PrivateKeyword)
			|| member.Parent is InterfaceDeclarationSyntax
			|| member.GetLeadingTrivia().ToFullString().Contains("test seam");

		string? name = member switch
		{
			MethodDeclarationSyntax { ExplicitInterfaceSpecifier: null } method => method
				.Identifier
				.ValueText,

			PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: null } property => property
				.Identifier
				.ValueText,

			FieldDeclarationSyntax { Declaration.Variables.Count: 1 } field => field
				.Declaration
				.Variables[0]
				.Identifier
				.ValueText,

			_ => null,
		};

		return exempt || name is null || IsObjectMember(name) ? null : name;
	}

	// A read of a diagnostic's properties, or a property bag written by hand.
	private static bool IsPropertyCodec(SyntaxNode node)
	{
		return node is MemberAccessExpressionSyntax { Name.Identifier.Text: "Properties" }
			|| (
				node is GenericNameSyntax { Identifier.Text: "ImmutableDictionary" } generic
				&& generic.TypeArgumentList.ToString() == "<string, string?>"
			);
	}

	private static bool IsObjectMember(string name) =>
		name is "Equals" or "GetHashCode" or "Compare" or "Dispose" or "GetEnumerator";

	private static bool IsChange(string shape) =>
		ChangeShape().IsMatch(shape.Replace("?", "", StringComparison.Ordinal));

	private static bool CanComeBackEmpty(TypeSyntax type)
	{
		string shape = type.ToString();

		return type is NullableTypeSyntax or ArrayTypeSyntax
			|| shape == "int"
			|| s_emptyDefaults.Contains(shape)
			|| (
				type is GenericNameSyntax generic
				&& s_emptyCollections.Contains(generic.Identifier.ValueText)
			);
	}

	private static bool IsAbstractClass(BaseTypeDeclarationSyntax type) =>
		type is ClassDeclarationSyntax && type.Modifiers.Any(SyntaxKind.AbstractKeyword);

	private static bool IsAnalyzerBase(BaseTypeDeclarationSyntax type)
	{
		string[] baseNames = GetBaseNames(type);

		return IsAbstractClass(type)
			&& (baseNames.Contains("DiagnosticAnalyzer") || baseNames.Contains("StanzaAnalyzer"));
	}

	private static bool IsAnalyzerFile(SourceFile file) => file.Project == AnalyzerProject;

	private static bool IsAnalyzerFamily(SourceFile file) =>
		file.Project == AnalyzerProject && file.Directory.Length > 0;

	private static bool IsAnalyzerRoot(SourceFile file) =>
		file.Project == AnalyzerProject && file.Directory.Length == 0;

	private static bool IsOverride(SyntaxNode? declaration)
	{
		return declaration is BaseMethodDeclarationSyntax { Modifiers: var modifiers }
			&& modifiers.Any(SyntaxKind.OverrideKeyword);
	}

	private static bool IsProjectRoot(SourceFile file) =>
		file.Directory.Length == 0 && file.Project is AnalyzerProject or FixProject;

	private static bool IsGeneratedRegex(MethodDeclarationSyntax method) =>
		HasAttribute(method.AttributeLists, name: "GeneratedRegex");

	private static bool HasAttribute(SyntaxList<AttributeListSyntax> lists, string name)
	{
		return lists
			.SelectMany(static list => list.Attributes)
			.Any(attribute => attribute.Name.ToString() == name);
	}

	private static bool ReturnsBool(MethodDeclarationSyntax method) =>
		method.ReturnType.ToString() is "bool" or "Task<bool>";

	private static string GetVerb(string name) =>
		FirstWord().Match(name) is { Success: true } match ? match.Value : name;

	private static string[] GetBaseNames(BaseTypeDeclarationSyntax type)
	{
		if (type.BaseList is not { } bases)
			return [];

		return
		[
			.. from baseType in bases.Types
			select baseType.Type is GenericNameSyntax generic
				? generic.Identifier.ValueText
				: baseType.Type.ToString().Split('.')[^1],
		];
	}

	private static IEnumerable<BaseTypeDeclarationSyntax> GetTopLevelTypes(SourceFile file)
	{
		return from type in file.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
			where type.Parent is not BaseTypeDeclarationSyntax
			select type;
	}

	private static IEnumerable<MethodFact> FindMethods()
	{
		return from file in s_sources.Value
			from method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>()
			where !method.Modifiers.Any(SyntaxKind.OverrideKeyword)
			where method.ExplicitInterfaceSpecifier is null && method.Identifier.ValueText != "Main"
			select new MethodFact(
				Syntax: method,
				Name: method.Identifier.ValueText,
				Where: $"{file.Relative}:{GetLine(method)}"
			);
	}

	/// <summary>A member, or a positional parameter, which is a property of its record.</summary>
	private static bool IsMember(SyntaxNode node)
	{
		return node
			is ParameterSyntax { Parent.Parent: RecordDeclarationSyntax }
				or (
					MemberDeclarationSyntax
					and not (BaseNamespaceDeclarationSyntax or GlobalStatementSyntax)
				);
	}

	// The run ends on the line above the member, with no blank line or directive between them.
	private static bool HasCommentDirectlyAbove(SyntaxNode member)
	{
		SyntaxTriviaList trivia = member.GetLeadingTrivia();
		int lineBreaks = 0;

		for (int index = trivia.Count - 1; index >= 0; index--)
		{
			if (trivia[index].IsKind(SyntaxKind.SingleLineCommentTrivia))
				return lineBreaks == 1;

			if (trivia[index].IsKind(SyntaxKind.EndOfLineTrivia))
				lineBreaks++;
			else if (!trivia[index].IsKind(SyntaxKind.WhitespaceTrivia))
				return false;
		}

		return false;
	}

	// A pattern, a case label, a default value, an attribute argument and a const take a constant, which string.Empty is not.
	private static bool RequiresConstant(LiteralExpressionSyntax literal) =>
		literal.Ancestors().Any(IsConstantContext);

	private static bool IsConstantContext(SyntaxNode node)
	{
		return node switch
		{
			ConstantPatternSyntax or CaseSwitchLabelSyntax => true,
			ParameterSyntax or AttributeArgumentSyntax => true,
			FieldDeclarationSyntax field => field.Modifiers.Any(SyntaxKind.ConstKeyword),
			LocalDeclarationStatementSyntax local => local.Modifiers.Any(SyntaxKind.ConstKeyword),
			_ => false,
		};
	}

	private static int GetLine(SyntaxNode node) =>
		node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

	private static void AssertNone(IReadOnlyCollection<string> offenders, string rule) =>
		GuardAssert.None(offenders, $"{rule}, as CONVENTIONS.md says");

	private static SourceFile[] ReadSources(string tree)
	{
		string source = RepositoryPaths.Below(tree);

		return
		[
			.. RepositoryFiles
				.Under(source, pattern: "*.cs")
				.Select(path => ReadSource(source, path)),
		];
	}

	private static SourceFile ReadSource(string source, string path)
	{
		string relative = Path.GetRelativePath(source, path).Replace('\\', '/');
		string[] segments = relative.Split('/');
		string text = File.ReadAllText(path);

		SyntaxTree tree = CSharpSyntaxTree.ParseText(
			text,
			new CSharpParseOptions(LanguageVersion.Preview),
			path
		);

		return new SourceFile(
			Path: path,
			Relative: relative,
			Project: segments[0],
			Directory: segments.Length > 2 ? segments[1] : "",
			Root: tree.GetRoot(),
			Text: text
		);
	}

	private sealed record SourceFile(
		string Path,
		string Relative,
		string Project,
		string Directory,
		SyntaxNode Root,
		string Text
	)
	{
		/// <summary>The names the code and its crefs spell; a comment's prose names none.</summary>
		public SimpleNameSyntax[] Names { get; } =
		[.. Root.DescendantNodes(descendIntoTrivia: true).OfType<SimpleNameSyntax>()];
	}

	private sealed record MethodFact(MethodDeclarationSyntax Syntax, string Name, string Where);
}
