using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the pairs of statements that the keep pair rules hold together.</summary>
/// <remarks>
/// Each rule claims the gap of its pair, so a separating rule neither asks for a blank line there nor writes one.
/// </remarks>
internal static class BlankLineKeptPairs
{
	/// <summary>
	/// The rules that keep a pair together, in ID order, which win the gap of their pair over the separating rules.
	/// </summary>
	private static readonly ImmutableArray<StanzaRule> s_rules =
	[
		StanzaRules.KeepGuardClausesTogether,
		StanzaRules.KeepGuardWithBinding,
		StanzaRules.KeepJumpWithBinding,
		StanzaRules.KeepNarrowingAssertionWithBinding,
		StanzaRules.KeepDependentBindingsTogether,
		StanzaRules.KeepInitializationWithCalls,
		StanzaRules.KeepAccumulatorWithLoop,
		StanzaRules.KeepReceiverGroupTogether,
	];

	/// <summary>The keepers that anchor on separating boundaries, each ignored by its own read of the runs.</summary>
	private static readonly ImmutableArray<StanzaRule> s_anchorKeepers = [StanzaRules.KeepJumpWithBinding, StanzaRules.KeepInitializationWithCalls];

	public static bool HasEnabledRule(ContainerContext container) => container.RunOwnership is not null;

	/// <summary>Whether the rule keeps a pair or a group together, so a read of runs joins its gaps.</summary>
	public static bool IsKeeper(StanzaRule rule) => s_rules.Contains(rule);

	public static bool HasEnabledRule(SyntaxNodeAnalysisContext context, StanzaOptions options)
	{
		return s_rules.Any(rule =>
			rule.Kinds.Contains(context.Node.Kind())
			&& (rule.Descriptor != DiagnosticDescriptors.KeepReceiverGroupTogether || !options.ReceiverRoots.IsEmpty)
			&& !StanzaRuleState.IsRuleDisabled(context, options, rule)
		);
	}

	/// <summary>Whether a keep rule that is on there claims the gap above the unit.</summary>
	public static bool IsGapClaimed(ContainerContext container, int boundary) => IsGapClaimedByAnother(container, boundary, askingRule: null);

