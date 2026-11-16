using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>A container a blank-line rule judges: its analysis context, the file's options and its siblings.</summary>
/// <remarks>
/// Copies share geometry while a receiver rule or a relationship keeper needs it.
/// </remarks>
internal readonly record struct ContainerContext(SyntaxNodeAnalysisContext Context, StanzaOptions Options, ImmutableArray<SyntaxNode> Siblings)
{
	private readonly Readings _readings = new();

	public bool ReadsRuns { get; init; }
	public bool ReadsAnchors { get; init; }
	public int IgnoredAnchorKeepers { get; init; }

	public BlankLineReceiverGroups.Reading? ReceiverGroups => _readings.FindReceiverGroups(this);
	public BlankLineDependentBindings.Reading? DependentBindings => _readings.FindDependentBindings(this);
	public BlankLineKeptPairs.RunOwnership? RunOwnership => _readings.FindRunOwnership(this);
	public ImmutableArray<BlankLineCheckedStanzas.Stanza> CheckedStanzas => _readings.CheckedStanzas.Get(this);
	public SyntaxNode Node => Context.Node;
	public SyntaxKind Kind => Context.Node.Kind();
	public CancellationToken CancellationToken => Context.CancellationToken;

	/// <summary>Reads the siblings and the options of the container the context analyzes.</summary>
	public static ContainerContext From(SyntaxNodeAnalysisContext context) =>
		new(context, StanzaOptions.From(context.Options, context.Node.SyntaxTree), StanzaSyntax.ReadSiblings(context.Node));

	public static implicit operator RuleScope(ContainerContext container) => container.Context;

	/// <summary>Creates each reading on its first ask, so a rule that never asks reads no rule's state.</summary>
	private sealed class Readings
	{
		private bool _readsReceiverGroups;
		private bool _readsDependentBindings;
		private bool _readsRunOwnership;

		private BlankLineReceiverGroups.Reading? _receiverGroups;
		private BlankLineDependentBindings.Reading? _dependentBindings;
		private BlankLineKeptPairs.RunOwnership? _runOwnership;

		public BlankLineCheckedStanzas.Reading CheckedStanzas { get; } = new();

		public BlankLineReceiverGroups.Reading? FindReceiverGroups(ContainerContext container)
		{
			if (!_readsReceiverGroups)
			{
				_readsReceiverGroups = true;
				_receiverGroups = IsReceiverRuleOn(container) ? new() : null;
			}

			return _receiverGroups;
		}

		public BlankLineDependentBindings.Reading? FindDependentBindings(ContainerContext container)
		{
			if (!_readsDependentBindings)
			{
				_readsDependentBindings = true;
				_dependentBindings = IsJudgedBy(container, StanzaRules.KeepDependentBindingsTogether) ? new() : null;
			}

			return _dependentBindings;
		}

		public BlankLineKeptPairs.RunOwnership? FindRunOwnership(ContainerContext container)
		{
			if (!_readsRunOwnership)
			{
				_readsRunOwnership = true;
				_runOwnership = BlankLineKeptPairs.HasEnabledRule(container.Context, container.Options) ? new() : null;
			}

			return _runOwnership;
		}

		private static bool IsReceiverRuleOn(ContainerContext container)
		{
			return !container.Options.ReceiverRoots.IsEmpty
				&& (IsJudgedBy(container, StanzaRules.SeparateReceiverGroups) || IsJudgedBy(container, StanzaRules.KeepReceiverGroupTogether));
		}

		private static bool IsJudgedBy(ContainerContext container, StanzaRule rule) =>
			rule.Kinds.Contains(container.Kind) && !StanzaRuleState.IsRuleDisabled(container, container.Options, rule);
	}
}
