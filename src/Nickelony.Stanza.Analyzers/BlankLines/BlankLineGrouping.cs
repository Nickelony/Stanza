using System;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the boundaries of the runs of statements, members and declarations a blank line splits.</summary>
internal static class BlankLineGrouping
{
	/// <summary>A boundary a repeated run asks for, with the run's start and its end, one past the last.</summary>
	public readonly record struct RunBorder(int Boundary, int RunStart, int RunEnd);

	public static ImmutableArray<int> ReadDeclarationRunBoundaries(
		ImmutableArray<SyntaxNode> siblings,
		StanzaOptions options,
		GapOwners gapOwners,
		CancellationToken cancellationToken
	) => ReadBoundaries(ReadDeclarationRunBorders(siblings, options, gapOwners, gathered: null, cancellationToken));

	/// <summary>Reads the declaration run borders, as if the gathered gaps held no blank line.</summary>
	public static ImmutableArray<RunBorder> ReadDeclarationRunBorders(
		ImmutableArray<SyntaxNode> siblings,
		StanzaOptions options,
		GapOwners gapOwners,
		ImmutableHashSet<int>? gathered,
		CancellationToken cancellationToken
	)
	{
		if (siblings.Length < 2)
			return [];

		return ReadRunBorders(
			siblings,
			getKey: node => GetDeclarationRunKey(node, options),
			includesLoneVariableBoundary: true,
			gapOwners,
			gathered,
			cancellationToken
		);
	}

	public static ImmutableArray<int> ReadMemberGroupBoundaries(
		ImmutableArray<SyntaxNode> siblings,
		GapOwners gapOwners,
		CancellationToken cancellationToken
	)
	{
		if (siblings.Length < 2)
			return [];

		return ReadBoundaries(
			ReadRunBorders(siblings, getKey: GetMemberGroupKey, includesLoneVariableBoundary: true, gapOwners, gathered: null, cancellationToken)
		);
	}

	public static ImmutableArray<int> ReadModifierGroupBoundaries(
		ImmutableArray<SyntaxNode> siblings,
		GapOwners gapOwners,
		CancellationToken cancellationToken
	)
	{
		if (siblings.Length < 2)
			return [];

		return ReadBoundaries(
			ReadRunBorders(siblings, getKey: GetModifierGroupKey, includesLoneVariableBoundary: false, gapOwners, gathered: null, cancellationToken)
		);
	}

	private static ImmutableArray<RunBorder> ReadRunBorders(
		ImmutableArray<SyntaxNode> siblings,
		Func<SyntaxNode, (RunKind Kind, string? Name)> getKey,
		bool includesLoneVariableBoundary,
		GapOwners gapOwners,
		ImmutableHashSet<int>? gathered,
		CancellationToken cancellationToken
	)
	{
		(RunKind Kind, string? Name)[] keys = new (RunKind, string?)[siblings.Length];

		for (int index = 0; index < siblings.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			keys[index] = getKey(siblings[index]);
		}

		ImmutableArray<Run> runs = WalkRuns(keys, siblings, gapOwners, gathered, cancellationToken);
		ImmutableArray<RunBorder>.Builder borders = ImmutableArray.CreateBuilder<RunBorder>();

		for (int index = 0; index < runs.Length; index++)
		{
			Run run = runs[index];

			if (!run.IsRepeated)
				continue;

			// A declaration on its own is set apart only while it stands directly above the run.
			// A caller that reads the gathered gaps keeps that boundary once a blank line stands below the declaration too,
			// since the rule writes that blank line and a fix order that wrote it first would otherwise judge the gap alone.
			if (
				includesLoneVariableBoundary
				&& run.Start >= 2
				&& IsVariableLikeDeclaration(siblings[run.Start - 1])
				&& !runs[index - 1].IsRepeated
				&& (gathered is not null || !HasBlankLineBefore(siblings, run.Start, gathered: null))
				&& !IsCommentedSiblingGap(
					siblings,
					boundaryIndex: run.Start,
					commentedSiblingOwnerId: gapOwners.CommentedSiblingId,
					cancellationToken
				)
			)
			{
				borders.Add(new RunBorder(run.Start - 1, run.Start, run.End));
			}

			if (run.Start > 0)
				borders.Add(new RunBorder(run.Start, run.Start, run.End));

			if (run.End < siblings.Length)
				borders.Add(new RunBorder(run.End, run.Start, run.End));
		}

		return borders.ToImmutable();
	}