	/// <summary>
	/// Whether a keep rule that is on there claims the gap above the unit, so the asking rule defers.
	/// </summary>
	public static bool IsGapClaimedByAnother(ContainerContext container, int boundary, StanzaRule? askingRule)
	{
		foreach (StanzaRule rule in s_rules)
		{
			if (container.ReadsRuns && (container.IgnoredAnchorKeepers & ReadAnchorBit(rule)) != 0)
				continue;

			if (rule.Descriptor == askingRule?.Descriptor || !rule.Kinds.Contains(container.Kind))
				continue;

			if (StanzaRuleState.IsRuleDisabled(container, container.Options, rule))
				continue;

			if (
				Claims(container, boundary, rule)
				&& !StanzaRuleState.IsOffAt(scope: container, options: container.Options, rule, position: container.Siblings[boundary].SpanStart)
			)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Whether the rule's pair stands at the boundary, with nothing but whitespace in its gap.</summary>
	public static bool Claims(ContainerContext container, int boundary, StanzaRule rule)
	{
		SyntaxNode previous = container.Siblings[boundary - 1];
		SyntaxNode next = container.Siblings[boundary];

		if (!IsWhitespaceGap(previous, next, container.CancellationToken))
			return false;

		if (rule.Descriptor == DiagnosticDescriptors.KeepGuardClausesTogether)
			return IsKeptGuard(previous, container.Options) && IsKeptGuard(next, container.Options);

		if (rule.Descriptor == DiagnosticDescriptors.KeepDependentBindingsTogether)
			return BlankLineDependentBindings.GetInner(container).Contains(boundary);

		if (rule.Descriptor == DiagnosticDescriptors.KeepReceiverGroupTogether)
			return BlankLineReceiverGroups.GetGroups(container).Inner.Contains(boundary);

		if (rule.Descriptor == DiagnosticDescriptors.KeepInitializationWithCalls)
			return BlankLineReceiverCalls.IsInitializationPair(container, boundary);

		if (rule.Descriptor == DiagnosticDescriptors.KeepGuardWithBinding)
			return IsGuardOfBinding(previous, next, options: container.Options, semanticModel: container.Context.SemanticModel);

		if (rule.Descriptor == DiagnosticDescriptors.KeepJumpWithBinding)
			return IsJumpOfLoneBinding(container, boundary);

		if (rule.Descriptor == DiagnosticDescriptors.KeepNarrowingAssertionWithBinding)
			return IsAssertionOfBinding(container, boundary);

		return rule.Descriptor == DiagnosticDescriptors.KeepAccumulatorWithLoop && GetAccumulatorGaps(container).Contains(boundary);
	}

	private static bool IsKeptGuard(SyntaxNode unit, StanzaOptions options) =>
		BlankLineGuards.IsGuard(unit, options) && (!StanzaSyntax.IsMultiLine(unit) || !BlankLineCheckedStanzas.IsAssertionRoot(unit, options));

	/// <summary>The gaps the statement run rules that are on report, since a gap is claimed once.</summary>
	public static ImmutableHashSet<int> ReadRunBoundaries(ContainerContext container, StanzaRule rule)
	{
		StanzaRule gather = rule.GetDeferral(DiagnosticDescriptors.GatherRunOfLooseStatements);
		StanzaRule keep = rule.GetDeferral(DiagnosticDescriptors.KeepRunOfStatementsTogether);

		ContainerContext reading = container with { ReadsRuns = true, IgnoredAnchorKeepers = container.IgnoredAnchorKeepers | ReadAnchorBit(rule) };

		return !container.ReadsRuns && container.RunOwnership is { } ownership
			? ownership.Get(reading, gather, keep)
			: ReadRunRuleBoundaries(reading, gather, keep);
	}

	/// <summary>The gaps the statement run rules report from their own read, with every claim in it.</summary>
	public static ImmutableHashSet<int> ReadReportedRunBoundaries(ContainerContext container, StanzaRule rule)
	{
		StanzaRule gather = rule.GetDeferral(DiagnosticDescriptors.GatherRunOfLooseStatements);
		StanzaRule keep = rule.GetDeferral(DiagnosticDescriptors.KeepRunOfStatementsTogether);

		return ReadRunRuleBoundaries(container, gather, keep);
	}

	/// <summary>Reads the bit of a keeper that anchors on separating boundaries, or 0 for any other rule.</summary>
	private static int ReadAnchorBit(StanzaRule rule)
	{
		int index = s_anchorKeepers.IndexOf(rule);
		return index < 0 ? 0 : 1 << index;
	}

	/// <summary>Caches ownership, anchors and kept groups so gap queries share their container scans.</summary>
	public sealed class RunOwnership
	{
		private Dictionary<(StanzaRule Gather, StanzaRule Keep, int Ignored), ImmutableHashSet<int>>? _boundaries;
		private Dictionary<StanzaRule, ImmutableHashSet<int>>? _anchors;
		private Dictionary<(bool Runs, bool Anchors, int Ignored), int[]>? _projections;

		private Dictionary<(bool Runs, bool Anchors, int Ignored), ImmutableHashSet<int>>? _initializations;
		private Dictionary<(bool Runs, bool Anchors, int Ignored), ImmutableHashSet<int>>? _joined;
		private Dictionary<(bool Runs, bool Anchors, int Ignored), ImmutableHashSet<int>>? _displaced;

		private ImmutableHashSet<int>? _accumulators;

		/// <summary>Gets the accumulator gaps, a walk of each loop that every claim would repeat.</summary>
		public ImmutableHashSet<int> GetAccumulators(ContainerContext container) => _accumulators ??= BlankLineAccumulators.ReadGaps(container);

		/// <summary>Gets the gaps inside kept groups, which every read of a gap owner repeats.</summary>
		public ImmutableHashSet<int> GetJoined(ContainerContext container)
		{
			_joined ??= [];
			(bool ReadsRuns, bool ReadsAnchors, int Ignored) key = (container.ReadsRuns, container.ReadsAnchors, container.IgnoredAnchorKeepers);

			if (_joined.TryGetValue(key, out ImmutableHashSet<int>? joined))
				return joined;

			int[] starts = GetProjection(container);
			ImmutableHashSet<int>.Builder builder = ImmutableHashSet.CreateBuilder<int>();

			for (int boundary = 1; boundary < starts.Length; boundary++)
			{
				if (starts[boundary] != boundary)
					builder.Add(boundary);
			}

			joined = builder.ToImmutable();
			_joined.Add(key, joined);

			return joined;
		}

		/// <summary>Gets the multi-line boundaries moved above kept groups, which each owner read repeats.</summary>
		public ImmutableHashSet<int> GetDisplaced(ContainerContext container, Func<ContainerContext, ImmutableHashSet<int>> readDisplaced)
		{
			_displaced ??= [];
			(bool ReadsRuns, bool ReadsAnchors, int Ignored) key = (container.ReadsRuns, container.ReadsAnchors, container.IgnoredAnchorKeepers);

			if (_displaced.TryGetValue(key, out ImmutableHashSet<int>? displaced))
				return displaced;

			displaced = readDisplaced(container);

			_displaced.Add(key, displaced);
			return displaced;
		}

		public int[] GetProjection(ContainerContext container)
		{
			_projections ??= [];
			(bool ReadsRuns, bool ReadsAnchors, int Ignored) key = (container.ReadsRuns, container.ReadsAnchors, container.IgnoredAnchorKeepers);

			if (_projections.TryGetValue(key, out int[]? starts))
				return starts;

			starts = ReadOuterStarts(container);

			_projections.Add(key, starts);
			return starts;
		}

		public ImmutableHashSet<int> GetInitializations(ContainerContext container)
		{
			_initializations ??= [];
			(bool ReadsRuns, bool ReadsAnchors, int Ignored) key = (container.ReadsRuns, container.ReadsAnchors, container.IgnoredAnchorKeepers);

			if (_initializations.TryGetValue(key, out ImmutableHashSet<int>? gaps))
				return gaps;

			gaps = ReadInitializationGaps(container);

			_initializations.Add(key, gaps);
			return gaps;
		}

		public ImmutableHashSet<int> GetAnchors(ContainerContext container, StanzaRule keeper)
		{
			_anchors ??= [];

			if (_anchors.TryGetValue(keeper, out ImmutableHashSet<int>? anchors))
				return anchors;

			anchors = BlankLineOwnership.ReadAskedAnchors(container with { ReadsAnchors = true }, keeper);

			_anchors.Add(keeper, anchors);
			return anchors;
		}

		public ImmutableHashSet<int> Get(ContainerContext container, StanzaRule gather, StanzaRule keep)
		{
			_boundaries ??= [];

			if (_boundaries.TryGetValue((gather, keep, container.IgnoredAnchorKeepers), out ImmutableHashSet<int>? boundaries))
				return boundaries;

			boundaries = ReadRunRuleBoundaries(container, gather, keep);

			_boundaries.Add((gather, keep, container.IgnoredAnchorKeepers), boundaries);
			return boundaries;
		}
	}

	private static ImmutableHashSet<int> GetAccumulatorGaps(ContainerContext container) =>
		container.RunOwnership?.GetAccumulators(container) ?? BlankLineAccumulators.ReadGaps(container);

	/// <summary>Gets the gaps inside kept groups, which a read of runs takes as tight before any fix.</summary>
	public static ImmutableHashSet<int> GetJoinedBoundaries(ContainerContext container) => container.RunOwnership?.GetJoined(container) ?? [];

	/// <summary>Gets the active initialization joins a jump must read before either fix runs.</summary>
	/// <remarks>
	/// Run readers retain these joins while their upper-gap query excludes the claiming keeper.
	/// </remarks>
	public static ImmutableHashSet<int> GetInitializationGaps(ContainerContext container)
	{
		StanzaRule keeper = StanzaRules.KeepInitializationWithCalls;

		return
			container.RunOwnership is not { } ownership
			|| !keeper.Kinds.Contains(container.Kind)
			|| StanzaRuleState.IsRuleDisabled(container, container.Options, keeper)
			? []
			: ownership.GetInitializations(container);
	}

	private static ImmutableHashSet<int> ReadInitializationGaps(ContainerContext container)
	{
		StanzaRule keeper = StanzaRules.KeepInitializationWithCalls;
		ImmutableHashSet<int>.Builder gaps = ImmutableHashSet.CreateBuilder<int>();
		ImmutableHashSet<int>? upperRemovals = null;

		bool HasStableOpening(int boundary)
		{
			if (boundary == 1 || container.ReadsAnchors)
				return true;

			// Native anchor reads stop the upper-removal preview from planning these same joins again.
			upperRemovals ??= ReadRunBoundaries(container with { ReadsRuns = true, ReadsAnchors = true }, keeper);
			return !upperRemovals.Contains(boundary - 1);
		}

		for (int boundary = 1; boundary < container.Siblings.Length; boundary++)
		{
			container.CancellationToken.ThrowIfCancellationRequested();

			if (
				BlankLineReceiverCalls.IsInitializationCandidate(container, boundary)
				&& HasStableOpening(boundary)
				&& IsWhitespaceGap(container.Siblings[boundary - 1], container.Siblings[boundary], container.CancellationToken)
				&& !StanzaRuleState.IsOffAt(
					scope: container,
					options: container.Options,
					rule: keeper,
					position: container.Siblings[boundary].SpanStart
				)
			)
			{
				gaps.Add(boundary);
			}
		}

		return gaps.ToImmutable();
	}

	private static ImmutableHashSet<int> ReadRunRuleBoundaries(ContainerContext container, StanzaRule gather, StanzaRule keep)
	{
		ImmutableHashSet<int>.Builder boundaries = ImmutableHashSet.CreateBuilder<int>();

		if (IsJudgedBy(container, gather))
			AddRunBoundaries(container, runRule: gather, runs: BlankLineLooseRuns.ReadRuns(container, gather, judgesMembers: false), boundaries);

		if (IsJudgedBy(container, keep))
			AddRunBoundaries(container, runRule: keep, runs: BlankLineKeptRuns.ReadRuns(container, keep, judgesMembers: false).Runs, boundaries);

		return boundaries.ToImmutable();
	}

	private static bool IsJudgedBy(ContainerContext container, StanzaRule runRule) =>
		runRule.Kinds.Contains(container.Kind) && !StanzaRuleState.IsRuleDisabled(container, container.Options, runRule);

	private static void AddRunBoundaries(
		ContainerContext container,
		StanzaRule runRule,
		ImmutableArray<BlankLineLooseRuns.LooseRun> runs,
		ImmutableHashSet<int>.Builder boundaries
	)
	{
		foreach (BlankLineLooseRuns.LooseRun run in runs)
		{
			for (int boundary = run.Start + 1; boundary < run.End; boundary++)
			{
				int position = container.Siblings[boundary].SpanStart;

				if (!StanzaRuleState.IsOffAt(scope: container, options: container.Options, rule: runRule, position))
					boundaries.Add(boundary);
			}
		}
	}

	/// <summary>Moves a separating boundary above the kept group that contains it.</summary>
	public static ImmutableArray<int> ReadOuterBoundaries(ContainerContext container, ImmutableArray<int> boundaries)
	{
		if (boundaries.IsEmpty || !HasEnabledRule(container))
			return boundaries;

		int[] starts = container.RunOwnership!.GetProjection(container);

		ImmutableHashSet<int>? runs = null;
		ImmutableArray<int>.Builder outer = ImmutableArray.CreateBuilder<int>();

		bool IsUpperAvailable(int start)
		{
			return start > 0
				&& (
					container.ReadsRuns
					|| container.ReadsAnchors
					|| !(runs ??= ReadRunBoundaries(container, StanzaRules.KeepGuardWithBinding)).Contains(start)
				)
				&& IsWhitespaceGap(container.Siblings[start - 1], container.Siblings[start], container.CancellationToken);
		}

		foreach (int boundary in boundaries)
		{
			int start = starts[boundary];

			if (start == boundary || IsUpperAvailable(start))
				outer.Add(start);
		}

		return [.. outer.Distinct()];
	}

	private static int[] ReadOuterStarts(ContainerContext container)
	{
		ImmutableHashSet<int> bindings = BlankLineCheckedStanzas.ReadConnectedBindingGaps(container);
		int[] starts = new int[container.Siblings.Length];

		for (int boundary = 1; boundary < starts.Length; boundary++)
			starts[boundary] = (bindings.Contains(boundary) || IsGapClaimed(container, boundary)) ? starts[boundary - 1] : boundary;

		return starts;
	}

	/// <summary>Finds the local a declarator declares, through the operation the model has already bound.</summary>
	/// <remarks>
	/// <c>GetDeclaredSymbol</c> searches the locals of the block for each declarator, so a long block costs its square.
	/// </remarks>
	public static ILocalSymbol? FindDeclaredLocal(
		SemanticModel semanticModel,
		VariableDeclaratorSyntax declarator,
		System.Threading.CancellationToken cancellationToken
	) => (semanticModel.GetOperation(declarator, cancellationToken) as IVariableDeclaratorOperation)?.Symbol;

	/// <summary>Whether the gap holds no comment and no directive, so a blank line in it is padding.</summary>
	public static bool IsWhitespaceGap(SyntaxNode previous, SyntaxNode next, System.Threading.CancellationToken cancellationToken)
	{
		SourceText sourceText = next.SyntaxTree.GetText(cancellationToken);

		return StanzaText.IsWhitespaceOnly(
			sourceText,
			TextSpan.FromBounds(SiblingGaps.GetGapStartToken(previous, next).Span.End, next.GetFirstToken().SpanStart)
		);
	}

	/// <summary>
	/// Whether the next unit is a guard <c>if</c> whose condition reads a name the previous one binds.
	/// </summary>
	private static bool IsGuardOfBinding(SyntaxNode previous, SyntaxNode next, StanzaOptions options, SemanticModel semanticModel)
	{
		if (next is not IfStatementSyntax ifStatement || !BlankLineGuards.IsGuard(ifStatement, options))
			return false;

		HashSet<string> bound = ReadBoundNames(previous);

		return bound.Count > 0 && ReadValueNames(ifStatement.Condition, semanticModel).Any(bound.Contains);
	}

	/// <summary>Whether a lone single-line binding stands above a single-line jump that reads a bound name.</summary>
	/// <remarks>
	/// The binding is lone when it opens the container or a blank line no other keep rule removes stands above it.
	/// </remarks>
	private static bool IsJumpOfLoneBinding(ContainerContext container, int boundary)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;

		SyntaxNode previous = siblings[boundary - 1];
		SyntaxNode next = siblings[boundary];

		if (StanzaSyntax.IsMultiLine(previous) || StanzaSyntax.IsMultiLine(next) || ReadJumpValue(next) is not { } value)
			return false;

		HashSet<string> bound = ReadBoundNames(previous);

		if (bound.Count == 0 || !ReadValueNames(value, container.Context.SemanticModel).Any(bound.Contains))
			return false;

		if (boundary == 1)
			return true;

		if (
			!HasStanzaOpening(container, boundary - 1, StanzaRules.KeepJumpWithBinding)
			|| IsGapClaimedByAnother(container, boundary - 1, askingRule: StanzaRules.KeepJumpWithBinding)
		)
		{
			return false;
		}

		// The run rules read the claims as they read their runs, so the claim inside that read takes the gap above as it stands.
		return container.ReadsAnchors
			|| !ReadRunBoundaries(container with { ReadsRuns = false }, StanzaRules.KeepJumpWithBinding).Contains(boundary - 1);
	}

	public static bool HasStanzaOpening(ContainerContext container, int boundary, StanzaRule keeper)
	{
		SyntaxNode previous = container.Siblings[boundary - 1];
		SyntaxNode next = container.Siblings[boundary];

		if (BlankLineDependentBindings.GetInner(container).Contains(boundary))
			return false;

		return BlankLineGaps.HasBlankLineBetween(previous, next)
			|| (
				!container.ReadsAnchors
				&& IsWhitespaceGap(previous, next, container.CancellationToken)
				&& container.RunOwnership?.GetAnchors(container, keeper).Contains(boundary) == true
			);
	}

	/// <summary>Reads the value a <c>return</c> or a <c>throw</c> hands on, if the unit is one.</summary>
	private static ExpressionSyntax? ReadJumpValue(SyntaxNode unit)
	{
		return unit switch
		{
			ReturnStatementSyntax statement => statement.Expression,
			ThrowStatementSyntax statement => statement.Expression,
			ExpressionStatementSyntax { Expression: ThrowExpressionSyntax expression } => expression.Expression,
			_ => null,
		};
	}

	/// <summary>Whether a single-line assertion narrows a name the unit above binds.</summary>
	/// <remarks>
	/// An assertion with another below it leads the assert phase of a test, so it stays with that run.
	/// </remarks>
	private static bool IsAssertionOfBinding(ContainerContext container, int boundary)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		SemanticModel semanticModel = container.Context.SemanticModel;
		SyntaxNode next = siblings[boundary];

		return IsEligibleAssertion(container, boundary) && ReadNarrowedNames(next, semanticModel).Overlaps(ReadBoundNames(siblings[boundary - 1]));
	}

