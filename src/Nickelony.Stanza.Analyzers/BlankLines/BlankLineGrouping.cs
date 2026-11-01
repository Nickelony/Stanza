using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the borders of the runs of statements, members and declarations a blank line splits.</summary>
internal static class BlankLineGrouping
{
	/// <summary>The rule that owns a run of statements, by what the run holds.</summary>
	public enum RunFamily
	{
		Declaration,
		Invocation,
		Assertion,
		Await,
		Subscription,
		Assignment,
	}

	/// <summary>How the key of a rule reads the gap between two siblings, whatever blank line stands there.</summary>
	/// <remarks>
	/// <c>Unnamed</c> is a key that names a sibling of the gap no run, so the rule has no say there.
	/// </remarks>
	public enum GapReading
	{
		Unnamed,
		Together,
		Apart,
	}

	/// <summary>A gap read by one key, with the family of the run each sibling belongs to.</summary>
	public readonly record struct GapRead(GapReading Reading, RunFamily? Previous = null, RunFamily? Next = null);

	/// <summary>The kind of what an assignment writes, which splits a run where the setting asks.</summary>
	/// <remarks>
	/// <c>None</c> is no kind and joins any neighbor, and <c>Several</c>, a mixed deconstruction, joins none.
	/// </remarks>
	public enum AssignmentKind
	{
		None,
		Field,
		Property,
		Local,
		Element,
		Several,
	}

	/// <summary>What an assignment writes: its kind, and the root it writes through.</summary>
	/// <remarks>
	/// The root is the symbol a target starts from, as <c>_a</c> is for <c>_a.B</c>, and none where it does not bind.
	/// </remarks>
	public readonly record struct AssignmentTarget(AssignmentKind Kind, ISymbol? Root = null);

	/// <summary>The key name of a deconstruction of several kinds, which no run continues through.</summary>
	private const string SeveralTargets = nameof(AssignmentKind.Several);

	/// <summary>A border of a repeated run: its boundary, and the run's start and its end, one past the last.</summary>
	/// <remarks>
	/// A run of statements is tagged with its family, and a run of members or types has none.
	/// </remarks>
	public readonly record struct RunBorder(int Boundary, int RunStart, int RunEnd, RunFamily? Family = null);

	/// <summary>The gaps a read gathers and its siblings from <c>Start</c> to one before <c>End</c>.</summary>
	/// <remarks>
	/// A stretch short of the container ends where no run crosses, and a written gap reads as blank.
	/// </remarks>
	public readonly record struct GatheredStretch(
		ImmutableHashSet<int> Boundaries,
		int Start,
		int End,
		bool ContinuesAbove = false,
		ImmutableHashSet<int>? Written = null
	);

	/// <summary>What puts a sibling in a run: its kind, a name that splits the kind, and an assignment root.</summary>
	private readonly record struct RunKey(RunKind Kind, string? Name = null, ISymbol? Root = null);

	/// <summary>The keys of a stretch of siblings, read by a sibling's index in the container.</summary>
	private readonly record struct RunKeys(RunKey[] Keys, int Start)
	{
		public RunKey this[int index] => Keys[index - Start];
	}

	/// <summary>Reads the run borders of every family, as if the gathered gaps held no blank line.</summary>
	/// <remarks>
	/// A lone declaration above a run is set apart by the family of that run, and a target reader splits assignments.
	/// </remarks>
	public static ImmutableArray<RunBorder> ReadRunBorders(
		ImmutableArray<SyntaxNode> siblings,
		StanzaOptions options,
		GapOwners gapOwners,
		GatheredStretch? gathered,
		Func<SyntaxNode, AssignmentTarget>? readTarget,
		CancellationToken cancellationToken
	)
	{
		if (siblings.Length < 2)
			return [];

		return WalkBorders(
			siblings,
			getKey: BuildStatementKeyReader(options, gapOwners, readTarget),
			mode: WalkMode.LoneVariableBoundary | WalkMode.TagsFamily,
			gapOwners,
			gathered,
			cancellationToken
		);
	}