	private static ImmutableArray<int> ReadBoundaries(ImmutableArray<RunBorder> borders)
	{
		// The runs are walked in order and the borders they add never fall behind the last one added,
		// so the walk appends the boundaries straight into the builder,
		// rather than collecting them into a set and sorting that.
		ImmutableArray<int>.Builder boundaries = ImmutableArray.CreateBuilder<int>();
		int last = -1;

		foreach (RunBorder border in borders)
		{
			if (border.Boundary <= last)
				continue;

			boundaries.Add(border.Boundary);
			last = border.Boundary;
		}

		return boundaries.ToImmutable();
	}

	/// <summary>Whether ZA0005 keeps the gap blank, so it ends a run even before its blank line is written.</summary>
	/// <remarks>
	/// A pragma that silences ZA0005 over the gap leaves it inside the run, as <c>none</c> does.
	/// </remarks>
	private static bool IsMultiLineGap(
		ImmutableArray<SyntaxNode> siblings,
		int boundaryIndex,
		string? multiLineGapOwnerId,
		CancellationToken cancellationToken
	)
	{
		if (
			multiLineGapOwnerId is null
			|| (!StanzaSyntax.IsMultiLine(siblings[boundaryIndex - 1]) && !StanzaSyntax.IsMultiLine(siblings[boundaryIndex]))
		)
		{
			return false;
		}

		return !StanzaPragmas.IsSilencedByPragma(
			syntaxTree: siblings[boundaryIndex].SyntaxTree,
			ruleId: multiLineGapOwnerId,
			position: siblings[boundaryIndex].SpanStart,
			cancellationToken
		);
	}

	/// <summary>Whether ZA0047 keeps the gap blank, so it ends a run even before its blank line is written.</summary>
	/// <remarks>
	/// ZA0047 separates a marker only from a unit beside it, so the first and the last directive of the gap decide.
	/// </remarks>
	private static bool IsRegionMarkerGap(SyntaxNode next, string? ownerId, CancellationToken cancellationToken)
	{
		if (ownerId is null)
			return false;

		DirectiveTriviaSyntax? first = null;
		DirectiveTriviaSyntax? last = null;

		// A directive is leading trivia of the token after it, so the next unit holds every directive of the gap.
		foreach (SyntaxTrivia trivia in next.GetLeadingTrivia())
		{
			if (trivia.GetStructure() is not DirectiveTriviaSyntax directive)
				continue;

			first ??= directive;
			last = directive;
		}

		return IsKeptMarker(first, ownerId, cancellationToken) || IsKeptMarker(last, ownerId, cancellationToken);
	}

	/// <summary>Whether ZA0053 keeps the gap blank, so it ends a run even before its blank line is written.</summary>
	/// <remarks>
	/// A pragma that silences ZA0053 over the gap leaves it inside the run, as <c>none</c> does.
	/// </remarks>
	private static bool IsCommentedSiblingGap(
		ImmutableArray<SyntaxNode> siblings,
		int boundaryIndex,
		string? commentedSiblingOwnerId,
		CancellationToken cancellationToken
	)
	{
		if (commentedSiblingOwnerId is null || !BlankLineGaps.IsKeptBlankAboveComment(siblings[boundaryIndex - 1], siblings[boundaryIndex]))
			return false;

		return !StanzaPragmas.IsSilencedByPragma(
			syntaxTree: siblings[boundaryIndex].SyntaxTree,
			ruleId: commentedSiblingOwnerId,
			position: siblings[boundaryIndex].SpanStart,
			cancellationToken
		);
	}

