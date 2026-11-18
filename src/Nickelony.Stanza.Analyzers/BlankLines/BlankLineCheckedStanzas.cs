using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads guard chains and the bindings directly above them that their checks read.</summary>
internal static class BlankLineCheckedStanzas
{
	private enum CheckKind
	{
		None,
		Guard,
		Assertion,
	}

	public readonly record struct Stanza(int Start, int FirstCheck, int End, bool AssertsOnly);

	/// <summary>Caches the stanzas of a container, a read that binds each statement.</summary>
	public sealed class Reading
	{
		private ImmutableArray<Stanza> _stanzas;
		public ImmutableArray<Stanza> Get(ContainerContext container) => _stanzas.IsDefault ? _stanzas = ReadStanzas(container) : _stanzas;
	}

	/// <summary>Reads the edges of the checked stanzas in order, each once.</summary>
	/// <remarks>
	/// One stanza can end where the next starts, and that gap is still one gap.
	/// </remarks>
	public static ImmutableArray<int> ReadBoundaries(ContainerContext container)
	{
		ImmutableArray<int>.Builder boundaries = ImmutableArray.CreateBuilder<int>();

		void Add(int boundary)
		{
			if (boundaries.Count == 0 || boundaries[boundaries.Count - 1] != boundary)
				boundaries.Add(boundary);
		}

		foreach (Stanza stanza in container.CheckedStanzas)
		{
			if (stanza.Start > 0)
				Add(stanza.Start);

			if (!stanza.AssertsOnly && stanza.End < container.Siblings.Length)
				Add(stanza.End);
		}

		return boundaries.ToImmutable();
	}

	/// <summary>Reads the binding gaps of checked groups claimed by an enabled keeper.</summary>
	public static ImmutableHashSet<int> ReadConnectedBindingGaps(ContainerContext container, StanzaRule? askingKeeper = null)
	{
		if (
			StanzaRuleState.IsRuleDisabled(container, container.Options, StanzaRules.KeepGuardWithBinding)
			&& StanzaRuleState.IsRuleDisabled(container, container.Options, StanzaRules.KeepNarrowingAssertionWithBinding)
		)
		{
			return [];
		}

		ImmutableHashSet<int>.Builder gaps = ImmutableHashSet.CreateBuilder<int>();

		foreach (Stanza stanza in container.CheckedStanzas)
		{
			StanzaRule? keeper =
				stanza.AssertsOnly || BlankLineKeptPairs.IsEligibleAssertion(container, stanza.FirstCheck)
					? StanzaRules.KeepNarrowingAssertionWithBinding
				: container.Siblings[stanza.FirstCheck] is IfStatementSyntax ? StanzaRules.KeepGuardWithBinding
				: null;

			if (keeper is null || !keeper.Kinds.Contains(container.Kind))
				continue;

			if (askingKeeper is not null && askingKeeper.Descriptor != keeper.Descriptor)
				continue;

			if (
				StanzaRuleState.IsOffAt(
					scope: container,
					options: container.Options,
					rule: keeper,
					position: container.Siblings[stanza.FirstCheck].SpanStart
				)
			)
			{
				continue;
			}

			for (int boundary = stanza.Start + 1; boundary <= stanza.FirstCheck; boundary++)
				gaps.Add(boundary);
		}

		return gaps.ToImmutable();
	}

	private static ImmutableArray<Stanza> ReadStanzas(ContainerContext container)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		CheckKind[] kinds = ReadKinds(container);
		ImmutableArray<Stanza>.Builder stanzas = ImmutableArray.CreateBuilder<Stanza>();

