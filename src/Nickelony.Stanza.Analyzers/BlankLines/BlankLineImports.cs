using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the kind of each import directive, which the import rules group and separate by.</summary>
internal static class BlankLineImports
{
	public static bool IsImportPair(SyntaxNode previous, SyntaxNode next) => IsImportDirective(previous) && IsImportDirective(next);

	/// <summary>Whether a boundary ends the import section; the import section rule owns that boundary.</summary>
	public static bool IsImportTerminator(SyntaxNode previous, SyntaxNode next) => IsImportDirective(previous) && !IsImportDirective(next);

	public static bool IsImportKindChange(SyntaxNode previous, SyntaxNode next)
	{
		ImportKind previousKind = GetImportKind(previous);
		ImportKind nextKind = GetImportKind(next);

		return previousKind != ImportKind.None && nextKind != ImportKind.None && previousKind != nextKind;
	}

	private static ImportKind GetImportKind(SyntaxNode node)
	{
		if (node is ExternAliasDirectiveSyntax)
			return ImportKind.ExternAlias;

		if (node is not UsingDirectiveSyntax directive)
			return ImportKind.None;

		bool isGlobal = !directive.GlobalKeyword.IsKind(SyntaxKind.None);

		if (directive.Alias is not null)
			return isGlobal ? ImportKind.GlobalAliasUsing : ImportKind.AliasUsing;

		bool isStatic = !directive.StaticKeyword.IsKind(SyntaxKind.None);

		return (isGlobal, isStatic) switch
		{
			(true, true) => ImportKind.GlobalStaticUsing,
			(true, false) => ImportKind.GlobalUsing,
			(false, true) => ImportKind.StaticUsing,
			_ => ImportKind.Using,
		};
	}

	private static bool IsImportDirective(SyntaxNode node) => GetImportKind(node) != ImportKind.None;

	private enum ImportKind
	{
		None,
		ExternAlias,
		Using,
		StaticUsing,
		AliasUsing,
		GlobalUsing,
		GlobalStaticUsing,
		GlobalAliasUsing,
	}
}