	/// <summary>Reads the run borders of the siblings the caller names as subscriptions.</summary>
	/// <remarks>
	/// The caller decides what a subscription is, so the read that needs a semantic model stays out of this class.
	/// </remarks>
	public static ImmutableArray<RunBorder> ReadSubscriptionRunBorders(
		ImmutableArray<SyntaxNode> siblings,
		StanzaOptions options,
		Func<SyntaxNode, bool> isSubscription,
		GapOwners gapOwners,
		GatheredStretch? gathered,
		CancellationToken cancellationToken
	)
	{
		if (siblings.Length < 2)
			return [];

		// The other statements keep their own keys, so a declaration that ends a run of declarations is not a lone one.
		ImmutableArray<RunBorder> borders = WalkBorders(
			siblings,
			getKey: BuildSubscriptionKeyReader(options, gapOwners, isSubscription),
			mode: WalkMode.LoneVariableBoundary | WalkMode.TagsFamily,
			gapOwners,
			gathered,
			cancellationToken
		);

		return [.. borders.Where(static border => border.Family == RunFamily.Subscription)];
	}

	/// <summary>Reads the member group borders, as if the gathered gaps held no blank line.</summary>
	public static ImmutableArray<RunBorder> ReadMemberGroupBorders(
		ImmutableArray<SyntaxNode> siblings,
		GapOwners gapOwners,
		GatheredStretch? gathered,
		CancellationToken cancellationToken
	)
	{
		if (siblings.Length < 2)
			return [];

		return WalkBorders(siblings, getKey: GetMemberGroupKey, mode: WalkMode.LoneVariableBoundary, gapOwners, gathered, cancellationToken);
	}

	/// <summary>Reads the modifier group borders, as if the gathered gaps held no blank line.</summary>
	public static ImmutableArray<RunBorder> ReadModifierGroupBorders(
		ImmutableArray<SyntaxNode> siblings,
		GapOwners gapOwners,
		GatheredStretch? gathered,
		CancellationToken cancellationToken
	)
	{
		if (siblings.Length < 2)
			return [];

		return WalkBorders(siblings, getKey: GetModifierGroupKey, mode: WalkMode.None, gapOwners, gathered, cancellationToken);
	}

	/// <summary>Reads the keys of the stretch and the two siblings above it, which its first run needs.</summary>
	private static RunKeys ReadRunKeys(
		ImmutableArray<SyntaxNode> siblings,
		Func<SyntaxNode, RunKey> getKey,
		GatheredStretch? gathered,
		CancellationToken cancellationToken
	)
	{
		int stretchStart = gathered?.Start ?? 0;
		int stretchEnd = gathered?.End ?? siblings.Length;

		int keysStart = Math.Max(0, stretchStart - 2);
		var readKeys = new RunKey[stretchEnd - keysStart];

		for (int index = keysStart; index < stretchEnd; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			readKeys[index - keysStart] = getKey(siblings[index]);
		}

		return new RunKeys(readKeys, keysStart);
	}

	/// <summary>Walks the runs of the keys and reads the borders of the repeated ones, as the mode asks.</summary>
	private static ImmutableArray<RunBorder> WalkBorders(
		ImmutableArray<SyntaxNode> siblings,
		Func<SyntaxNode, RunKey> getKey,
		WalkMode mode,
		GapOwners gapOwners,
		GatheredStretch? gathered,
		CancellationToken cancellationToken
	)
	{
		RunKeys keys = ReadRunKeys(siblings, getKey, gathered, cancellationToken);

		ImmutableArray<Run> runs = WalkRuns(keys, siblings, gapOwners, gathered, cancellationToken);
		ImmutableArray<RunBorder>.Builder borders = ImmutableArray.CreateBuilder<RunBorder>();

		for (int index = 0; index < runs.Length; index++)
		{
			Run run = runs[index];

			if (!run.IsRepeated)
				continue;

			RunFamily? family = (mode & WalkMode.TagsFamily) != 0 ? FindFamily(run.Kind) : null;

			bool followsLoneDeclaration =
				(mode & WalkMode.LoneVariableBoundary) != 0
				&& !run.ContinuesAbove
				&& run.Start >= 2
				&& IsVariableLikeDeclaration(siblings[run.Start - 1]);

			// A declaration on its own is set apart only while it stands directly above the run.
			// A caller that gathers some gaps keeps that boundary once a blank line stands below the declaration too,
			// since the rule writes that blank line and a fix order that wrote it first would otherwise judge the gap alone.
			// With no gap gathered the text is read as it stands, so a blank line below the declaration keeps the boundary from being asked.
			if (
				followsLoneDeclaration
				&& !IsRepeatedAboveLoneDeclaration(runs, index, keys, siblings, gapOwners, gathered, cancellationToken)
				&& (gathered is { Boundaries.IsEmpty: false } || !HasBlankLineBefore(siblings, run.Start, gathered: null))
				&& !IsCommentedSiblingGap(siblings, boundary: run.Start, commentedSiblingOwnerId: gapOwners.CommentedSiblingId, cancellationToken)
			)
			{
				borders.Add(new RunBorder(Boundary: run.Start - 1, RunStart: run.Start, RunEnd: run.End, Family: family));
			}

			if (run.Start > 0 && !run.ContinuesAbove)
				borders.Add(new RunBorder(Boundary: run.Start, RunStart: run.Start, RunEnd: run.End, Family: family));

			if (run.End < siblings.Length)
				borders.Add(new RunBorder(Boundary: run.End, RunStart: run.Start, RunEnd: run.End, Family: family));
		}

		return borders.ToImmutable();
	}

