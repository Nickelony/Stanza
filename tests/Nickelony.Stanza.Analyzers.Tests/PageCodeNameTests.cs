using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Guards that each type and member a page names in code is still in the source.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class PageCodeNameTests
{
	// The changelog names code of past releases, so it is not read.
	private static readonly string[] s_skippedPages = ["CHANGELOG.md"];

	// A file named like a type, such as `MarkupKeys.cs`, is a file and not a member.
	private static readonly string[] s_fileExtensions =
	[
		"cs",
		"md",
		"json",
		"yml",
		"props",
		"targets",
		"csproj",
		"ps1",
	];

	// A page spells these in code and none is a member of a type of this repository: a framework member and project names.
	private static readonly string[] s_otherNames =
	[
		"string.Empty",
		"Analyzers.Tests",
		"Markup.Core",
		"Markup.Tests",
	];

	private const int ResolvedFloor = 2;

	[GeneratedRegex(
		@"`(?<type>[A-Za-z_]\w*)\.(?<member>[A-Za-z_]\w*)(?:\([^`]*\))?`",
		RegexOptions.CultureInvariant
	)]
	private static partial Regex CodeName();

	[TestMethod]
	public void EachMemberAGuidePageNamesInCodeIsDeclaredByItsType()
	{
		string root = RepositoryPaths.Root();

		Dictionary<string, HashSet<string>> types = ReadMembers(
			RepositoryFiles
				.Under(Path.Combine(root, "src"), "*.cs")
				.Concat(RepositoryFiles.Under(Path.Combine(root, "tests"), "*.cs"))
				.Select(File.ReadAllText)
		);

		Assert.IsNotEmpty(
			types,
			"No type is read from the source, so no page name could be checked."
		);

		HashSet<string> resolved = new(StringComparer.Ordinal);

		List<string> unresolved = [];
		List<string> unknown = [];

		foreach (string page in GuidePages(root))
		{
			string relative = MarkdownScan.Relative(root, page);

			foreach ((int number, string line) in MarkdownScan.ProseLines(File.ReadLines(page)))
			{
				resolved.UnionWith(FindResolved(line, types));

				unresolved.AddRange(
					FindUnresolved(line, types).Select(name => $"{relative}:{number}  {name}")
				);

				unknown.AddRange(
					FindUnknownTypes(line, types).Select(name => $"{relative}:{number}  {name}")
				);
			}
		}

		// A pattern or a scan that stopped reading the pages would resolve no member.
		Assert.IsGreaterThanOrEqualTo(
			ResolvedFloor,
			resolved.Count,
			$"The pages name {resolved.Count} members the source declares, so the scan may have stopped reading them."
		);

		GuardAssert.None(unresolved, "A page names a member that its type no longer declares");

		GuardAssert.None(
			unknown,
			"A page names a type that no source declares",
			"A name of something other than a type of this repository goes in s_otherNames."
		);
	}

	[TestMethod]
	public void ARenamedMemberIsFoundAndAnUnknownTypeIsLeftAlone()
	{
		Dictionary<string, HashSet<string>> types = ReadMembers([
			"class Widget { int Count; void Reset() { } int Size { get; } enum Kind { One } }",
		]);

		string line =
			"Call `Widget.Reset()` and `Widget.Count`, not `Widget.Clear(x, y)`; `Widget.Kind` stays.";

		Assert.AreSequenceEqual(["Widget.Clear"], FindUnresolved(line, types));

		Assert.AreSequenceEqual(
			["Widget.Reset", "Widget.Count", "Widget.Kind"],
			FindResolved(line, types)
		);
	}

	[TestMethod]
	public void ATypeNoSourceDeclaresIsFoundUnlessThePageNamesItAsAnotherThing()
	{
		Dictionary<string, HashSet<string>> types = ReadMembers(["class Widget { }"]);

		string line =
			"A renamed `Gadget.Reset()` is found; `string.Empty`, `Markup.Core` and `Widget.cs` are not.";

		Assert.AreSequenceEqual(["Gadget.Reset"], FindUnknownTypes(line, types));
		Assert.IsEmpty(FindUnresolved(line, types));
		Assert.IsEmpty(FindResolved(line, types));
	}

	[TestMethod]
	public void AFileNamedLikeATypeIsNoMemberName()
	{
		Dictionary<string, HashSet<string>> types = ReadMembers(["class Widget { }"]);
		Assert.IsEmpty(FindUnresolved("`Widget.cs` and `Widget.md`", types));
	}

	[TestMethod]
	public void APositionalRecordParameterAndATypeParameterAreMembersToo()
	{
		Dictionary<string, HashSet<string>> types = ReadMembers([
			"record Point(int X, int Y); interface IShape { int Area(); } enum Mode { Fast, Slow }",
		]);

		Assert.IsEmpty(FindUnresolved("`Point.X` `IShape.Area` `Mode.Slow`", types));
		Assert.AreSequenceEqual(["Mode.Medium"], FindUnresolved("`Mode.Medium`", types));
	}

	private static IEnumerable<string> GuidePages(string root)
	{
		string docs = Path.Combine(root, "docs");

		return MarkdownScan
			.Files(root, s_skippedPages)
			.Where(file =>
				Path.GetDirectoryName(file) is { } directory
				&& (
					string.Equals(directory, root, StringComparison.OrdinalIgnoreCase)
					|| string.Equals(directory, docs, StringComparison.OrdinalIgnoreCase)
				)
			);
	}

	// The name of a type of this repository that a page spells in code is declared by its source.
	private static List<string> FindUnresolved(
		string line,
		Dictionary<string, HashSet<string>> types
	) => FindNames(line, types, NameKind.MissingMember);

	private static List<string> FindResolved(
		string line,
		Dictionary<string, HashSet<string>> types
	) => FindNames(line, types, NameKind.Resolved);

	// A type no source declares is a renamed type unless the page means something other than a type.
	private static List<string> FindUnknownTypes(
		string line,
		Dictionary<string, HashSet<string>> types
	) => FindNames(line, types, NameKind.UnknownType);

	private static List<string> FindNames(
		string line,
		Dictionary<string, HashSet<string>> types,
		NameKind kind
	)
	{
		return
		[
			.. CodeName()
				.Matches(line)
				.Select(match =>
					(Type: match.Groups["type"].Value, Member: match.Groups["member"].Value)
				)
				.Where(name =>
					!s_fileExtensions.Contains(name.Member)
					&& !s_otherNames.Contains($"{name.Type}.{name.Member}")
					&& Classify(name.Type, name.Member, types) == kind
				)
				.Select(name => $"{name.Type}.{name.Member}"),
		];
	}

	private static NameKind Classify(
		string type,
		string member,
		Dictionary<string, HashSet<string>> types
	)
	{
		if (!types.TryGetValue(type, out HashSet<string>? members))
			return NameKind.UnknownType;

		return members.Contains(member) ? NameKind.Resolved : NameKind.MissingMember;
	}

	private enum NameKind
	{
		Resolved,
		MissingMember,
		UnknownType,
	}

	private static Dictionary<string, HashSet<string>> ReadMembers(IEnumerable<string> sources)
	{
		Dictionary<string, HashSet<string>> types = new(StringComparer.Ordinal);

		foreach (string source in sources)
		{
			SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();

			foreach (
				BaseTypeDeclarationSyntax type in root.DescendantNodes()
					.OfType<BaseTypeDeclarationSyntax>()
			)
			{
				if (!types.TryGetValue(type.Identifier.ValueText, out HashSet<string>? members))
					types[type.Identifier.ValueText] = members = new(StringComparer.Ordinal);

				members.UnionWith(NameMembers(type));
			}
		}

		return types;
	}

	private static IEnumerable<string> NameMembers(BaseTypeDeclarationSyntax type)
	{
		IEnumerable<SyntaxNode> declared = type switch
		{
			TypeDeclarationSyntax typeDeclaration => typeDeclaration.Members.Cast<SyntaxNode>(),
			EnumDeclarationSyntax enumDeclaration => enumDeclaration.Members,
			_ => [],
		};

		foreach (SyntaxNode node in declared)
		{
			switch (node)
			{
				case MethodDeclarationSyntax method:
					yield return method.Identifier.ValueText;
					break;

				case PropertyDeclarationSyntax property:
					yield return property.Identifier.ValueText;
					break;

				case EventDeclarationSyntax @event:
					yield return @event.Identifier.ValueText;
					break;

				case FieldDeclarationSyntax field:
					foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
						yield return variable.Identifier.ValueText;

					break;

				case EventFieldDeclarationSyntax eventField:
					foreach (VariableDeclaratorSyntax variable in eventField.Declaration.Variables)
						yield return variable.Identifier.ValueText;

					break;

				case EnumMemberDeclarationSyntax member:
					yield return member.Identifier.ValueText;
					break;

				case BaseTypeDeclarationSyntax nested when nested != type:
					yield return nested.Identifier.ValueText;
					break;

				case DelegateDeclarationSyntax @delegate:
					yield return @delegate.Identifier.ValueText;
					break;
			}
		}

		if (type is RecordDeclarationSyntax { ParameterList: { } parameters })
		{
			foreach (ParameterSyntax parameter in parameters.Parameters)
				yield return parameter.Identifier.ValueText;
		}

		if (type is TypeDeclarationSyntax { ParameterList: { } primary })
		{
			foreach (ParameterSyntax parameter in primary.Parameters)
				yield return parameter.Identifier.ValueText;
		}
	}
}