	/// <summary>Whether the directive is a region marker that ZA0047 is not silenced at by a pragma.</summary>
	private static bool IsKeptMarker(SyntaxNode? directive, string ruleId, CancellationToken cancellationToken)
	{
		if (directive is not (RegionDirectiveTriviaSyntax or EndRegionDirectiveTriviaSyntax))
			return false;

		int position = directive.SpanStart;
		return !StanzaPragmas.IsSilencedByPragma(syntaxTree: directive.SyntaxTree, ruleId, position, cancellationToken);
	}

	/// <summary>Whether ZA0005, ZA0047 or ZA0053 keeps the gap, so a run ends there before it is written.</summary>
	public static bool IsGapKeptBlank(
		ImmutableArray<SyntaxNode> siblings,
		int boundaryIndex,
		GapOwners gapOwners,
		CancellationToken cancellationToken
	)
	{
		return IsMultiLineGap(siblings, boundaryIndex, multiLineGapOwnerId: gapOwners.MultiLineId, cancellationToken)
			|| IsRegionMarkerGap(siblings[boundaryIndex], gapOwners.RegionMarkerId, cancellationToken)
			|| IsCommentedSiblingGap(siblings, boundaryIndex, commentedSiblingOwnerId: gapOwners.CommentedSiblingId, cancellationToken);
	}

	private static ImmutableArray<Run> WalkRuns(
		(RunKind Kind, string? Name)[] keys,
		ImmutableArray<SyntaxNode> siblings,
		GapOwners gapOwners,
		ImmutableHashSet<int>? gathered,
		CancellationToken cancellationToken
	)
	{
		ImmutableArray<Run>.Builder runs = ImmutableArray.CreateBuilder<Run>();
		int start = 0;

		while (start < siblings.Length)
		{
			cancellationToken.ThrowIfCancellationRequested();

			(RunKind Kind, string? Name) runKey = keys[start];
			int end = start + 1;

			while (
				runKey.Kind != RunKind.None
				&& end < siblings.Length
				&& keys[end] == runKey
				&& !HasBlankLineBefore(siblings, end, gathered)
				&& !IsGapKeptBlank(siblings, boundaryIndex: end, gapOwners, cancellationToken)
			)
			{
				end++;
			}

			runs.Add(new Run(start, end, runKey.Kind != RunKind.None && end - start > 1));
			start = end;
		}

		return runs.ToImmutable();
	}

	public static ImmutableArray<int> ReadSameTypeDeclarationBoundaries(
		ImmutableArray<SyntaxNode> siblings,
		GapOwners gapOwners,
		CancellationToken cancellationToken
	) => ReadBoundaries(ReadSameTypeDeclarationBorders(siblings, gapOwners, gathered: null, cancellationToken));

	/// <summary>Reads the same-type group borders, as if the gathered gaps held no blank line.</summary>
	public static ImmutableArray<RunBorder> ReadSameTypeDeclarationBorders(
		ImmutableArray<SyntaxNode> siblings,
		GapOwners gapOwners,
		ImmutableHashSet<int>? gathered,
		CancellationToken cancellationToken
	)
	{
		if (siblings.Length < 2)
			return [];

		return ReadRunBorders(siblings, getKey: GetTypeKey, includesLoneVariableBoundary: false, gapOwners, gathered, cancellationToken);
	}

	/// <summary>Whether a blank line stands above a sibling, unless the caller reads the gap as gathered.</summary>
	private static bool HasBlankLineBefore(ImmutableArray<SyntaxNode> siblings, int index, ImmutableHashSet<int>? gathered) =>
		(gathered is null || !gathered.Contains(index)) && BlankLineGaps.HasBlankLineBetween(siblings[index - 1], siblings[index]);

	private static (RunKind Kind, string? Name) GetDeclarationRunKey(SyntaxNode node, StanzaOptions options)
	{
		(RunKind Kind, string? Name) variableKey = GetVariableLikeKey(node);

		if (variableKey.Kind != RunKind.None)
			return variableKey;

		if (node is not ExpressionStatementSyntax expressionStatement)
			return default;

		if (FindInvocation(expressionStatement.Expression) is not { } invocation)
			return default;

		string? outerName = FindName(invocation.Expression);

		if (outerName is null)
			return default;

		InvocationExpressionSyntax rootInvocation = GetRootInvocation(invocation);

		string? rootName = FindName(rootInvocation.Expression);
		string? receiverName = FindAssertionRoot(rootInvocation.Expression, options.AssertionRoots);
		string? groupName = options.AssertionRoots.Contains(rootName ?? string.Empty) ? rootName : receiverName ?? outerName;

		return (RunKind.Call, groupName);
	}