	public static bool IsEligibleAssertion(ContainerContext container, int index)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		SemanticModel semanticModel = container.Context.SemanticModel;
		SyntaxNode unit = siblings[index];

		if (StanzaSyntax.IsMultiLine(unit) || ReadNarrowedNames(unit, semanticModel).Count == 0)
			return false;

		return index + 1 == siblings.Length
			|| !IsWhitespaceGap(unit, siblings[index + 1], container.CancellationToken)
			|| !IsAssertion(siblings[index + 1], container.Options, semanticModel);
	}

	/// <summary>Whether the unit calls an assertion root or narrows a name.</summary>
	private static bool IsAssertion(SyntaxNode unit, StanzaOptions options, SemanticModel semanticModel)
	{
		if (unit is not ExpressionStatementSyntax statement || BlankLineExpressions.FindInvocation(statement.Expression) is not { } invocation)
			return false;

		ExpressionSyntax callee = BlankLineExpressions.GetRootInvocation(invocation).Expression;

		return BlankLineExpressions.FindRoot(callee, options.AssertionRoots) is not null || ReadNarrowedNames(unit, semanticModel).Count > 0;
	}

	/// <summary>Reads the names a call narrows by the flow attributes of the parameters it passes them to.</summary>
	/// <remarks>
	/// A <c>[NotNull]</c> parameter narrows the name it gets, and a <c>[DoesNotReturnIf]</c> one the names it tests.
	/// </remarks>
	public static HashSet<string> ReadNarrowedNames(SyntaxNode unit, SemanticModel semanticModel)
	{
		HashSet<string> names = new(StringComparer.Ordinal);

		if (unit is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation })
			return names;

		if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
			return names;

		SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;

		for (int index = 0; index < arguments.Count; index++)
		{
			if (FindParameter(method, arguments[index], index) is not { } parameter)
				continue;

			foreach (AttributeData attribute in parameter.GetAttributes())
				AddNarrowedNames(attribute, arguments[index].Expression, names);
		}

		return names;
	}

	private static IParameterSymbol? FindParameter(IMethodSymbol method, ArgumentSyntax argument, int index)
	{
		ImmutableArray<IParameterSymbol> parameters = method.Parameters;

		if (argument.NameColon is { } nameColon)
			return parameters.FirstOrDefault(parameter => parameter.Name == nameColon.Name.Identifier.ValueText);

		if (index < parameters.Length)
			return parameters[index];

		return parameters.Length > 0 && parameters[parameters.Length - 1].IsParams ? parameters[parameters.Length - 1] : null;
	}

	private static void AddNarrowedNames(AttributeData attribute, ExpressionSyntax argument, HashSet<string> names)
	{
		if (attribute.AttributeClass is not { } type || type.ContainingNamespace?.ToDisplayString() != "System.Diagnostics.CodeAnalysis")
			return;

		if (type.Name == "NotNullAttribute")
		{
			if (ReadName(argument) is { } name)
				names.Add(name);

			return;
		}

		if (
			type.Name == "DoesNotReturnIfAttribute"
			&& attribute.ConstructorArguments.Length == 1
			&& attribute.ConstructorArguments[0].Value is bool exitValue
		)
		{
			AddCheckedNames(argument, holdsWhenTrue: !exitValue, names);
		}
	}

	/// <summary>Adds the names a condition checks for a value or a type, where the call goes on only then.</summary>
	private static void AddCheckedNames(ExpressionSyntax condition, bool holdsWhenTrue, HashSet<string> names)
	{
		condition = StripParentheses(condition);

		switch (condition)
		{
			case BinaryExpressionSyntax binary when binary.IsKind(holdsWhenTrue ? SyntaxKind.LogicalAndExpression : SyntaxKind.LogicalOrExpression):
				AddCheckedNames(binary.Left, holdsWhenTrue, names);
				AddCheckedNames(binary.Right, holdsWhenTrue, names);

				break;

			case BinaryExpressionSyntax binary when binary.IsKind(holdsWhenTrue ? SyntaxKind.NotEqualsExpression : SyntaxKind.EqualsExpression):
				AddNullComparedName(binary, names);
				break;

			case BinaryExpressionSyntax binary when holdsWhenTrue && binary.IsKind(SyntaxKind.IsExpression):
				AddName(binary.Left, names);
				break;

			case IsPatternExpressionSyntax isPattern when IsNarrowingPattern(isPattern.Pattern, holdsWhenTrue):
				AddName(isPattern.Expression, names);
				break;
		}
	}

	private static void AddNullComparedName(BinaryExpressionSyntax comparison, HashSet<string> names)
	{
		if (comparison.Right.IsKind(SyntaxKind.NullLiteralExpression))
			AddName(comparison.Left, names);
		else if (comparison.Left.IsKind(SyntaxKind.NullLiteralExpression))
			AddName(comparison.Right, names);
	}

	/// <summary>Whether a pattern leaves its operand set: a type, a property pattern or <c>not null</c>.</summary>
	private static bool IsNarrowingPattern(PatternSyntax pattern, bool holdsWhenTrue)
	{
		if (!holdsWhenTrue)
		{
			return pattern is ConstantPatternSyntax { Expression: LiteralExpressionSyntax literal }
				&& literal.IsKind(SyntaxKind.NullLiteralExpression);
		}

		return pattern switch
		{
			TypePatternSyntax or DeclarationPatternSyntax or RecursivePatternSyntax => true,

			UnaryPatternSyntax { Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax literal } } unary => unary.IsKind(
				SyntaxKind.NotPattern
			) && literal.IsKind(SyntaxKind.NullLiteralExpression),

			_ => false,
		};
	}

	private static void AddName(ExpressionSyntax expression, HashSet<string> names)
	{
		if (ReadName(expression) is { } name)
			names.Add(name);
	}

	/// <summary>Reads the plain name an argument passes, through parentheses and a <c>!</c>.</summary>
	private static string? ReadName(ExpressionSyntax expression)
	{
		expression = StripParentheses(expression);

		if (expression is PostfixUnaryExpressionSyntax suppression && suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
			expression = StripParentheses(suppression.Operand);

		return expression is IdentifierNameSyntax name ? name.Identifier.ValueText : null;
	}

	private static ExpressionSyntax StripParentheses(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax parenthesized)
			expression = parenthesized.Expression;

		return expression;
	}

	/// <summary>Reads the local names a declaration or a simple assignment binds.</summary>
	public static HashSet<string> ReadBoundNames(SyntaxNode statement)
	{
		HashSet<string> names = new(StringComparer.Ordinal);

		switch (statement)
		{
			case LocalDeclarationStatementSyntax declaration:
				foreach (VariableDeclaratorSyntax declarator in declaration.Declaration.Variables)
					names.Add(declarator.Identifier.ValueText);

				break;

			case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
				when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
				AddAssignedNames(assignment.Left, names);
				break;
		}

		return names;
	}

	private static void AddAssignedNames(ExpressionSyntax target, HashSet<string> names)
	{
		switch (target)
		{
			case IdentifierNameSyntax name:
				names.Add(name.Identifier.ValueText);
				break;

			case DeclarationExpressionSyntax declaration:
				AddDesignatedNames(declaration.Designation, names);
				break;

			case TupleExpressionSyntax tuple:
				foreach (ArgumentSyntax argument in tuple.Arguments)
					AddAssignedNames(argument.Expression, names);

				break;
		}
	}

	private static void AddDesignatedNames(VariableDesignationSyntax designation, HashSet<string> names)
	{
		switch (designation)
		{
			case SingleVariableDesignationSyntax single:
				names.Add(single.Identifier.ValueText);
				break;

			case ParenthesizedVariableDesignationSyntax parenthesized:
				foreach (VariableDesignationSyntax variable in parenthesized.Variables)
					AddDesignatedNames(variable, names);

				break;
		}
	}

	/// <summary>
	/// Reads outer value names, excluding <c>nameof</c> operands and bindings declared within the expression.
	/// </summary>
	public static IEnumerable<string> ReadValueNames(SyntaxNode expression, SemanticModel semanticModel)
	{
		Dictionary<ISymbol, bool> declaredInside = new(SymbolEqualityComparer.Default);

		bool IsOuterRead(IdentifierNameSyntax name)
		{
			if (!IsRead(name))
				return false;

			if (semanticModel.GetSymbolInfo(name).Symbol is not { } symbol)
				return true;

			if (!declaredInside.TryGetValue(symbol, out bool inside))
			{
				inside = symbol.Locations.Any(location =>
					location.IsInSource && location.SourceTree == expression.SyntaxTree && expression.Span.Contains(location.SourceSpan)
				);

				declaredInside.Add(symbol, inside);
			}

			return !inside;
		}

		return expression
			.DescendantNodesAndSelf(node =>
				node is not InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } invocation
				|| semanticModel.GetOperation(invocation) is not INameOfOperation
			)
			.OfType<IdentifierNameSyntax>()
			.Where(IsOuterRead)
			.Select(name => name.Identifier.ValueText);
	}

	/// <summary>Whether the name stands as a value of its own, not as the member a dot or a label names.</summary>
	private static bool IsRead(IdentifierNameSyntax name)
	{
		bool value = name.Parent switch
		{
			MemberAccessExpressionSyntax access => access.Expression == name,
			MemberBindingExpressionSyntax => false,
			NameColonSyntax or NameEqualsSyntax => false,
			QualifiedNameSyntax => false,
			_ => !SyntaxFacts.IsInTypeOnlyContext(name),
		};

		return value && !IsWriteOnlyTarget(name);
	}

	private static bool IsWriteOnlyTarget(ExpressionSyntax target)
	{
		while (true)
		{
			switch (target.Parent)
			{
				case ParenthesizedExpressionSyntax parenthesized:
					target = parenthesized;
					continue;

				case PostfixUnaryExpressionSyntax suppression when suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression):
					target = suppression;
					continue;

				case ArgumentSyntax { Parent: TupleExpressionSyntax tuple }:
					target = tuple;
					continue;
			}

			return target.Parent switch
			{
				AssignmentExpressionSyntax assignment when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) => assignment.Left == target,
				ArgumentSyntax argument => argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword),
				_ => false,
			};
		}
	}
}
