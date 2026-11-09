using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nickelony.Stanza.Analyzers;

internal static class StanzaKinds
{
	/// <summary>
	/// The kind of a C# 14 extension block, which holds members, read by name because Roslyn 4.11 predates it.
	/// </summary>
	private static readonly ImmutableArray<SyntaxKind> s_extensionBlockKinds = Enum.TryParse("ExtensionBlockDeclaration", out SyntaxKind kind)
		? [kind]
		: [];

	/// <summary>Lists the nodes that hold arguments; a tuple literal is its own list.</summary>
	public static readonly ImmutableArray<SyntaxKind> ArgumentListKinds =
	[
		SyntaxKind.ArgumentList,
		SyntaxKind.AttributeArgumentList,
		SyntaxKind.BracketedArgumentList,
		SyntaxKind.TupleExpression,
	];

	public static readonly ImmutableArray<SyntaxKind> CallKinds =
	[
		SyntaxKind.BaseConstructorInitializer,
		SyntaxKind.ElementAccessExpression,
		SyntaxKind.ImplicitObjectCreationExpression,
		SyntaxKind.InvocationExpression,
		SyntaxKind.ObjectCreationExpression,
		SyntaxKind.PrimaryConstructorBaseType,
		SyntaxKind.ThisConstructorInitializer,
	];

	public static readonly ImmutableArray<SyntaxKind> CollectionElementKinds =
	[
		SyntaxKind.ArrayInitializerExpression,
		SyntaxKind.CollectionExpression,
		SyntaxKind.CollectionInitializerExpression,
		SyntaxKind.ComplexElementInitializerExpression,
	];

	public static readonly ImmutableArray<SyntaxKind> InitializerMemberKinds =
	[
		SyntaxKind.AnonymousObjectCreationExpression,
		SyntaxKind.ObjectInitializerExpression,
		SyntaxKind.WithInitializerExpression,
	];

	/// <summary>Lists the nodes whose last element may end in a comma.</summary>
	public static readonly ImmutableArray<SyntaxKind> TrailingCommaKinds =
	[
		SyntaxKind.EnumDeclaration,
		SyntaxKind.ListPattern,
		SyntaxKind.PropertyPatternClause,
		SyntaxKind.SwitchExpression,
		// The braces of a complex element hold the arguments of an `Add` call, which take no trailing comma.
		.. CollectionElementKinds.Remove(SyntaxKind.ComplexElementInitializerExpression),
		.. InitializerMemberKinds,
	];

	/// <summary>Lists the nodes that hold parameters; a tuple type is its own list.</summary>
	public static readonly ImmutableArray<SyntaxKind> ParameterListKinds =
	[
		SyntaxKind.BracketedParameterList,
		SyntaxKind.ParameterList,
		SyntaxKind.TupleType,
	];

	public static readonly ImmutableArray<SyntaxKind> MarkupLiteralKinds =
	[
		SyntaxKind.StringLiteralExpression,
		SyntaxKind.Utf8StringLiteralExpression,
	];