		for (int index = 0; index < siblings.Length; )
		{
			if (kinds[index] == CheckKind.None)
			{
				index++;
				continue;
			}

			int first = index;
			bool assertsOnly = true;
			HashSet<string> reads = [];

			do
			{
				assertsOnly &= kinds[index] == CheckKind.Assertion;
				reads.UnionWith(ReadNames(container, siblings[index]));
				index++;
			} while (
				index < siblings.Length
				&& kinds[index] != CheckKind.None
				&& BlankLineKeptPairs.IsWhitespaceGap(siblings[index - 1], siblings[index], container.CancellationToken)
			);

			int start = first;

			while (
				start > 0
				&& BlankLineKeptPairs.ReadBoundNames(siblings[start - 1]).Overlaps(reads)
				&& BlankLineKeptPairs.IsWhitespaceGap(siblings[start - 1], siblings[start], container.CancellationToken)
			)
			{
				start--;
			}

			stanzas.Add(new Stanza(Start: start, FirstCheck: first, End: index, AssertsOnly: assertsOnly));
		}

		return stanzas.ToImmutable();
	}

	private static CheckKind[] ReadKinds(ContainerContext container)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		var kinds = new CheckKind[siblings.Length];
		bool[] assertions = new bool[siblings.Length];

		for (int index = 0; index < siblings.Length; index++)
		{
			SyntaxNode unit = siblings[index];

			bool guard = BlankLineGuards.IsGuard(unit, container.Options);
			bool narrows = BlankLineKeptPairs.ReadNarrowedNames(unit, container.Context.SemanticModel).Count > 0;
			bool assertion = IsAssertionRoot(unit, container.Options) || (narrows && !guard);
			bool singleLine = !StanzaSyntax.IsMultiLine(unit);

			assertions[index] = assertion && singleLine;

			if (assertion)
				kinds[index] = narrows && singleLine ? CheckKind.Assertion : CheckKind.None;
			else if (guard)
				kinds[index] = CheckKind.Guard;
		}

		for (int index = 0; index < siblings.Length; )
		{
			if (!assertions[index])
			{
				index++;
				continue;
			}

			int first = index;
			bool plain = false;

			do
			{
				plain |= kinds[index] == CheckKind.None;
				index++;
			} while (
				index < siblings.Length
				&& assertions[index]
				&& BlankLineKeptPairs.IsWhitespaceGap(siblings[index - 1], siblings[index], container.CancellationToken)
			);

			if (plain)
			{
				for (int at = first; at < index; at++)
					kinds[at] = CheckKind.None;
			}
		}

		return kinds;
	}

	public static bool IsAssertionRoot(SyntaxNode unit, StanzaOptions options)
	{
		if (unit is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call })
			return false;

		return BlankLineExpressions.FindRoot(call.Expression, options.AssertionRoots) is not null
			|| call.Expression.ToString() is "Debug.Assert" or "System.Diagnostics.Debug.Assert";
	}

	private static IEnumerable<string> ReadNames(ContainerContext container, SyntaxNode check)
	{
		SyntaxNode? condition = check switch
		{
			IfStatementSyntax guard => guard.Condition,
			ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call } => call.ArgumentList,
			_ => null,
		};

		HashSet<string> narrowed = BlankLineKeptPairs.ReadNarrowedNames(check, container.Context.SemanticModel);

		return narrowed.Count > 0 ? narrowed
			: condition is null ? []
			: BlankLineKeptPairs.ReadValueNames(condition, container.Context.SemanticModel);
	}

	public static ImmutableHashSet<int> ReadAskedBoundaries(ContainerContext container, StanzaRule askingRule)
	{
		if (
			StanzaRuleState.FindJudgingReadRule(askingRule, DiagnosticDescriptors.SeparateCheckedStanza, container.Kind) is not { } owner
			|| StanzaRuleState.IsRuleDisabled(container, container.Options, owner)
		)
		{
			return [];
		}

		bool IsOn(int boundary) =>
			!StanzaRuleState.IsOffAt(scope: container, options: container.Options, rule: owner, position: container.Siblings[boundary].SpanStart);

		// The rule asks above a kept group for an edge inside it, so the rules that yield to it yield there.
		return [.. BlankLineKeptPairs.ReadOuterBoundaries(container, [.. ReadBoundaries(container).Where(IsOn)]).Where(IsOn)];
	}
}