	private static (RunKind Kind, string? Name) GetMemberGroupKey(SyntaxNode node)
	{
		if (IsVariableLikeDeclaration(node))
			return GetVariableLikeKey(node);

		RunKind memberKind = GetMemberKind(node);
		return memberKind == RunKind.None ? default : (memberKind, null);
	}

	/// <summary>Splits a member group by its static and readonly modifiers.</summary>
	private static (RunKind Kind, string? Name) GetModifierGroupKey(SyntaxNode node)
	{
		// A top-level statement shares a member's container but is no member, so it joins no run.
		if (node is not MemberDeclarationSyntax member)
			return default;

		(RunKind Kind, string? Name) memberKey = GetMemberGroupKey(node);

		if (memberKey.Kind == RunKind.None)
			return memberKey;

		bool isStatic = StanzaSyntax.HasModifier(member.Modifiers, SyntaxKind.StaticKeyword);
		bool isReadOnly = StanzaSyntax.HasModifier(member.Modifiers, SyntaxKind.ReadOnlyKeyword);

		string? modifiers = (isStatic, isReadOnly) switch
		{
			(true, true) => "static readonly",
			(true, false) => "static",
			(false, true) => "readonly",
			_ => null,
		};

		return (memberKey.Kind, modifiers);
	}

	private static RunKind GetMemberKind(SyntaxNode node)
	{
		return node switch
		{
			ConstructorDeclarationSyntax => RunKind.Constructor,
			DestructorDeclarationSyntax => RunKind.Destructor,
			EventDeclarationSyntax => RunKind.Event,
			IndexerDeclarationSyntax => RunKind.Indexer,
			MethodDeclarationSyntax => RunKind.Method,
			OperatorDeclarationSyntax => RunKind.Operator,
			ConversionOperatorDeclarationSyntax => RunKind.Operator,
			PropertyDeclarationSyntax => RunKind.Property,
			EnumMemberDeclarationSyntax => RunKind.EnumMember,
			DelegateDeclarationSyntax => RunKind.Delegate,
			BaseTypeDeclarationSyntax => RunKind.TypeDeclaration,
			BaseNamespaceDeclarationSyntax => RunKind.Namespace,
			_ => RunKind.None,
		};
	}

	private static (RunKind Kind, string? Name) GetTypeKey(SyntaxNode node)
	{
		TypeSyntax? type = node switch
		{
			LocalDeclarationStatementSyntax localDeclaration => localDeclaration.Declaration.Type,
			FieldDeclarationSyntax fieldDeclaration => fieldDeclaration.Declaration.Type,
			EventFieldDeclarationSyntax eventField => eventField.Declaration.Type,
			_ => null,
		};

		if (type is null)
			return default;

		string typeKey = ReadTypeKey(type);

		// A const declaration never joins a run of non-const ones.
		return GetDeclarationKind(node) == RunKind.Constant ? (RunKind.ConstantType, typeKey) : (RunKind.Type, typeKey);
	}

	/// <summary>Joins the type's tokens with single spaces, so whitespace inside a type cannot split a run.</summary>
	private static string ReadTypeKey(SyntaxNode type)
	{
		StringBuilder builder = new();

		foreach (SyntaxToken token in type.DescendantTokens())
		{
			if (builder.Length > 0)
				builder.Append(' ');

			builder.Append(token.Text);
		}

		return builder.ToString();
	}

	private static (RunKind Kind, string? Name) GetVariableLikeKey(SyntaxNode node)
	{
		RunKind kind = GetDeclarationKind(node);
		return kind == RunKind.None ? default : (kind, null);
	}