	/// <summary>Reads the boundaries of the borders one walk read, in order and without repeats.</summary>
	public static ImmutableArray<int> ReadBorderBoundaries(ImmutableArray<RunBorder> borders)
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

	/// <summary>Whether ZA0001 keeps the gap blank, so it ends a run even before its blank line is written.</summary>
	/// <remarks>
	/// A pragma that silences ZA0001 over the gap leaves it inside the run, as <c>none</c> does.
	/// </remarks>
	private static bool IsMultiLineGap(
		ImmutableArray<SyntaxNode> siblings,
		int boundary,
		string? multiLineGapOwnerId,
		CancellationToken cancellationToken
	)
	{
		if (multiLineGapOwnerId is null || (!StanzaSyntax.IsMultiLine(siblings[boundary - 1]) && !StanzaSyntax.IsMultiLine(siblings[boundary])))
			return false;

		return !StanzaPragmas.IsSilencedByPragma(
			syntaxTree: siblings[boundary].SyntaxTree,
			ruleId: multiLineGapOwnerId,
			position: siblings[boundary].SpanStart,
			cancellationToken
		);
	}

	/// <summary>Whether ZA0012 keeps the gap blank, so it ends a run even before its blank line is written.</summary>
	/// <remarks>
	/// A pragma that silences ZA0012 over the gap leaves it inside the run, as <c>none</c> does.
	/// </remarks>
	private static bool IsCommentedSiblingGap(
		ImmutableArray<SyntaxNode> siblings,
		int boundary,
		string? commentedSiblingOwnerId,
		CancellationToken cancellationToken
	)
	{
		if (commentedSiblingOwnerId is null || !BlankLineGaps.IsKeptBlankAboveComment(siblings[boundary - 1], siblings[boundary]))
			return false;

		return !StanzaPragmas.IsSilencedByPragma(
			syntaxTree: siblings[boundary].SyntaxTree,
			ruleId: commentedSiblingOwnerId,
			position: siblings[boundary].SpanStart,
			cancellationToken
		);
	}

	/// <summary>Whether an owning rule keeps the gap blank, so a run ends there.</summary>
	public static bool IsGapKeptBlank(ImmutableArray<SyntaxNode> siblings, int boundary, GapOwners gapOwners, CancellationToken cancellationToken)
	{
		return gapOwners.Owns(boundary)
			|| IsMultiLineGap(siblings, boundary, multiLineGapOwnerId: gapOwners.MultiLineId, cancellationToken)
			|| BlankLineDirectives.IsRegionMarkerGap(siblings[boundary], gapOwners.RegionMarkerId, cancellationToken)
			|| BlankLineDirectives.IsConditionalBlockGap(siblings[boundary], gapOwners.ConditionalBlockId, cancellationToken)
			|| IsCommentedSiblingGap(siblings, boundary, commentedSiblingOwnerId: gapOwners.CommentedSiblingId, cancellationToken);
	}

	private static ImmutableArray<Run> WalkRuns(
		RunKeys keys,
		ImmutableArray<SyntaxNode> siblings,
		GapOwners gapOwners,
		GatheredStretch? gathered,
		CancellationToken cancellationToken
	)
	{
		ImmutableArray<Run>.Builder runs = ImmutableArray.CreateBuilder<Run>();

		int stretchEnd = gathered?.End ?? siblings.Length;
		int start = gathered?.Start ?? 0;

		// A stretch that continues above starts inside the runs that cross its first gap, and those keep no border at their start.
		// Such a run reaches the sibling above, so it holds more than one.
		bool continuesAbove =
			gathered is { ContinuesAbove: true, Start: > 0 }
			&& ContinuesRun(keys[start - 1], keys[start])
			&& IsOpenGap(siblings, boundary: start, gapOwners, gathered, cancellationToken);

		while (start < stretchEnd)
		{
			cancellationToken.ThrowIfCancellationRequested();

			RunKey runKey = keys[start];
			int end = start + 1;

			while (
				end < stretchEnd
				&& ContinuesRun(keys[end - 1], keys[end])
				&& IsOpenGap(siblings, boundary: end, gapOwners, gathered, cancellationToken)
			)
			{
				end++;
			}

			runs.Add(
				new Run(
					Start: start,
					End: end,
					Kind: runKey.Kind,
					IsRepeated: runKey.Kind != RunKind.None && (end - start > 1 || continuesAbove),
					ContinuesAbove: continuesAbove
				)
			);

			start = end;
			continuesAbove = false;
		}

		return runs.ToImmutable();
	}