	public static bool IsMarkupLiteralToken(SyntaxToken token) =>
		token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken) || token.IsKind(SyntaxKind.Utf8MultiLineRawStringLiteralToken);

	/// <summary>Lists the containers a blank-line rule may judge; accessor lists belong to their own rules.</summary>
	public static readonly ImmutableArray<SyntaxKind> ContainerKinds =
	[
		SyntaxKind.Block,
		SyntaxKind.ClassDeclaration,
		SyntaxKind.CompilationUnit,
		SyntaxKind.EnumDeclaration,
		SyntaxKind.FileScopedNamespaceDeclaration,
		SyntaxKind.InterfaceDeclaration,
		SyntaxKind.NamespaceDeclaration,
		SyntaxKind.RecordDeclaration,
		SyntaxKind.RecordStructDeclaration,
		SyntaxKind.StructDeclaration,
		SyntaxKind.SwitchSection,
		.. s_extensionBlockKinds,
	];

	/// <summary>Lists every container whose units <see cref="StanzaSyntax.ReadSiblings"/> reads.</summary>
	public static readonly ImmutableArray<SyntaxKind> SiblingContainerKinds =
	[
		.. ContainerKinds,
		.. ArgumentListKinds,
		.. CollectionElementKinds,
		.. InitializerMemberKinds,
		.. ParameterListKinds,
		SyntaxKind.AccessorList,
		SyntaxKind.SwitchExpression,
		SyntaxKind.SwitchStatement,
		SyntaxKind.VariableDeclaration,
	];

	public static readonly ImmutableArray<SyntaxKind> MemberContainerKinds =
	[
		SyntaxKind.ClassDeclaration,
		SyntaxKind.CompilationUnit,
		SyntaxKind.EnumDeclaration,
		SyntaxKind.FileScopedNamespaceDeclaration,
		SyntaxKind.InterfaceDeclaration,
		SyntaxKind.NamespaceDeclaration,
		SyntaxKind.RecordDeclaration,
		SyntaxKind.RecordStructDeclaration,
		SyntaxKind.StructDeclaration,
		.. s_extensionBlockKinds,
	];

	public static readonly ImmutableArray<SyntaxKind> ImportContainerKinds =
	[
		SyntaxKind.CompilationUnit,
		SyntaxKind.FileScopedNamespaceDeclaration,
		SyntaxKind.NamespaceDeclaration,
	];

	public static readonly ImmutableArray<SyntaxKind> BraceContainerKinds =
	[
		SyntaxKind.AccessorList,
		SyntaxKind.AnonymousObjectCreationExpression,
		SyntaxKind.ArrayInitializerExpression,
		SyntaxKind.Block,
		SyntaxKind.ClassDeclaration,
		SyntaxKind.CollectionInitializerExpression,
		SyntaxKind.ComplexElementInitializerExpression,
		SyntaxKind.EnumDeclaration,
		SyntaxKind.InterfaceDeclaration,
		SyntaxKind.NamespaceDeclaration,
		SyntaxKind.ObjectInitializerExpression,
		SyntaxKind.RecordDeclaration,
		SyntaxKind.RecordStructDeclaration,
		SyntaxKind.StructDeclaration,
		SyntaxKind.SwitchExpression,
		SyntaxKind.SwitchStatement,
		SyntaxKind.WithInitializerExpression,
		.. s_extensionBlockKinds,
	];

	public static readonly ImmutableArray<SyntaxKind> BraceStatementKinds =
	[
		SyntaxKind.DoStatement,
		SyntaxKind.FixedStatement,
		SyntaxKind.ForEachStatement,
		SyntaxKind.ForEachVariableStatement,
		SyntaxKind.ForStatement,
		SyntaxKind.IfStatement,
		SyntaxKind.LockStatement,
		SyntaxKind.UsingStatement,
		SyntaxKind.WhileStatement,
	];

	public static readonly ImmutableArray<SyntaxKind> ClauseStatementKinds =
	[
		SyntaxKind.DoStatement,
		SyntaxKind.IfStatement,
		SyntaxKind.TryStatement,
	];

	/// <summary>
	/// Lists the kinds whose empty braces a rule may join; a namespace and a <see langword="switch"/> are excluded.
	/// </summary>
	public static readonly ImmutableArray<SyntaxKind> EmptyBlockKinds =
	[
		SyntaxKind.AnonymousObjectCreationExpression,
		SyntaxKind.ArrayInitializerExpression,
		SyntaxKind.Block,
		SyntaxKind.ClassDeclaration,
		SyntaxKind.CollectionInitializerExpression,
		SyntaxKind.ComplexElementInitializerExpression,
		SyntaxKind.EnumDeclaration,
		SyntaxKind.InterfaceDeclaration,
		SyntaxKind.ObjectInitializerExpression,
		SyntaxKind.RecordDeclaration,
		SyntaxKind.RecordStructDeclaration,
		SyntaxKind.StructDeclaration,
		SyntaxKind.WithInitializerExpression,
		.. s_extensionBlockKinds,
	];
}
