using System;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers;

/// <summary>
/// Two rules that judge one gap or construct in opposite ways; the file's settings pick the half that reports.
/// </summary>
internal sealed record RulePair
{
	/// <summary>
	/// The half that owns the gap or construct when both are enabled is the default half unless a pair names one.
	/// </summary>
	public RulePair(
		DiagnosticDescriptor first,
		DiagnosticDescriptor second,
		DiagnosticDescriptor defaultHalf,
		DiagnosticDescriptor? ownerWhenBothEnabled = null
	)
	{
		First = first;
		Second = second;
		DefaultHalf = defaultHalf;
		OwnerWhenBothEnabled = ownerWhenBothEnabled ?? defaultHalf;
	}

	public DiagnosticDescriptor First { get; }
	public DiagnosticDescriptor Second { get; }
	public DiagnosticDescriptor DefaultHalf { get; }
	public DiagnosticDescriptor OwnerWhenBothEnabled { get; }

	public DiagnosticDescriptor GetOtherHalf(string ruleId)
	{
		if (string.Equals(ruleId, First.Id, StringComparison.Ordinal))
			return Second;

		return string.Equals(ruleId, Second.Id, StringComparison.Ordinal)
			? First
			: throw new ArgumentException($"{ruleId} is not a half of this pair.", nameof(ruleId));
	}
}