	/// <summary>Whether a sibling of this key can join the run of the sibling above it.</summary>
	/// <remarks>
	/// An assignment of no kind joins either neighbor, and a deconstruction of several kinds joins neither.
	/// </remarks>
	private static bool ContinuesRun(RunKey previousKey, RunKey key)
	{
		if (previousKey.Kind == RunKind.None || key.Kind != previousKey.Kind)
			return false;

		if (key.Kind != RunKind.Assignment)
			return key.Name == previousKey.Name;

		if (previousKey.Name == SeveralTargets || key.Name == SeveralTargets)
			return false;

		if (previousKey.Name is null || key.Name is null || key.Name == previousKey.Name)
			return true;

		// Two assignments through one root join whatever their kinds, as `_a = Read();` and `_a.B = 1;` do.
		return key.Root is not null && SymbolEqualityComparer.Default.Equals(key.Root, previousKey.Root);
	}

	/// <summary>Whether no blank line stands above the sibling at the boundary and no rule keeps one there.</summary>
	private static bool IsOpenGap(
		ImmutableArray<SyntaxNode> siblings,
		int boundary,
		GapOwners gapOwners,
		GatheredStretch? gathered,
		CancellationToken cancellationToken
	) => !HasBlankLineBefore(siblings, boundary, gathered) && !IsGapKeptBlank(siblings, boundary, gapOwners, cancellationToken);

	/// <summary>Reads the gap by the keys of the declaration, call, assertion, await and assignment runs.</summary>
	/// <remarks>
	/// A target reader splits the assignments, and every gap is read as if its blank line were not there.
	/// </remarks>
	public static GapRead ReadStatementGap(
		ImmutableArray<SyntaxNode> siblings,
		int boundary,
		StanzaOptions options,
		GapOwners gapOwners,
		Func<SyntaxNode, AssignmentTarget>? readTarget,
		CancellationToken cancellationToken
	)
	{
		return ReadGap(getKey: BuildStatementKeyReader(options, gapOwners, readTarget), siblings, boundary, gapOwners, cancellationToken);
	}

	/// <summary>Reads the gap by the key of the subscription runs, where the caller names the subscriptions.</summary>
	public static GapRead ReadSubscriptionGap(
		ImmutableArray<SyntaxNode> siblings,
		int boundary,
		StanzaOptions options,
		GapOwners gapOwners,
		Func<SyntaxNode, bool> isSubscription,
		CancellationToken cancellationToken
	)
	{
		return ReadGap(getKey: BuildSubscriptionKeyReader(options, gapOwners, isSubscription), siblings, boundary, gapOwners, cancellationToken);
	}

	/// <summary>Reads the gap by the key of the runs of declarations of one type.</summary>
	public static GapRead ReadSameTypeGap(
		ImmutableArray<SyntaxNode> siblings,
		int boundary,
		GapOwners gapOwners,
		CancellationToken cancellationToken
	) => ReadGap(getKey: GetTypeKey, siblings, boundary, gapOwners, cancellationToken);

	/// <summary>Reads the gap by the key of the runs of members of one kind.</summary>
	public static GapRead ReadMemberGroupGap(
		ImmutableArray<SyntaxNode> siblings,
		int boundary,
		GapOwners gapOwners,
		CancellationToken cancellationToken
	) => ReadGap(getKey: GetMemberGroupKey, siblings, boundary, gapOwners, cancellationToken);

	/// <summary>Reads the gap by the key of the runs of members that share their modifiers.</summary>
	public static GapRead ReadModifierGroupGap(
		ImmutableArray<SyntaxNode> siblings,
		int boundary,
		GapOwners gapOwners,
		CancellationToken cancellationToken
	) => ReadGap(getKey: GetModifierGroupKey, siblings, boundary, gapOwners, cancellationToken);

