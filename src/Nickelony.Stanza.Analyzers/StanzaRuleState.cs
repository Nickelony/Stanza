using System;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Decides whether a rule is off at a place, and which half of a pair owns the gap they share.</summary>
internal static class StanzaRuleState
{
	/// <summary>
	/// A rule's own severity beats the bulk keys, and <see cref="ReportDiagnostic.Default"/> keeps its shipped state.
	/// </summary>
	public static bool IsRuleOff(RuleScope scope, StanzaOptions options, StanzaRule rule)
	{
		string ruleId = rule.Descriptor.Id;

		// Roslyn lets any key a rule sets for itself block a bulk key, a `default` one included.
		return ReadConfiguredSeverity(scope, ruleId) switch
		{
			ReportDiagnostic.Default => !rule.Descriptor.IsEnabledByDefault,
			{ } severity => severity == ReportDiagnostic.Suppress,
			null => options.RulesOffUnlessEnabled.Contains(ruleId),
		};
	}

	/// <summary>The rule's severity from a <c>NoWarn</c>, then the tree, global and compilation options.</summary>
	private static ReportDiagnostic? ReadConfiguredSeverity(RuleScope scope, string ruleId)
	{
		CompilationOptions compilationOptions = scope.CompilationOptions;
		bool hasCompilationSeverity = compilationOptions.SpecificDiagnosticOptions.TryGetValue(ruleId, out ReportDiagnostic compilationSeverity);

		// Roslyn reads <NoWarn> before .editorconfig, so a rule it disables stays disabled whatever a file says.
		if (hasCompilationSeverity && compilationSeverity == ReportDiagnostic.Suppress)
			return ReportDiagnostic.Suppress;

		if (compilationOptions.SyntaxTreeOptionsProvider is { } provider)
		{
			if (
				provider.TryGetDiagnosticValue(
					tree: scope.Tree,
					diagnosticId: ruleId,
					cancellationToken: scope.CancellationToken,
					severity: out ReportDiagnostic treeSeverity
				)
			)
			{
				return treeSeverity;
			}

			if (
				provider.TryGetGlobalDiagnosticValue(
					diagnosticId: ruleId,
					cancellationToken: scope.CancellationToken,
					severity: out ReportDiagnostic globalSeverity
				)
			)
			{
				return globalSeverity;
			}
		}

		return hasCompilationSeverity ? compilationSeverity : null;
	}

	public static bool IsOffAt(RuleScope scope, StanzaOptions options, StanzaRule rule, int position)
	{
		return IsRuleOff(scope, options, rule)
			|| StanzaPragmas.IsSilencedByPragma(
				syntaxTree: scope.Tree,
				ruleId: rule.Descriptor.Id,
				position,
				cancellationToken: scope.CancellationToken
			);
	}

	public static bool IsEnabledByConfig(RuleScope scope, StanzaRule rule)
	{
		return ReadConfiguredSeverity(scope, rule.Descriptor.Id) is { } severity
			&& severity is not ReportDiagnostic.Suppress and not ReportDiagnostic.Default;
	}

	public static bool ReportsTheSharedGap(RuleScope scope, StanzaOptions options, StanzaRule rule, int position) =>
		OwnsTheSharedGap(scope, options, rule, position) && !IsOffAt(scope, options, rule, position);

	/// <summary>
	/// Whether <paramref name="rule"/> owns the gap it shares with its pair, so only one half reports it.
	/// </summary>
	public static bool OwnsTheSharedGap(RuleScope scope, StanzaOptions options, StanzaRule rule, int position)
	{
		if (rule.Pair is not { } pair)
			return true;

		StanzaRule partner = StanzaRules.Get(pair.GetOtherHalf(rule.Descriptor.Id));

		bool mineIsEnabled = IsEnabledByConfig(scope, rule);
		bool partnerIsEnabled = IsEnabledByConfig(scope, partner);

		DiagnosticDescriptor owner;

		if (mineIsEnabled && partnerIsEnabled)
			owner = pair.OwnerWhenBothEnabled;
		else if (mineIsEnabled)
			owner = rule.Descriptor;
		else if (partnerIsEnabled)
			owner = partner.Descriptor;
		else
			owner = pair.DefaultHalf;

		// A pragma over the owner hands the gap to the other half only when the file enables a half:
		// otherwise the pragma says the file wants nothing there, so the gap stays silent.
		return string.Equals(owner.Id, rule.Descriptor.Id, StringComparison.Ordinal)
			|| ((mineIsEnabled || partnerIsEnabled) && IsOffAt(scope, options, rule: StanzaRules.Get(owner), position));
	}
}
