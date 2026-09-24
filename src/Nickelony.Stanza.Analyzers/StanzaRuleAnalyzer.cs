namespace Nickelony.Stanza.Analyzers;

/// <summary>Base of an analyzer that judges the node kinds of one registered rule.</summary>
internal abstract class StanzaRuleAnalyzer(StanzaRule rule) : StanzaNodeAnalyzer(rule.Descriptor, rule.Kinds)
{
	protected StanzaRule Rule { get; } = rule;
}