	/// <summary>Reads whether the key holds both siblings of a gap in one run, unless a rule keeps it blank.</summary>
	private static GapRead ReadGap(
		Func<SyntaxNode, RunKey> getKey,
		ImmutableArray<SyntaxNode> siblings,
		int boundary,
		GapOwners gapOwners,
		CancellationToken cancellationToken
	)
	{
		RunKey previous = getKey(siblings[boundary - 1]);
		RunKey next = getKey(siblings[boundary]);

		if (previous.Kind == RunKind.None || next.Kind == RunKind.None)
			return new GapRead(GapReading.Unnamed);

		bool together = ContinuesRun(previous, next) && !IsGapKeptBlank(siblings, boundary, gapOwners, cancellationToken);

		return new GapRead(together ? GapReading.Together : GapReading.Apart, FindFamily(previous.Kind), FindFamily(next.Kind));
	}

	/// <summary>Whether the declaration above the run at the index ends a repeated run, so it is no lone one.</summary>
	/// <remarks>
	/// A gap written above the declaration reads open, or its border would be asked for only because it is written.
	/// </remarks>
	private static bool IsRepeatedAboveLoneDeclaration(
		ImmutableArray<Run> runs,
		int index,
		RunKeys keys,
		ImmutableArray<SyntaxNode> siblings,
		GapOwners gapOwners,
		GatheredStretch? gathered,
		CancellationToken cancellationToken
	)
	{
		int above = runs[index].Start - 1;

		if (gathered is { Written: { } written } stretch && written.Contains(above))
		{
			GatheredStretch unwritten = stretch with { Written = written.Remove(above) };
			return IsRepeatedRunBefore(keys, siblings, index: runs[index].Start, gapOwners, gathered: unwritten, cancellationToken);
		}

		return index > 0
			? runs[index - 1].IsRepeated
			: IsRepeatedRunBefore(keys, siblings, index: runs[index].Start, gapOwners, gathered, cancellationToken);
	}

	/// <summary>Whether the run that ends where the one at the index starts holds more than one sibling.</summary>
	private static bool IsRepeatedRunBefore(
		RunKeys keys,
		ImmutableArray<SyntaxNode> siblings,
		int index,
		GapOwners gapOwners,
		GatheredStretch? gathered,
		CancellationToken cancellationToken
	) => ContinuesRun(keys[index - 2], keys[index - 1]) && IsOpenGap(siblings, boundary: index - 1, gapOwners, gathered, cancellationToken);

	/// <summary>Reads the same-type group borders, as if the gathered gaps held no blank line.</summary>
	public static ImmutableArray<RunBorder> ReadSameTypeDeclarationBorders(
		ImmutableArray<SyntaxNode> siblings,
		GapOwners gapOwners,
		GatheredStretch? gathered,
		CancellationToken cancellationToken
	)
	{
		if (siblings.Length < 2)
			return [];

		return WalkBorders(siblings, getKey: GetTypeKey, mode: WalkMode.None, gapOwners, gathered, cancellationToken);
	}

	/// <summary>Reads the stretch of siblings around a gathered gap whose runs the gap can change.</summary>
	/// <remarks>
	/// It spans the runs beside the gap and the one after, and ends where no kind of run crosses.
	/// </remarks>
	public static GatheredStretch ReadStretchAround(
		ImmutableArray<SyntaxNode> siblings,
		StanzaOptions options,
		GapOwners gapOwners,
		ImmutableHashSet<int> gathered,
		int boundary,
		CancellationToken cancellationToken
	)
	{
		GatheredStretch read = new(gathered, Start: 0, End: siblings.Length);

		bool IsCrossed(int candidate)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Assignments are read as one kind here, so a stretch holds every run a split of them can make and binds no symbol.
			return IsOpenGap(siblings, boundary: candidate, gapOwners, gathered: read, cancellationToken)
				&& (
					ContinuesRun(
						GetDeclarationRunKey(siblings[candidate - 1], options, gapOwners.AwaitRuleEnabled),
						GetDeclarationRunKey(siblings[candidate], options, gapOwners.AwaitRuleEnabled)
					)
					|| ContinuesRun(GetTypeKey(siblings[candidate - 1]), GetTypeKey(siblings[candidate]))
					|| ContinuesRun(GetMemberGroupKey(siblings[candidate - 1]), GetMemberGroupKey(siblings[candidate]))
					|| ContinuesRun(GetModifierGroupKey(siblings[candidate - 1]), GetModifierGroupKey(siblings[candidate]))
				);
		}

		int start = boundary - 1;

		// A gathered gap directly above is inside the runs that cross it, and the read that gathered it judged where they start.
		// So the stretch begins at that gap, rather than at the start of a run that grows with each retry.
		bool continuesAbove = start > 0 && gathered.Contains(start);

		while (!continuesAbove && start > 0 && IsCrossed(start))
			start--;

