using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>A container a blank-line rule judges: its analysis context, the file's options and its siblings.</summary>
/// <remarks>
/// Copies share geometry while a receiver rule or a relationship keeper needs it, as do the rules that judge one node.
/// </remarks>
internal readonly record struct ContainerContext(SyntaxNodeAnalysisContext Context, StanzaOptions Options, ImmutableArray<SyntaxNode> Siblings)
{
	private readonly Readings _readings = Readings.Share(Context.Node, Context.Compilation, Options);

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
	/// <remarks>
	/// The rules that judge a node ask from threads of their own, so one gate guards every reading.
	/// </remarks>
	private sealed class Readings
	{
		private static readonly ConditionalWeakTable<SyntaxNode, Readings> s_shared = new();

		private readonly WeakReference<Compilation> _compilation;
		private readonly StanzaOptions _options;
		private readonly object _gate = new();

		private bool _readsReceiverGroups;
		private bool _readsDependentBindings;
		private bool _readsRunOwnership;

		private BlankLineReceiverGroups.Reading? _receiverGroups;
		private BlankLineDependentBindings.Reading? _dependentBindings;
		private BlankLineKeptPairs.RunOwnership? _runOwnership;

		private Readings(Compilation compilation, StanzaOptions options)
		{
			_compilation = new WeakReference<Compilation>(compilation);
			_options = options;
			CheckedStanzas = new(_gate);
		}

		public BlankLineCheckedStanzas.Reading CheckedStanzas { get; }

		/// <summary>Gets the readings of the node, which another compilation or other options do not share.</summary>
		public static Readings Share(SyntaxNode node, Compilation compilation, StanzaOptions options)
		{
			lock (s_shared)
			{
				if (s_shared.TryGetValue(node, out Readings? shared) && shared.Matches(compilation, options))
					return shared;

				var readings = new Readings(compilation, options);

				s_shared.Remove(node);
				s_shared.Add(node, readings);

				return readings;
			}
		}

		private bool Matches(Compilation compilation, StanzaOptions options) =>
			ReferenceEquals(_options, options) && _compilation.TryGetTarget(out Compilation? known) && ReferenceEquals(known, compilation);

		public BlankLineReceiverGroups.Reading? FindReceiverGroups(ContainerContext container)
		{
			lock (_gate)
			{
				if (!_readsReceiverGroups)
				{
					_readsReceiverGroups = true;
					_receiverGroups = IsReceiverRuleOn(container) ? new(_gate) : null;
				}

				return _receiverGroups;
			}
		}

		public BlankLineDependentBindings.Reading? FindDependentBindings(ContainerContext container)
		{
			lock (_gate)
			{
				if (!_readsDependentBindings)
				{
					_readsDependentBindings = true;
					_dependentBindings = IsJudgedBy(container, StanzaRules.KeepDependentBindingsTogether) ? new(_gate) : null;
				}

				return _dependentBindings;
			}
		}

		public BlankLineKeptPairs.RunOwnership? FindRunOwnership(ContainerContext container)
		{
			lock (_gate)
			{
				if (!_readsRunOwnership)
				{
					_readsRunOwnership = true;
					_runOwnership = BlankLineKeptPairs.HasEnabledRule(container.Context, container.Options) ? new(_gate) : null;
				}

				return _runOwnership;
			}
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