	private static RunKind GetDeclarationKind(SyntaxNode node)
	{
		return node switch
		{
			LocalDeclarationStatementSyntax localDeclaration => GetDeclarationKind(localDeclaration.IsConst, localDeclaration.Declaration.Type),

			FieldDeclarationSyntax fieldDeclaration => GetDeclarationKind(
				StanzaSyntax.HasModifier(fieldDeclaration.Modifiers, SyntaxKind.ConstKeyword),
				fieldDeclaration.Declaration.Type
			),

			// An event field is a field for a declaration run, but it can never be const.
			EventFieldDeclarationSyntax eventField => GetDeclarationKind(isConst: false, eventField.Declaration.Type),
			_ => RunKind.None,
		};
	}

	private static RunKind GetDeclarationKind(bool isConst, TypeSyntax type)
	{
		if (isConst)
			return RunKind.Constant;

		if (type is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == "var")
			return RunKind.Variable;

		return RunKind.ExplicitDeclaration;
	}

	/// <summary>Finds the call a statement makes, through a `?.` that guards it.</summary>
	private static InvocationExpressionSyntax? FindInvocation(ExpressionSyntax expression)
	{
		expression = Unwrap(expression);

		while (expression is ConditionalAccessExpressionSyntax conditionalAccess)
			expression = Unwrap(conditionalAccess.WhenNotNull);

		return expression as InvocationExpressionSyntax;
	}

	private static InvocationExpressionSyntax GetRootInvocation(InvocationExpressionSyntax invocation)
	{
		InvocationExpressionSyntax current = invocation;

		while (
			Unwrap(current.Expression) is MemberAccessExpressionSyntax memberAccess
			&& Unwrap(memberAccess.Expression) is InvocationExpressionSyntax nestedInvocation
		)
		{
			current = nestedInvocation;
		}

		return current;
	}

	private static string? FindName(ExpressionSyntax expression)
	{
		expression = Unwrap(expression);

		return expression switch
		{
			SimpleNameSyntax simpleName => simpleName.Identifier.ValueText,
			MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
			MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
			_ => null,
		};
	}

	private static string? FindAssertionRoot(ExpressionSyntax expression, ImmutableHashSet<string> roots)
	{
		if (roots.Count == 0)
			return null;

		expression = Unwrap(expression);

		// A receiver that is a single name has no chain to walk, so none is built for it.
		if (expression is SimpleNameSyntax singleName)
		{
			string single = singleName.Identifier.ValueText;
			return roots.Contains(single) ? single : null;
		}

		// The chain is walked from the called name back to its receiver, so the matching name nearest the call wins.
		while (expression is MemberAccessExpressionSyntax memberAccess)
		{
			string name = memberAccess.Name.Identifier.ValueText;

			if (roots.Contains(name))
				return name;

			expression = Unwrap(memberAccess.Expression);
		}

		return expression is SimpleNameSyntax leaf && roots.Contains(leaf.Identifier.ValueText) ? leaf.Identifier.ValueText : null;
	}

	private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
	{
		while (true)
		{
			switch (expression)
			{
				case AwaitExpressionSyntax awaitExpression:
					expression = awaitExpression.Expression;
					continue;

				case ParenthesizedExpressionSyntax parenthesizedExpression:
					expression = parenthesizedExpression.Expression;
					continue;

				case PostfixUnaryExpressionSyntax postfixUnaryExpression
					when postfixUnaryExpression.IsKind(SyntaxKind.SuppressNullableWarningExpression):
					expression = postfixUnaryExpression.Operand;
					continue;

				default:
					return expression;
			}
		}
	}

	private static bool IsVariableLikeDeclaration(SyntaxNode node) =>
		node is LocalDeclarationStatementSyntax or FieldDeclarationSyntax or EventFieldDeclarationSyntax;

	private enum RunKind
	{
		None,
		Constant,
		Variable,
		ExplicitDeclaration,
		Type,
		ConstantType,
		Constructor,
		Destructor,
		Event,
		Indexer,
		Method,
		Operator,
		Property,
		EnumMember,
		Delegate,
		TypeDeclaration,
		Namespace,
		Call,
	}

	private readonly struct Run(int start, int end, bool isRepeated)
	{
		public int End { get; } = end;
		public bool IsRepeated { get; } = isRepeated;
		public int Start { get; } = start;
	}
}