		// The sibling below the gap's own can start the run that a lone declaration at the gap's own sets apart, so the stretch takes it in.
		int end = Math.Min(boundary + 2, siblings.Length);

		while (end < siblings.Length && IsCrossed(end))
			end++;

		return read with
		{
			Start = start,
			End = end,
			ContinuesAbove = continuesAbove,
		};
	}

	/// <summary>Whether a blank line stands or is written above a sibling, unless the gap is gathered.</summary>
	private static bool HasBlankLineBefore(ImmutableArray<SyntaxNode> siblings, int boundary, GatheredStretch? gathered)
	{
		if (gathered is not { } stretch)
			return BlankLineGaps.HasBlankLineBetween(siblings[boundary - 1], siblings[boundary]);

		return stretch.Written?.Contains(boundary) == true
			|| (!stretch.Boundaries.Contains(boundary) && BlankLineGaps.HasBlankLineBetween(siblings[boundary - 1], siblings[boundary]));
	}

	/// <summary>Builds the reader of the keys that form the runs of statements.</summary>
	private static Func<SyntaxNode, RunKey> BuildStatementKeyReader(
		StanzaOptions options,
		GapOwners gapOwners,
		Func<SyntaxNode, AssignmentTarget>? readTarget
	) => node => GetSplitRunKey(node, options, awaitRuleEnabled: gapOwners.AwaitRuleEnabled, readTarget);

	/// <summary>Builds the reader of the keys that form the runs of subscriptions.</summary>
	private static Func<SyntaxNode, RunKey> BuildSubscriptionKeyReader(
		StanzaOptions options,
		GapOwners gapOwners,
		Func<SyntaxNode, bool> isSubscription
	) => node => isSubscription(node) ? new RunKey(RunKind.Subscription) : GetDeclarationRunKey(node, options, gapOwners.AwaitRuleEnabled);

	/// <summary>Reads the run kind of a statement, where a target reader names what an assignment writes.</summary>
	/// <remarks>
	/// Without a reader every assignment is one kind, as the setting <c>mixed</c> asks.
	/// </remarks>
	private static RunKey GetSplitRunKey(
		SyntaxNode node,
		StanzaOptions options,
		bool awaitRuleEnabled,
		Func<SyntaxNode, AssignmentTarget>? readTarget
	)
	{
		RunKey key = GetDeclarationRunKey(node, options, awaitRuleEnabled);

		if (key.Kind != RunKind.Assignment || readTarget is null)
			return key;

		AssignmentTarget target = readTarget(node);
		return new RunKey(RunKind.Assignment, Name: ReadTargetName(target.Kind), Root: target.Root);
	}

	private static string? ReadTargetName(AssignmentKind kind)
	{
		return kind switch
		{
			AssignmentKind.Field => nameof(AssignmentKind.Field),
			AssignmentKind.Property => nameof(AssignmentKind.Property),
			AssignmentKind.Local => nameof(AssignmentKind.Local),
			AssignmentKind.Element => nameof(AssignmentKind.Element),
			AssignmentKind.Several => SeveralTargets,
			_ => null,
		};
	}

	/// <summary>Reads the run kind of a statement, where an await is a kind of its own while ZA0006 is on.</summary>
	private static RunKey GetDeclarationRunKey(SyntaxNode node, StanzaOptions options, bool awaitRuleEnabled)
	{
		RunKey variableKey = GetVariableLikeKey(node);

		if (variableKey.Kind != RunKind.None)
			return variableKey;

		if (node is not ExpressionStatementSyntax expressionStatement)
			return default;

		RunKey expressionKey = GetExpressionRunKey(expressionStatement.Expression, options);

		// An awaited assertion stays an assertion, since checking a result is the more specific intent.
		return awaitRuleEnabled && expressionKey.Kind != RunKind.Assertion && BlankLineExpressions.IsAwaitStatement(expressionStatement.Expression)
			? new RunKey(RunKind.Await)
			: expressionKey;
	}

	private static RunKey GetExpressionRunKey(ExpressionSyntax expression, StanzaOptions options)
	{
		if (BlankLineExpressions.FindInvocation(expression) is not { } invocation)
			return BlankLineExpressions.FindAssignment(expression) is not null ? new RunKey(RunKind.Assignment) : default;

		// A chain is named by the call it starts from, so what it goes on to do with the result, such as `.ConfigureAwait(false)`, names no run.
		InvocationExpressionSyntax rootInvocation = BlankLineExpressions.GetRootInvocation(invocation);

		string? rootName = BlankLineExpressions.FindName(rootInvocation.Expression);

		if (rootName is null)
			return default;

		string? receiverName = BlankLineExpressions.FindRoot(rootInvocation.Expression, options.AssertionRoots);

		return receiverName is null ? new RunKey(RunKind.Call, Name: rootName) : new RunKey(RunKind.Assertion, Name: receiverName);
	}

	/// <summary>Finds the family that owns a run of this kind, or none for a kind of member or type.</summary>
	private static RunFamily? FindFamily(RunKind kind)
	{
		return kind switch
		{
			RunKind.Constant or RunKind.Variable or RunKind.ExplicitDeclaration or RunKind.UsingDeclaration => RunFamily.Declaration,
			RunKind.Call => RunFamily.Invocation,
			RunKind.Assertion => RunFamily.Assertion,
			RunKind.Await => RunFamily.Await,
			RunKind.Subscription => RunFamily.Subscription,
			RunKind.Assignment => RunFamily.Assignment,
			_ => null,
		};
	}

	private static RunKey GetMemberGroupKey(SyntaxNode node)
	{
		if (IsVariableLikeDeclaration(node))
			return GetVariableLikeKey(node);

		RunKind memberKind = GetMemberKind(node);
		return memberKind == RunKind.None ? default : new RunKey(memberKind);
	}

	/// <summary>Splits a member group by its access, static and readonly modifiers.</summary>
	private static RunKey GetModifierGroupKey(SyntaxNode node)
	{
		// A top-level statement shares a member's container but is no member, so it joins no run.
		if (node is not MemberDeclarationSyntax member)
			return default;

		RunKey memberKey = GetMemberGroupKey(node);

		if (memberKey.Kind == RunKind.None)
			return memberKey;

		bool isStatic = member.Modifiers.Any(SyntaxKind.StaticKeyword);
		bool isReadOnly = member.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);

		string? modifiers = (isStatic, isReadOnly) switch
		{
			(true, true) => "static readonly",
			(true, false) => "static",
			(false, true) => "readonly",
			_ => null,
		};

		string? access = FindAccessLevel(member);

		return new RunKey(
			memberKey.Kind,

			Name: access is null ? modifiers
				: modifiers is null ? access
				: $"{access} {modifiers}"
		);
	}

	/// <summary>Finds the access of a member, or the default its container gives; an enum member has none.</summary>
	private static string? FindAccessLevel(MemberDeclarationSyntax member)
	{
		if (member is EnumMemberDeclarationSyntax)
			return null;

		return ReadWrittenAccess(member)
			?? FindAccessOfOtherPart(member)
			?? member.Parent switch
			{
				InterfaceDeclarationSyntax => "public",
				TypeDeclarationSyntax => "private",
				_ => "internal",
			};
	}

	private static string? ReadWrittenAccess(MemberDeclarationSyntax member)
	{
		bool isPublic = member.Modifiers.Any(SyntaxKind.PublicKeyword);
		bool isPrivate = member.Modifiers.Any(SyntaxKind.PrivateKeyword);
		bool isProtected = member.Modifiers.Any(SyntaxKind.ProtectedKeyword);
		bool isInternal = member.Modifiers.Any(SyntaxKind.InternalKeyword);

		return (isPublic, isPrivate, isProtected, isInternal) switch
		{
			(true, _, _, _) => "public",
			(_, true, true, _) => "private protected",
			(_, true, _, _) => "private",
			(_, _, true, true) => "protected internal",
			(_, _, true, _) => "protected",
			(_, _, _, true) => "internal",
			_ => null,
		};
	}

	/// <summary>Finds the access another part of a partial type writes, as all parts share one access.</summary>
	/// <remarks>
	/// Only a part beside it is read, as a part elsewhere may write one the tree does not show.
	/// </remarks>
	private static string? FindAccessOfOtherPart(MemberDeclarationSyntax member)
	{
		if (member is not TypeDeclarationSyntax part || !part.Modifiers.Any(SyntaxKind.PartialKeyword))
			return null;

		if (part.Parent is not { } container)
			return null;

		// One pass over the container reads the access of every partial type in it, so a part does not scan its siblings.
		Dictionary<(SyntaxKind Kind, string Name, int Arity), string> accesses = s_partAccesses.GetOrCompute(container, ReadPartAccesses);

		return accesses.TryGetValue(ReadPartKey(part), out string? access) ? access : null;
	}

	/// <summary>Reads the first access written for each partial type of a container.</summary>
	private static Dictionary<(SyntaxKind Kind, string Name, int Arity), string> ReadPartAccesses(SyntaxNode container)
	{
		Dictionary<(SyntaxKind Kind, string Name, int Arity), string> accesses = [];

		foreach (SyntaxNode child in container.ChildNodes())
		{
			if (child is not TypeDeclarationSyntax other || !other.Modifiers.Any(SyntaxKind.PartialKeyword))
				continue;

			if (ReadWrittenAccess(other) is { } access && !accesses.ContainsKey(ReadPartKey(other)))
				accesses.Add(ReadPartKey(other), access);
		}

		return accesses;
	}

	private static (SyntaxKind Kind, string Name, int Arity) ReadPartKey(TypeDeclarationSyntax type) =>
		(type.Kind(), type.Identifier.ValueText, type.TypeParameterList?.Parameters.Count ?? 0);

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

	private static RunKey GetTypeKey(SyntaxNode node)
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
		RunKind kind = GetDeclarationKind(node) == RunKind.Constant ? RunKind.ConstantType : RunKind.Type;
		return new RunKey(kind, Name: typeKey);
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

	private static RunKey GetVariableLikeKey(SyntaxNode node)
	{
		RunKind kind = GetDeclarationKind(node);
		return kind == RunKind.None ? default : new RunKey(kind);
	}

	private static RunKind GetDeclarationKind(SyntaxNode node)
	{
		return node switch
		{
			// Taking a resource is a different job from declaring a value, so a `using` declaration is a kind of its own.
			LocalDeclarationStatementSyntax localDeclaration => localDeclaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)
				? RunKind.UsingDeclaration
				: GetDeclarationKind(localDeclaration.IsConst, localDeclaration.Declaration.Type),

			FieldDeclarationSyntax fieldDeclaration => GetDeclarationKind(
				fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword),
				fieldDeclaration.Declaration.Type
			),

			// An event field is a field for a declaration run, but it can never be const.
			EventFieldDeclarationSyntax eventField => GetDeclarationKind(isConst: false, eventField.Declaration.Type),

			// A deconstruction such as `var (a, b) = Read();` declares in the syntax of an assignment.
			ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { Left: DeclarationExpressionSyntax deconstruction } } =>
				GetDeclarationKind(isConst: false, deconstruction.Type),

			// So does `(int a, int b) = Read();`, while a tuple that writes an existing name, as `(int a, b)` does, assigns.
			ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { Left: TupleExpressionSyntax tuple } } => GetDeclarationKind(tuple),
			_ => RunKind.None,
		};
	}

	/// <summary>Reads the kind a tuple declares, which is none unless every element declares.</summary>
	private static RunKind GetDeclarationKind(TupleExpressionSyntax tuple)
	{
		RunKind kind = RunKind.Variable;

		foreach (ArgumentSyntax argument in tuple.Arguments)
		{
			RunKind element = argument.Expression switch
			{
				DeclarationExpressionSyntax declaration => GetDeclarationKind(isConst: false, declaration.Type),
				TupleExpressionSyntax nested => GetDeclarationKind(nested),
				_ => RunKind.None,
			};

			if (element == RunKind.None)
				return RunKind.None;

			// One explicit type makes the whole a declaration with types, as `var` declares only where every element does.
			if (element == RunKind.ExplicitDeclaration)
				kind = RunKind.ExplicitDeclaration;
		}

		return kind;
	}

	private static RunKind GetDeclarationKind(bool isConst, TypeSyntax type)
	{
		if (isConst)
			return RunKind.Constant;

		if (type is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == "var")
			return RunKind.Variable;

		return RunKind.ExplicitDeclaration;
	}

	private static bool IsVariableLikeDeclaration(SyntaxNode node) => GetDeclarationKind(node) != RunKind.None;

	private enum RunKind
	{
		None,
		Constant,
		Variable,
		ExplicitDeclaration,
		UsingDeclaration,
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
		Assertion,
		Await,
		Subscription,
		Assignment,
	}

	private static readonly WeakCache<SyntaxNode, Dictionary<(SyntaxKind Kind, string Name, int Arity), string>> s_partAccesses = new();

	/// <summary>A stretch of siblings one key holds, from <c>Start</c> to one before <c>End</c>.</summary>
	private readonly record struct Run(int Start, int End, RunKind Kind, bool IsRepeated, bool ContinuesAbove);

	/// <summary>What a walk of borders adds beside the borders of its runs.</summary>
	[Flags]
	private enum WalkMode
	{
		None = 0,

		/// <summary>A lone declaration above a run is set apart by it.</summary>
		LoneVariableBoundary = 1,

		/// <summary>A border carries the family of its run.</summary>
		TagsFamily = 2,
	}
}
